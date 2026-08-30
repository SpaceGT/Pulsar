using Pulsar.Shared;
using Pulsar.Shared.Data;
using Sandbox.Graphics.GUI;

namespace Pulsar.Legacy.Extensions;

internal static class LocalPluginExtensions
{
    public static void Show(this LocalPlugin localPlugin)
    {
        Tools.ShowInFileManager(localPlugin.Dll);
    }

    public static void GetDescriptionText(MyGuiControlMultilineText textbox)
    {
        textbox.Visible = false;
        textbox.Clear();
    }
}
