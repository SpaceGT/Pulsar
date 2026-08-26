using Pulsar.Shared.Splash;

namespace Pulsar.Modern.Launcher;

internal static class Progress
{
    public static void Start(string harmonyId)
    {
        if (SplashManager.Instance is null)
            return;

        ProgressTracker tracker = new(harmonyId);
        tracker.Patch("Keen.VRage.Core.VRageCore, VRage.Core", "CreateEngineBuilder", 0.05f);
        tracker.Patch("Keen.VRage.Core.Plugins.PluginHost, VRage.Core", "InvokeOnBeforeProjectsLoaded", 0.10f);
        tracker.Patch("Keen.VRage.Core.Plugins.PluginHost, VRage.Core", "InvokeOnBeforeEngineInstantiated", 0.15f);
        tracker.Patch("Keen.VRage.Core.EngineComponents.EngineDataLoaderComponent, VRage.Core", "Init", 0.22f);
        tracker.Patch("Keen.VRage.Render12.EngineComponents.Render12EngineComponent, VRage.Render12", "PostInit", 0.30f);
        tracker.Patch("Keen.VRage.UI.EngineComponents.UIEngineComponent, VRage.UI", "PostInit", 0.37f);
        tracker.Patch("Keen.Game2.GameAppComponent, SpaceEngineers2", "PostInit", 0.45f);
        tracker.Patch("Keen.Game2.Client.UI.Library.SharedUIComponent, Game2.Client", "PostInit", 0.53f);
        tracker.Patch("Keen.Game2.GameApp, SpaceEngineers2", "PostEngineInit", 0.61f);
        tracker.Patch("Keen.VRage.Core.EngineComponents.EngineBuilder, VRage.Core", "Dispose", 0.77f);
        tracker.Patch("Keen.Game2.Simulation.RuntimeSystems.CoreScenes.GameCoreScene, Game2.Simulation", "TransitionToMainMenu", 0.90f);
        tracker.Patch("Keen.VRage.Core.VRageCore, VRage.Core", "NotifyApplicationReady", 1f, true);
    }
}
