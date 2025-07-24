using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Pulsar.Shared;
using Sandbox.Engine.Utils;
using Sandbox.Game;
using SpaceEngineers;
using SpaceEngineers.Game;
using VRage.Plugins;
using VRage.Utils;

namespace Pulsar.Legacy.Launcher
{
    internal static class Game
    {
        public static void RegisterPlugin(Assembly plugin)
        {
            FieldInfo userPluginsField = typeof(MyPlugins).GetField(
                "m_userPluginAssemblies",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            userPluginsField.SetValue(null, new List<Assembly> { plugin });
        }

        public static void StartSpaceEngineers(string[] args)
        {
            MyProgram.Main(args);
        }

        public static Version GetGameVersion()
        {
            SpaceEngineersGame.SetupBasicGameInfo();
            int? gameVersionInt = MyPerGameSettings.BasicGameInfo.GameVersion;
            if (!gameVersionInt.HasValue)
                return null;

            string gameVersionStr = MyBuildNumbers.ConvertBuildNumberFromIntToStringFriendly(
                gameVersionInt.Value,
                "."
            );
            return new Version(gameVersionStr);
        }

        public static void SetupMyFakes()
        {
            typeof(MyFakes).TypeInitializer.Invoke(null, null);

            MyFakes.ENABLE_SPLASHSCREEN = false;

            if (Tools.HasCommandArg("-f12menu"))
                MyFakes.ENABLE_F12_MENU = true;
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

        public static void ShowIntroVideo(bool enabled) =>
            MyPlatformGameSettings.ENABLE_LOGOS = enabled;
    }
}
