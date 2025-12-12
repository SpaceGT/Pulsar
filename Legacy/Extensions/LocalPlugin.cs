using System.Diagnostics;
using System.IO;
using Pulsar.Shared.Data;
using Sandbox.Graphics.GUI;

namespace Pulsar.Legacy.Extensions;

internal static class LocalPluginExtensions
{
    public static void Show(this LocalPlugin localPlugin)
    {
        string file = Path.GetFullPath(localPlugin.Dll);
        if (File.Exists(file))
            Process.Start("explorer.exe", $"/select, \"{file}\"");
    }

    public static void GetDescriptionText(MyGuiControlMultilineText textbox)
    {
        textbox.Visible = false;
        textbox.Clear();
    }
}
