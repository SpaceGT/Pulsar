using System;
using System.Linq;
using Avalonia;

namespace Pulsar.Interface;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (!args.Contains("--ipc-stdio"))
            return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime([]);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseWin32().UseSkia().LogToTrace();
}
