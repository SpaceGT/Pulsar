using Pulsar.Legacy.Screens;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Sandbox.Graphics.GUI;

namespace Pulsar.Legacy.Extensions;

internal static class GitHubPluginExtensions
{
    public static void AddDetailControls(
        this GitHubPlugin gitHubPlugin,
        PluginDetailMenu screen,
        MyGuiControlBase bottomControl,
        out MyGuiControlBase topControl
    )
    {
        if (gitHubPlugin.AlternateVersions is null || gitHubPlugin.AlternateVersions.Length == 0)
        {
            topControl = null;
            return;
        }

        var draftConfig = (GitHubPluginConfig)screen.draft.GetData(gitHubPlugin.Id);

        MyGuiControlCombobox versionDropdown = new();
        versionDropdown.AddItem(-1, "Default");
        int selectedKey = -1;
        for (int i = 0; i < gitHubPlugin.AlternateVersions.Length; i++)
        {
            GitHubPlugin.Branch version = gitHubPlugin.AlternateVersions[i];
            versionDropdown.AddItem(i, version.Name);
            if (version.Name == draftConfig?.SelectedVersion)
                selectedKey = i;
        }
        versionDropdown.SelectItemByKey(selectedKey);
        versionDropdown.Enabled = draftConfig is not null;
        versionDropdown.ItemSelected += () =>
        {
            int selectedKey = (int)versionDropdown.GetSelectedKey();
            GitHubPlugin.Branch version = gitHubPlugin.AlternateVersions[selectedKey];
            draftConfig.SelectedVersion = version.Name;
        };

        screen.PositionAbove(bottomControl, versionDropdown, MyAlignH.Left);
        screen.Controls.Add(versionDropdown);

        MyGuiControlLabel lblVersion = new(text: "Installed Version");
        screen.PositionAbove(versionDropdown, lblVersion, align: MyAlignH.Left, spacing: 0);
        screen.Controls.Add(lblVersion);
        topControl = lblVersion;
    }

    public static void Show(this GitHubPlugin gitHubPlugin)
    {
        MyGuiSandbox.OpenUrl(
            "https://github.com/" + gitHubPlugin.Id,
            UrlOpenMode.SteamOrExternalWithConfirm
        );
    }
}
