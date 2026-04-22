using Avalonia;
using Avalonia.Markup.Xaml;

namespace Pulsar.Modern;

internal class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
