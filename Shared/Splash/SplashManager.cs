namespace Pulsar.Shared.Splash;

public class SplashManager
{
    public static SplashManager Instance = null;
    public float BarValue { get; private set; } = float.NaN;
    private bool available = true;

    public SplashManager()
    {
        TrySend(Tools.Interface.ShowSplash);
    }

    public void SetText(string msg)
    {
        BarValue = float.NaN;
        TrySend(() => Tools.Interface.SetSplashText(msg));
    }

    public void SetBarValue(float ratio = float.NaN)
    {
        BarValue = ratio;
        TrySend(() => Tools.Interface.SetSplashProgress(float.IsNaN(ratio) ? null : ratio));
    }

    public void SetTitle(string title) => TrySend(() => Tools.Interface.SetSplashTitle(title));

    public void Delete()
    {
        Instance = null;
        TrySend(Tools.Interface.CloseSplash);
    }

    private void TrySend(System.Action action)
    {
        if (!available)
            return;

        try
        {
            action();
        }
        catch (System.Exception e)
        {
            available = false;
            LogFile.Error("Pulsar interface failed: " + e);
            Tools.Interface.Dispose();
        }
    }
}
