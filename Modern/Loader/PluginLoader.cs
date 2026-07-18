using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using HarmonyLib;
using Keen.VRage.Core.Plugins;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Extensions;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Splash;
using SharedLoader = Pulsar.Shared.Loader;

namespace Pulsar.Modern.Loader;

internal class PluginLoader : IPlugin, IDisposable
{
    public static PluginLoader Instance;

    private readonly bool init;
    private readonly List<PluginInstance> plugins = [];
    public List<PluginInstance> Plugins => plugins;

    public PluginLoader(PluginHost host)
    {
        Instance = this;
        AppDomain.CurrentDomain.FirstChanceException += OnException;

        LogFile.GameLog.Write("NOTE: Running with Pulsar plugin loader.");

        Assembly currentAssembly = Assembly.GetExecutingAssembly();
        new Harmony(currentAssembly.GetName().Name + ".Late").PatchCategory("Late");

        if (ConfigManager.Instance.SafeMode)
        {
            plugins.Clear();
            LogFile.Warn("Skipping plugin instantiation");
        }
        else
        {
            InstantiatePlugins(host);
        }

        init = true;
    }

    public bool TryGetPluginInstance(string id, out PluginInstance instance)
    {
        instance = null;
        if (!init)
            return false;

        foreach (PluginInstance p in plugins)
        {
            if (p.Id == id)
            {
                instance = p;
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        foreach (PluginInstance p in plugins)
            p.Dispose();
        plugins.Clear();

        LogFile.Dispose();
        Instance = null;
    }

    private void OnException(object sender, FirstChanceExceptionEventArgs e)
    {
        try
        {
            MemberAccessException accessException =
                e.Exception as MemberAccessException
                ?? e.Exception?.InnerException as MemberAccessException;
            if (accessException is not null)
            {
                foreach (PluginInstance plugin in plugins)
                {
                    if (plugin.ContainsExceptionSite(accessException))
                        return;
                }
            }
        }
        catch { } // Do NOT throw exceptions inside this method!
    }

    private void InstantiatePlugins(PluginHost host)
    {
        StringBuilder debugCompileResults = new();

        if (Flags.CheckAllPlugins)
            debugCompileResults.Append("Plugins that failed to Init:").AppendLine();

        foreach (var (data, assembly) in SharedLoader.Instance.Plugins)
            if (PluginInstance.TryGet(data, assembly, out PluginInstance instance))
                plugins.Add(instance);

        LogFile.WriteLine($"Initializing {plugins.Count} plugins");

        int totalPlugins = plugins.Count;

        for (int i = plugins.Count - 1; i >= 0; i--)
        {
            SplashManager.Instance?.SetText($"Loading {i + 1} of {totalPlugins} plugins");
            PluginInstance p = plugins[i];
            if (!p.Instantiate(host))
                plugins.RemoveAtFast(i);

            if (Flags.CheckAllPlugins)
                debugCompileResults
                    .Append(p.FriendlyName ?? "(null)")
                    .Append(" - ")
                    .Append(p.Id ?? "(null)")
                    .Append(" by ")
                    .Append(p.Author ?? "(null)")
                    .AppendLine();
        }

        LogFile.WriteLine($"Initialized {plugins.Count} of {totalPlugins} plugins");
        SplashManager.Instance?.SetText($"Launching Space Engineers 2...");

        if (Flags.CheckAllPlugins)
        {
            Tools.ShowMessageBox("All plugins compiled, log file will now open");
            LogFile.WriteLine(debugCompileResults.ToString());
            LogFile.Open();
        }
    }
}
