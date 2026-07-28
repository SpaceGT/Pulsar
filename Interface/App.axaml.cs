using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Pulsar.Interface;

internal class App : Application
{
    private InterfaceServer server;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            throw new InvalidOperationException("The Pulsar interface requires a desktop lifetime.");

        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        server = new InterfaceServer(new WindowManager(desktop));
        server.Start();

        base.OnFrameworkInitializationCompleted();
    }
}
