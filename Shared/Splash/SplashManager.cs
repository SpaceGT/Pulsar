using System;

namespace Pulsar.Shared.Splash;

public class SplashManager
{
    public static SplashManager Instance = null;
    private bool available = true;

    public SplashManager() => TrySend(Tools.Interface.ShowSplash);

    public void SetText(string msg) => TrySend(() => Tools.Interface.SetSplashText(msg));

    public void SetBarValue(float ratio) => TrySend(() => Tools.Interface.SetSplashProgress(ratio));

    public void SetTitle(string title) => TrySend(() => Tools.Interface.SetSplashTitle(title));

    public void Delete()
    {
        ProgressTracker.ClearActive();
        Instance = null;
        TrySend(Tools.Interface.CloseSplash);
    }

    private void TrySend(Action action)
    {
        if (!available)
            return;

        try
        {
            action();
        }
        catch (Exception e)
        {
            available = false;
            ProgressTracker.ClearActive();
            LogFile.Error("Pulsar interface failed: " + e);
            Tools.Interface.Dispose();
        }
    }
}
