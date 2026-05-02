namespace Pulsar.Shared.Splash;

public class SplashManager
{
    public static SplashManager Instance = null;

    public float BarValue => splash?.BarValue ?? 0f;

    private readonly SplashScreen splash;

    public SplashManager()
    {
        splash = new SplashScreen();
    }

    public void SetText(string msg) => splash.SetText(msg);

    public void SetBarValue(float ratio = float.NaN) => splash.SetBarValue(ratio);

    public void SetTitle(string title) => splash.SetTitle(title);

    public void Delete()
    {
        Instance = null;
        splash.Delete();
    }
}
