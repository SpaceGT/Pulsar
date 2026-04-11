using System.Threading;

namespace Pulsar.Shared.Splash;

public class SplashManager
{
    public static SplashManager Instance = null;
    public float BarValue => 0.0f;

    private readonly ManualResetEventSlim ready = new();
    private readonly Thread thread;

    public SplashManager()
    {
        thread = new Thread(() =>
        {
            ready.Set();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait();
    }

    public void SetText(string msg)
    {
    }

    public void SetBarValue(float ratio = float.NaN)
    {
    }

    public void SetTitle(string title)
    {
    }

    public void Delete()
    {
    }
}
