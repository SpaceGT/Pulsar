using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Pulsar.Shared;
using Sandbox;
using Sandbox.Engine.Utils;
using Sandbox.Game;
using SpaceEngineers;
using SpaceEngineers.Game;
using VRage.FileSystem;
using VRage.Plugins;
using VRage.Utils;

namespace Pulsar.Legacy.Launcher
{
    internal class GameLog : IGameLog
    {
        public bool Exists()
        {
            string file = MyLog.Default?.GetFilePath();
            return File.Exists(file) && file.EndsWith(".log");
        }

        public bool Open()
        {
            MyLog.Default.Flush();
            string file = MyLog.Default?.GetFilePath();

            if (!File.Exists(file) || !file.EndsWith(".log"))
                return false;

            Process.Start(file);
            return true;
        }

        public void Write(string line) => MyLog.Default.WriteLine(line);
    }

    internal static class Game
    {
        public static void RegisterPlugin(IPlugin plugin)
        {
            FieldInfo m_pluginsField = typeof(MyPlugins).GetField(
                "m_plugins",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            List<IPlugin> m_plugins = (List<IPlugin>)m_pluginsField.GetValue(null);
            m_plugins.Add(plugin);
        }

        public static void SetMainAssembly(Assembly assembly)
        {
            FieldInfo mainAssemblyField = typeof(MyFileSystem).GetField(
                "m_mainAssembly",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            mainAssemblyField.SetValue(null, assembly);

            FieldInfo mainAssemblyNameField = typeof(MyFileSystem).GetField(
                "MainAssemblyName",
                BindingFlags.Static | BindingFlags.Public
            );
            mainAssemblyNameField.SetValue(null, assembly.GetName().Name);

            var exePath = new FileInfo(assembly.Location).DirectoryName;
            var rootPath = new FileInfo(exePath).Directory?.FullName ?? Path.GetFullPath(exePath);

            MyFileSystem.ExePath = exePath;
            MyFileSystem.RootPath = rootPath;
        }

        public static ResolveEventHandler GameAssemblyResolver(string bin64Dir)
        {
            return (sender, args) =>
            {
                string targetName = new AssemblyName(args.Name).Name;
                string loaderBase = AppDomain.CurrentDomain.BaseDirectory;
                string targetPath = Path.Combine(bin64Dir, targetName);

                if (File.Exists(targetPath + ".dll"))
                    return Assembly.LoadFrom(targetPath + ".dll");

                if (File.Exists(targetPath + ".exe"))
                    return Assembly.LoadFrom(targetPath + ".exe");

                return null;
            };
        }

        public static Version GetGameVersion(string gameDir)
        {
            // Version is fetched in an appdomain so that references can be unloaded
            AppDomain domain = AppDomain.CreateDomain("GameVersion");
            domain.SetData("GameDir", gameDir);
            domain.DoCallBack(() =>
            {
                AppDomain domain = AppDomain.CurrentDomain;
                string gameDir = (string)domain.GetData("GameDir");
                domain.AssemblyResolve += GameAssemblyResolver(gameDir);
            });
            domain.DoCallBack(() =>
            {
                SpaceEngineersGame.SetupBasicGameInfo();
                int? gameVersionInt = MyPerGameSettings.BasicGameInfo.GameVersion;
                string gameVersionStr = null;

                if (gameVersionInt.HasValue)
                    gameVersionStr = MyBuildNumbers.ConvertBuildNumberFromIntToStringFriendly(
                        gameVersionInt.Value,
                        "."
                    );

                AppDomain.CurrentDomain.SetData("Version", gameVersionStr);
            });

            string version = (string)domain.GetData("Version");
            AppDomain.Unload(domain);

            return new Version(version);
        }

        public static void SetupMyFakes()
        {
            typeof(MyFakes).TypeInitializer.Invoke(null, null);

            MyFakes.ENABLE_SPLASHSCREEN = false;

            if (Tools.HasCommandArg("-f12menu"))
                MyFakes.ENABLE_F12_MENU = true;
        }

        public static void CorrectExitText()
        {
            string message;
            string platform = Tools.FriendlyPlatformName();

            if (platform == null)
                message = "Exit Game";
            else
                message = MyCommonTexts
                    .ScreenMenuButtonExitToWindows.ToString()
                    .Replace("Windows", platform);

            FieldInfo exitTextField = typeof(MyCommonTexts).GetField(
                "ScreenMenuButtonExitToWindows",
                BindingFlags.Static | BindingFlags.Public
            );

            exitTextField.SetValue(null, MyStringId.GetOrCompute(message));
        }

        public static float GetLoadProgress()
        {
            // No native function in Space Engineers does this but we can estimate
            float expectedGrowth = 1100f * 1024 * 1024;

            Process process = Process.GetCurrentProcess();
            process.Refresh();

            float ratio = process.PrivateMemorySize64 / expectedGrowth;

            return Math.Min(1f, Math.Max(0f, ratio));
        }

        public static void StartSpaceEngineers(string[] args) => MyProgram.Main(args);

        public static void ShowIntroVideo(bool enabled) =>
            MyPlatformGameSettings.ENABLE_LOGOS = enabled;

        public static void RunOnGameThread(Action action) =>
            MySandboxGame.Static.Invoke(action, "Pulsar");
    }
}
