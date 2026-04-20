using System;
using System.Collections.ObjectModel;
using System.Linq;
using Keen.Game2.Client.UI.Library.Dialogs.OneOptionDialog;
using Keen.Game2.Client.UI.Library.Dialogs.TextInputDialog;
using Keen.Game2.Client.UI.Library.Dialogs.TwoOptionsDialog;
using Keen.VRage.UI.Screens;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Screens.ProfilesScreen;

internal class ProfilesScreenViewModel : ScreenViewModel
{
    public ObservableCollection<ProfileViewModel> Profiles { get; private set; } = [];

    public ProfileViewModel SelectedProfile { get; set; }

    private readonly Profile draft;
    private readonly ProfilesConfig profilesConfig = ConfigManager.Instance?.Profiles;

    private event Action<Profile> onDraftChange;

    private readonly TwoOptionsDialogDefinition renameProfileDialogDef = new()
    {
        SelectedOption = TwoOptionsDialogSelectedOption.Confirm,
        ConfirmOption = ScreenTools.GetKeyFromString("Ok"),
        CancelOption = ScreenTools.GetKeyFromString("Cancel"),
        Title = ScreenTools.GetKeyFromString("Enter Profile Name"),
        Content = ScreenTools.GetKeyFromString("Please enter a name for the profile."),
    };

    public ProfilesScreenViewModel(Profile draft, Action<Profile> onDraftChange)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        this.draft = draft;
        this.onDraftChange = onDraftChange;

        RefreshProfileList();

        InitializeInputContext();
    }

    public void RefreshProfileList()
    {
        Profiles.Clear();

        foreach (Profile profile in profilesConfig.Profiles)
        {
            Profiles.Add(new ProfileViewModel(profile));
        }
    }

    public void LoadProfile()
    {
        Profile newDraft = Tools.DeepCopy(SelectedProfile.Profile);
        onDraftChange(newDraft);
    }

    public void CreateProfile()
    {
        ScreenTools
            .GetSharedUIComponent()
            .ShowDialog(
                new TextInputDialogViewModel(renameProfileDialogDef, string.Empty)
                {
                    ConfirmAction = delegate(string text)
                    {
                        if (string.IsNullOrWhiteSpace(text))
                            return;

                        Profile newProfile = Tools.DeepCopy(draft);
                        newProfile.Name = text;

                        if (profilesConfig.Exists(newProfile.Key))
                        {
                            ShowDuplicateWarning(text);
                            return;
                        }

                        profilesConfig.Add(newProfile);

                        SelectedProfile = null;

                        RefreshProfileList();
                    },
                }
            );
    }

    public void UpdateProfile()
    {
        Profile newProfile = Tools.DeepCopy(draft);
        newProfile.Name = SelectedProfile.Name;

        profilesConfig.Remove(SelectedProfile.Profile.Key);
        profilesConfig.Add(newProfile);

        RefreshProfileList();

        SelectedProfile = Profiles.First(x => x.Name == newProfile.Name);
    }

    public void RenameProfile()
    {
        ScreenTools
            .GetSharedUIComponent()
            .ShowDialog(
                new TextInputDialogViewModel(renameProfileDialogDef, string.Empty)
                {
                    ConfirmAction = delegate(string text)
                    {
                        if (profilesConfig.Exists(Tools.CleanFileName(text)))
                        {
                            ShowDuplicateWarning(text);
                            return;
                        }

                        profilesConfig.Rename(SelectedProfile.Profile.Key, text);

                        RefreshProfileList();

                        SelectedProfile = Profiles.First(x => x.Name == text);
                    },
                }
            );
    }

    public void DeleteProfile()
    {
        var definition = ScreenTools.GetDefaultYesNoDialog();
        definition.Title = ScreenTools.GetKeyFromString("Delete Profile");
        definition.Content = ScreenTools.GetKeyFromString(
            $"Are you sure you want to delete \"{SelectedProfile.Name}\"?"
        );

        ScreenTools
            .GetSharedUIComponent()
            .ShowDialog(
                new TwoOptionsDialogViewModel(definition)
                {
                    ConfirmAction = () =>
                    {
                        profilesConfig.Remove(SelectedProfile.Profile.Key);

                        SelectedProfile = null;

                        RefreshProfileList();
                    },
                }
            );
    }

    private static void ShowDuplicateWarning(string name)
    {
        var definition = ScreenTools.GetDefaultOkDialog();
        definition.Title = ScreenTools.GetKeyFromString("Duplicate Profile");
        definition.Content = ScreenTools.GetKeyFromString(
            $"A profile called {name} already exists!\n" + "Please enter a different name."
        );

        ScreenTools.GetSharedUIComponent().ShowDialog(new OneOptionDialogViewModel(definition));
    }
}
