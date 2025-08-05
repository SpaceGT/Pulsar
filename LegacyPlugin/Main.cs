using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows.Forms;
using HarmonyLib;
using Pulsar.Legacy.Plugin.GUI;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Splash;
using Sandbox.Game.World;
using VRage.Plugins;
using VRage.Utils;

namespace Pulsar.Legacy.Plugin
{
    public class Main : IHandleInputPlugin
    {
        public static Main Instance;

        private bool init;
        private readonly PluginConfig config;
        private readonly StringBuilder debugCompileResults = new();
        private readonly List<PluginInstance> plugins = [];
        public List<PluginInstance> Plugins => plugins;

        public Main()
        {
            Instance = this;
            config = ConfigManager.Instance.Config;

            Assembly pluginAssembly = Assembly.GetExecutingAssembly();
            new Harmony(pluginAssembly.GetName().Name).PatchAll(pluginAssembly);

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

        // Skip local plugins, keep only enabled ones
        public string[] TrackablePluginIds =>
            [.. config.EnabledPlugins.Where(x => !x.IsLocal).Select(x => x.Id)];

        public void RegisterComponents()
        {
            LogFile.WriteLine($"Registering {plugins.Count} components");
            foreach (PluginInstance plugin in plugins)
                plugin.RegisterSession(MySession.Static);
        }

        public void DisablePlugins()
        {
            LogFile.WriteLine("Skipping plugin instantiation");
            plugins.Clear();
        }

        public void InstantiatePlugins()
        {
            LogFile.WriteLine($"Loading {Loader.Instance.Plugins.Count} plugins");

            foreach (var (data, assembly) in Loader.Instance.Plugins)
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

        public void Init(object gameInstance)
        {
            if (ConfigManager.Instance.SafeMode)
                DisablePlugins();
            else
                InstantiatePlugins();

            LogFile.WriteLine($"Initializing {plugins.Count} plugins");
            SplashManager.Instance?.SetText($"Initializing {plugins.Count} plugins");

            if (ConfigManager.Instance.DebugCompileAll)
                debugCompileResults.Append("Plugins that failed to Init:").AppendLine();
            for (int i = plugins.Count - 1; i >= 0; i--)
            {
                PluginInstance p = plugins[i];
                if (!p.Init(gameInstance))
                {
                    plugins.RemoveAtFast(i);
                    if (ConfigManager.Instance.DebugCompileAll)
                        debugCompileResults
                            .Append(p.FriendlyName ?? "(null)")
                            .Append(" - ")
                            .Append(p.Id ?? "(null)")
                            .Append(" by ")
                            .Append(p.Author ?? "(null)")
                            .AppendLine();
                }
            }
            init = true;

            if (ConfigManager.Instance.DebugCompileAll)
            {
                MessageBox.Show("All plugins compiled, log file will now open");

                LogFile.WriteLine(debugCompileResults.ToString());

                string file = MyLog.Default.GetFilePath();
                if (File.Exists(file) && file.EndsWith(".log"))
                    Process.Start(file);
            }

            SplashManager.Instance?.SetText($"Updating workshop items...");
            PluginConfig config = ConfigManager.Instance.Config;
            Steam.Update(config.EnabledPlugins.OfType<ISteamItem>().Select(mod => mod.WorkshopId));

            SplashManager.Instance?.Delete();
        }

        public void Update()
        {
            if (init)
            {
                for (int i = plugins.Count - 1; i >= 0; i--)
                {
                    PluginInstance p = plugins[i];
                    if (!p.Update())
                        plugins.RemoveAtFast(i);
                }
            }
        }

        public void HandleInput()
        {
            if (init)
            {
                for (int i = plugins.Count - 1; i >= 0; i--)
                {
                    PluginInstance p = plugins[i];
                    if (!p.HandleInput())
                        plugins.RemoveAtFast(i);
                }
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
                if (accessException != null)
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
    }
}
