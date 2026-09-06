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

    public static AppBuilder BuildAvaloniaApp()
    {
        AppBuilder builder = AppBuilder.Configure<App>();
#if NETFRAMEWORK
        Win32PlatformOptions options = new();
        if (Environment.GetEnvironmentVariable("STEAM_COMPAT_PROTON") is not null)
            options.RenderingMode = [Win32RenderingMode.Software];

        builder.UseWin32().With(options);
#else
        builder.UseX11();
#endif
        return builder.UseSkia().WithInterFont().LogToTrace();
    }
}
