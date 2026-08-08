using Pulsar.Shared.Splash;

namespace Pulsar.Legacy.Launcher;

internal static class Progress
{
    public static void Start(string harmonyId)
    {
        if (SplashManager.Instance is null)
            return;

        ProgressTracker tracker = new(harmonyId);
        tracker.Patch("SpaceEngineers.MyProgram, SpaceEngineers", "Main", 0.04f, true);
        tracker.Patch("SpaceEngineers.Game.SpaceEngineersGame, SpaceEngineers.Game", "SetupBasicGameInfo", 0.09f);
        tracker.Patch("Sandbox.MyInitializer, Sandbox.Game", "InvokeBeforeRun", 0.14f);
        tracker.Patch("SpaceEngineers.PlatformInitialization.MySteamInitializer, SpaceEngineers", "InitServices", 0.20f);
        tracker.Patch("SpaceEngineers.Game.SpaceEngineersGame, SpaceEngineers.Game", "SetupPerGameSettings", 0.26f);
        tracker.Patch("Sandbox.MySandboxGame, Sandbox.Game", "InitMultithreading", 0.32f);
        tracker.Patch("Sandbox.Engine.Platform.VideoMode.MyVideoSettingsManager, Sandbox.Game", "Initialize", 0.40f);
        tracker.Patch("SpaceEngineers.Game.SpaceEngineersGame, SpaceEngineers.Game", "InitializeRender", 0.48f);
        tracker.Patch("Sandbox.MySandboxGame, Sandbox.Game", "Initialize", 0.56f, true);
        tracker.Patch("Sandbox.Definitions.MyDefinitionManager, Sandbox.Game", "PreloadDefinitions", 0.65f);
        tracker.Patch("Sandbox.Graphics.GUI.MyDX9Gui, Sandbox.Game", "LoadContent", 0.74f);
        tracker.Patch("Sandbox.Graphics.GUI.MyOffensiveWords, Sandbox.Game", "Init", 0.83f);
        tracker.Patch("SpaceEngineers.Game.SpaceEngineersGame, SpaceEngineers.Game", "InitServices", 0.92f);
        tracker.Patch("Sandbox.MyCommonProgramStartup, Sandbox.Game", "DisposeSplashScreen", 1f);
    }
}
