using System.Text.RegularExpressions;
using Pulsar.Legacy.Plugin.GUI;
using Pulsar.Shared.Data;
using Sandbox.Graphics.GUI;

namespace Pulsar.Legacy.Plugin.Extensions
{
    internal static class PluginDataExtensions
    {
        public static void AddDetailControls(
            this PluginData pluginData,
            PluginDetailMenu screen,
            MyGuiControlBase bottomControl,
            out MyGuiControlBase topControl
        )
        {
            if (pluginData is LocalFolderPlugin localFolderPlugin)
                localFolderPlugin.AddDetailControls(screen, bottomControl, out topControl);
            else if (pluginData is GitHubPlugin gitHubPlugin)
                gitHubPlugin.AddDetailControls(screen, bottomControl, out topControl);
            else
                topControl = null;
        }

        public static void Show(this PluginData pluginData)
        {
            if (pluginData is LocalFolderPlugin localFolderPlugin)
                localFolderPlugin.Show();
            else if (pluginData is GitHubPlugin gitHubPlugin)
                gitHubPlugin.Show();
        }

        public static void GetDescriptionText(
            this PluginData pluginData,
            MyGuiControlMultilineText textbox
        )
        {
            textbox.Visible = true;
            textbox.Clear();
            if (string.IsNullOrEmpty(pluginData.Description))
            {
                if (string.IsNullOrEmpty(pluginData.Tooltip))
                    textbox.AppendText("No description");
                else
                    textbox.AppendText(CapLength(pluginData.Tooltip, 1000));
                return;
            }
            else
            {
                string text = CapLength(pluginData.Description, 1000);
                int textStart = 0;
                foreach (
                    Match m in Regex.Matches(
                        text,
                        @"https?:\/\/(www\.)?[\w-.]{2,256}\.[a-z]{2,4}\b[\w-.@:%\+~#?&//=]*"
                    )
                )
                {
                    int textLen = m.Index - textStart;
                    if (textLen > 0)
                        textbox.AppendText(text.Substring(textStart, textLen));

                    textbox.AppendLink(m.Value, m.Value);
                    textStart = m.Index + m.Length;
                }

                if (textStart < text.Length)
                    textbox.AppendText(text.Substring(textStart));
            }
        }

        private static string CapLength(string s, int len)
        {
            if (s.Length > len)
                return s.Substring(0, len);
            return s;
        }
    }
}
