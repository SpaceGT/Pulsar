using VRage.Plugins;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.IO;
using HarmonyLib;
using System.Windows.Forms;
using Sandbox.Game.World;
using System.Diagnostics;
using System.Linq;
using avaness.PluginLoader.Compiler;
using avaness.PluginLoader.GUI;
using avaness.PluginLoader.Data;
using avaness.PluginLoader.Stats;
using avaness.PluginLoader.Network;
using System.Runtime.ExceptionServices;
using avaness.PluginLoader.Stats.Model;
using avaness.PluginLoader.Config;
using VRage.Utils;
using System.Text;
using VRage;

namespace avaness.PluginLoader
{
    public class Main : IHandleInputPlugin
    {
        const string HarmonyVersion = "2.3.6.0";

        public static Main Instance;

        public PluginList List { get; }
        public PluginConfig Config { get; }
        public SplashScreen Splash { get; }
        public PluginStats Stats {get; private set; }

        /// <summary>
        /// True if a local plugin was loaded
        /// </summary>
        public bool HasLocal { get; private set; }

        public bool DebugCompileAll { get; }

        private bool init;
        private readonly StringBuilder debugCompileResults = new StringBuilder();
        private readonly List<PluginInstance> plugins = new List<PluginInstance>();
        public List<PluginInstance> Plugins => plugins;

