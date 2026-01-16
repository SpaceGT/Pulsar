using Keen.Game2.Client.UI.Library.Dialogs.OneOptionDialog;
using Keen.VRage.UI.Screens;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using System;

namespace Pulsar.Modern.Screens.ProfilesScreen;

internal class ProfilesScreenViewModel : ScreenViewModel
{
    public readonly ProfilesConfig Config = ConfigManager.Instance.Profiles;

    public readonly Profile draft;
    private event Action<Profile> onDraftChange;

    public ProfilesScreenViewModel(Profile draft, Action<Profile> onDraftChange)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        this.draft = draft;
        this.onDraftChange = onDraftChange;

        InitializeInputContext();
    }

    public void LoadProfile(Profile p)
    {
        Profile newDraft = Tools.DeepCopy(p);
        onDraftChange(newDraft);
    }

    public Profile CreateProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        Profile newProfile = Tools.DeepCopy(draft);
        newProfile.Name = name;

        if (Config.Exists(newProfile.Key))
        {
            ShowDuplicateWarning(name);
            return null;
        }

        Config.Add(newProfile);
        return newProfile;
    }

    public void ShowDuplicateWarning(string name)
    {
        var definition = ScreenTools.GetDefaultOkDialog();
        definition.Title = ScreenTools.GetKeyFromString("Duplicate Profile");
        definition.Content = ScreenTools.GetKeyFromString($"A profile called {name} already exists!\n" + "Please enter a different name.");

        ScreenTools.GetSharedUIComponent().ShowDialog(new OneOptionDialogViewModel(definition));
    }
}
