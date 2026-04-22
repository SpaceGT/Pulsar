using Avalonia;
using Avalonia.Controls;
using Keen.VRage.UI.Screens;

namespace Pulsar.Modern.Screens;

public abstract class PluginScreenBase : ScreenView
{
    public override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (Design.IsDesignMode)
        {
            return;
        }
        base.OnAttachedToVisualTree(e);
    }
}