        public Main()
        {
            // Remember to setup AssemblyResolve in the calling process if dependencies
            // are being loaded from a subdir (like the one used by the deploy script)

            Stopwatch sw = Stopwatch.StartNew();

            Splash = new SplashScreen();

            Instance = this;

            Cursor temp = Cursor.Current;
            Cursor.Current = Cursors.AppStarting;

            string pluginsDir = LoaderTools.PluginsDir;
            Directory.CreateDirectory(pluginsDir);

            LogFile.Init(pluginsDir);
            LogFile.WriteLine("Starting - v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));

            DebugCompileAll = Environment.GetCommandLineArgs()?.Any(x => x != null && x.Equals("-testcompileall", StringComparison.InvariantCultureIgnoreCase)) == true;
            if (DebugCompileAll)
                LogFile.WriteLine("COMPILING ALL PLUGINS");

            GitHub.Init();

            Splash.SetText("Finding references...");
            RoslynReferences.GenerateAssemblyList();

            AppDomain.CurrentDomain.FirstChanceException += OnException;

            Splash.SetText("Starting...");
            Config = PluginConfig.Load(pluginsDir);
            Config.CheckGameVersion();
            List = new PluginList(pluginsDir, Config);
            
            Splash.SetText("Starting...");
            Config.Init(List, DebugCompileAll);

            if (Config.GameVersionChanged)
                ClearGitHubCache(pluginsDir);

            StatsClient.OverrideBaseUrl(Config.StatsServerBaseUrl);
            UpdatePlayerStats();
            PlayerConsent.OnConsentChanged += OnConsentChanged;

            bool devTools = Environment.GetCommandLineArgs()?.Any(x => x != null && x.Equals("-f12menu", StringComparison.InvariantCultureIgnoreCase)) == true;
            if (devTools)
                MyFakes.ENABLE_F12_MENU = true;

            Splash.SetText("Patching...");
            LogFile.WriteLine("Patching");

            // Check harmony version
            Version expectedHarmony = new Version(HarmonyVersion);
            Version actualHarmony = typeof(Harmony).Assembly.GetName().Version;
            if (expectedHarmony != actualHarmony)
                LogFile.Warn($"Unexpected Harmony version, plugins may be unstable. Expected {expectedHarmony} but found {actualHarmony}");

            new Harmony("avaness.PluginLoader").PatchAll(Assembly.GetExecutingAssembly());

            Splash.SetText("Instantiating plugins...");
            LogFile.WriteLine("Instantiating plugins");

            if(DebugCompileAll)
                debugCompileResults.Append("Plugins that failed to compile:").AppendLine();
            foreach (PluginData data in Config.EnabledPlugins)
            {
                if (PluginInstance.TryGet(data, out PluginInstance p))
                {
                    plugins.Add(p);
                    if (data.IsLocal)
                        HasLocal = true;
                }
                else if(DebugCompileAll)
                {
                    debugCompileResults.Append(data.FriendlyName ?? "(null)").Append(" - ").Append(data.Id ?? "(null)").Append(" by ").Append(data.Author ?? "(null)").AppendLine();
                }
            }

            sw.Stop();

            // FIXME: It can potentially run in the background speeding up the game's startup
            ReportEnabledPlugins();

            LogFile.WriteLine($"Finished startup. Took {sw.ElapsedMilliseconds}ms");

            Cursor.Current = temp;

            Splash.Delete();
            Splash = null;

        }

        private void OnException(object sender, FirstChanceExceptionEventArgs e)
        {
            try
            {
                MemberAccessException accessException = e.Exception as MemberAccessException;
                if (accessException == null)
                    accessException = e.Exception?.InnerException as MemberAccessException;
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
        
        public void UpdatePlayerStats()
        {
            ParallelTasks.Parallel.Start(() =>
            {
                Stats = StatsClient.DownloadStats();
            });
        }

        private void ClearGitHubCache(string pluginsDir)
        {
            string pluginCache = Path.Combine(pluginsDir, "GitHub");
            if (!Directory.Exists(pluginCache))
                return;

            bool hasGitHub = Config.EnabledPlugins.Any(x => x is GitHubPlugin);

            if(hasGitHub)
            {
                LoaderTools.ShowMessageBox("Space Engineers has been updated, so all plugins that are currently enabled must be downloaded and compiled.");
            }

            try
            {
                LogFile.WriteLine("Deleting plugin cache because of an update");
                Directory.Delete(pluginCache, true);
            }
            catch (Exception e) 
            {
                LogFile.Error("Failed to delete plugin cache: " + e);
            }
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

        private void ReportEnabledPlugins()
        {
            if (!PlayerConsent.ConsentGiven)
                return;

            Splash.SetText("Reporting plugin usage...");
            LogFile.WriteLine("Reporting plugin usage");

            // Config has already been validated at this point so all enabled plugins will have list items
            // FIXME: Move into a background thread
            if (StatsClient.Track(TrackablePluginIds))
                LogFile.WriteLine("List of enabled plugins has been sent to the statistics server");
            else
                LogFile.Error("Failed to send the list of enabled plugins to the statistics server");
        }

        // Skip local plugins, keep only enabled ones
        public string[] TrackablePluginIds => Config.EnabledPlugins.Where(x => !x.IsLocal).Select(x => x.Id).ToArray();

        public void RegisterComponents()
        {
            LogFile.WriteLine($"Registering {plugins.Count} components");
            foreach (PluginInstance plugin in plugins)
                plugin.RegisterSession(MySession.Static);
        }

        public void DisablePlugins()
        {
            Config.Disable();
            plugins.Clear();
            LogFile.WriteLine("Disabled all plugins");
        }

        public void InstantiatePlugins()
        {
            LogFile.WriteLine($"Loading {plugins.Count} plugins");
            for (int i = plugins.Count - 1; i >= 0; i--)
            {
                PluginInstance p = plugins[i];
                if (!p.Instantiate())
                    plugins.RemoveAtFast(i);
            }
        }

        public void Init(object gameInstance)
        {
            LogFile.WriteLine($"Initializing {plugins.Count} plugins");
            if (DebugCompileAll)
                debugCompileResults.Append("Plugins that failed to Init:").AppendLine();
            for (int i = plugins.Count - 1; i >= 0; i--)
            {
                PluginInstance p = plugins[i];
                if (!p.Init(gameInstance))
                {
                    plugins.RemoveAtFast(i);
                    if(DebugCompileAll)
                        debugCompileResults.Append(p.FriendlyName ?? "(null)").Append(" - ").Append(p.Id ?? "(null)").Append(" by ").Append(p.Author ?? "(null)").AppendLine();
                }
            }
            init = true;

            if(DebugCompileAll)
            {
                MessageBox.Show("All plugins compiled, log file will now open");

                LogFile.WriteLine(debugCompileResults.ToString());

                string file = MyLog.Default.GetFilePath();
                if (File.Exists(file) && file.EndsWith(".log"))
                    Process.Start(file);
            }
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
            UpdatePlayerStats();
        }
    }
}