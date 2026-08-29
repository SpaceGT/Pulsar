using System;
using Avalonia;

namespace Pulsar.Interface;

internal static class Program
{
    private const string BuiltInComInterop =
        "System.Runtime.InteropServices.BuiltInComInterop.IsSupported";

    [STAThread]
    public static void Main(string[] args)
    {
#if NETFRAMEWORK
        // Mono's ConditionalWeakTable crashes Avalonia's Win32 automation.
        if (Type.GetType("Mono.Runtime") is not null)
            AppDomain.CurrentDomain.SetData(BuiltInComInterop, "false");
#endif

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
#if NETFRAMEWORK
        AppBuilder.Configure<App>().UseWin32().UseSkia().LogToTrace();
#else
        AppBuilder.Configure<App>().UseX11().UseSkia().LogToTrace();
#endif
}
