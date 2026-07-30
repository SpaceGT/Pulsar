using System;
using Avalonia;

namespace Pulsar.Interface;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
#if NETFRAMEWORK
        AppBuilder.Configure<App>().UseWin32().UseSkia().LogToTrace();
#else
        AppBuilder.Configure<App>().UseX11().UseSkia().LogToTrace();
#endif
}
