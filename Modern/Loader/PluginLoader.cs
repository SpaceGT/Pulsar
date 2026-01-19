using HarmonyLib;
using Keen.Game2.Game.Plugins;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Project;
using Keen.VRage.Library.Extensions;
using Pulsar.Modern.Screens;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Splash;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows.Forms;
using SharedLoader = Pulsar.Shared.Loader;

namespace Pulsar.Modern.Loader;

internal class PluginLoader : IPlugin, IDisposable
{
    public static PluginLoader Instance;

    private bool init;
    private readonly List<PluginInstance> plugins = [];
    public List<PluginInstance> Plugins => plugins;

    public PluginLoader(PluginHost host)
    {
        Instance = this;
        AppDomain.CurrentDomain.FirstChanceException += OnException;

        Assembly currentAssembly = Assembly.GetExecutingAssembly();
        new Harmony(currentAssembly.GetName().Name + ".Late").PatchCategory("Late");

        if (ConfigManager.Instance.SafeMode)
        {
            plugins.Clear();
            LogFile.Warn("Skipping plugin instantiation");
        }
        else
        {
            LogFile.WriteLine($"Initializing plugins");
            SplashManager.Instance?.SetText($"Initializing plugins");
            InstantiatePlugins(host);
            LogFile.WriteLine($"Initialized {plugins.Count} plugins");
            SplashManager.Instance?.SetText($"Initialized {plugins.Count} plugins");
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

        for (int i = plugins.Count - 1; i >= 0; i--)
        {
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

        if (Flags.CheckAllPlugins)
        {
            MessageBox.Show("All plugins compiled, log file will now open");
            LogFile.WriteLine(debugCompileResults.ToString());
            LogFile.Open();
        }
    }
}
