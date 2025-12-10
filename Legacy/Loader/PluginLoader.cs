using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows.Forms;
using HarmonyLib;
using Pulsar.Legacy.Screens;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Splash;
using Sandbox.Game.World;
using VRage;
using VRage.Plugins;
using SharedLoader = Pulsar.Shared.Loader;

namespace Pulsar.Legacy.Loader;

public class PluginLoader : IHandleInputPlugin
{
    public static PluginLoader Instance;

    private bool init;
    private readonly List<PluginInstance> plugins = [];
    public List<PluginInstance> Plugins => plugins;

    public PluginLoader()
    {
        Instance = this;
        AppDomain.CurrentDomain.FirstChanceException += OnException;
        PlayerConsent.OnConsentChanged += OnConsentChanged;
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

    public void RegisterSessionComponents()
    {
        foreach (PluginInstance plugin in plugins)
            plugin.RegisterSessionComponents(MySession.Static);
    }

    public void RegisterEntityComponents()
    {
        foreach (PluginInstance plugin in plugins)
            plugin.RegisterEntityComponents(MyScriptManager.Static);
    }

    public void Init(object gameInstance)
    {
        StringBuilder debugCompileResults = new();
        Assembly currentAssembly = Assembly.GetExecutingAssembly();
        new Harmony(currentAssembly.GetName().Name + ".Late").PatchCategory("Late");

        if (ConfigManager.Instance.SafeMode)
        {
            plugins.Clear();
            LogFile.Warn("Skipping plugin instantiation");
        }
        else
        {
            InstantiatePlugins();
            LogFile.WriteLine($"Initializing {plugins.Count} plugins");
            SplashManager.Instance?.SetText($"Initializing {plugins.Count} plugins");

            if (Flags.CheckAllPlugins)
                debugCompileResults.Append("Plugins that failed to Init:").AppendLine();

            for (int i = plugins.Count - 1; i >= 0; i--)
            {
                PluginInstance p = plugins[i];
                if (!p.Init(gameInstance))
                {
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
            }
        }

        init = true;

        if (Flags.CheckAllPlugins)
        {
            MessageBox.Show("All plugins compiled, log file will now open");
            LogFile.WriteLine(debugCompileResults.ToString());
            LogFile.Open();
        }

        SplashManager.Instance?.SetText($"Updating workshop items...");
        ProfilesConfig profiles = ConfigManager.Instance.Profiles;
        SteamMods.Update(profiles.Current.Mods);

        ShowGame();
    }

    public void Update()
    {
        if (!init)
            return;

        for (int i = plugins.Count - 1; i >= 0; i--)
        {
            PluginInstance p = plugins[i];
            if (!p.Update())
                plugins.RemoveAtFast(i);
        }
    }

    public void HandleInput()
    {
        if (!init)
            return;

        for (int i = plugins.Count - 1; i >= 0; i--)
        {
            PluginInstance p = plugins[i];
            if (!p.HandleInput())
                plugins.RemoveAtFast(i);
        }
    }

    public void Dispose()
    {
        foreach (PluginInstance p in plugins)
            p.Dispose();
        plugins.Clear();

        PlayerConsent.OnConsentChanged -= OnConsentChanged;
        LogFile.Dispose();
        Instance = null;
    }

    private void OnConsentChanged()
    {
        ConfigManager.Instance.UpdatePlayerStats();
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

    private void InstantiatePlugins()
    {
        foreach (var (data, assembly) in SharedLoader.Instance.Plugins)
        {
            PluginInstance.TryGet(data, assembly, out PluginInstance instance);
            plugins.Add(instance);
        }

        for (int i = plugins.Count - 1; i >= 0; i--)
        {
            PluginInstance p = plugins[i];
            if (!p.Instantiate())
                plugins.RemoveAtFast(i);
        }
    }

    private static void ShowGame()
    {
        SplashManager.Instance?.Delete();
        Patch.Patch_ShowAndFocus.Enabled = true;
        MyVRage.Platform.Windows.Window.ShowAndFocus();
    }
}
