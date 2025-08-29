using System.Threading;
using System.Windows.Forms;

namespace Pulsar.Shared.Splash;

public class SplashManager
{
    public static SplashManager Instance = null;
    public float BarValue => splash.BarValue;

    private readonly ManualResetEventSlim ready = new();
    private readonly Thread thread;
    private SplashScreen splash;

    public SplashManager()
    {
        thread = new Thread(() =>
        {
            splash = new SplashScreen();
            ready.Set();
            Application.Run(splash);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait();
    }

    public void SetText(string msg) => splash.Invoke(() => splash.SetText(msg));

    public void SetBarValue(float ratio = float.NaN) =>
        splash.Invoke(() => splash.SetBarValue(ratio));

    public void SetTitle(string title) => splash.Invoke(() => splash.Text = title);

    public void Delete()
    {
        splash.Invoke(splash.Delete);
        Instance = null;
    }
}
