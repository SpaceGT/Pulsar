using Avalonia.Controls;
using Keen.Game2.Client.UI.Library.Dialogs.TwoOptionsDialog;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Modern.Screens.TextInputDialog;
using Pulsar.Shared;
using Pulsar.Shared.Data;

namespace Pulsar.Modern.Screens.ProfilesScreen;

[NeedsWindowStyles]
public partial class ProfilesScreen : PluginScreenBase
{
    private bool selected = false;
    private bool justSelected = false;

    public ProfilesScreen()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
        {
            foreach (Profile p in (DataContext as ProfilesScreenViewModel).Config.Profiles)
                ProfilesList.Items.Add(new ProfileViewModel(p));
            UpdateButtons();
        }
    }

    private void UpdateButtons()
    {
        bool selected = ProfilesList.SelectedItem is not null;
        NewButton.Content = selected ? "Update" : "New";
        LoadButton.IsEnabled = selected;
        RenameButton.IsEnabled = selected;
        DeleteButton.IsEnabled = selected;
    }

    private void NewButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is null)
        {
            ScreenTools.GetSharedUIComponent().CreateScreen<TextInputDialog.TextInputDialog>(new TextInputDialogViewModel("Profile Name", onComplete: delegate (string name)
            {
                Profile p = (DataContext as ProfilesScreenViewModel).CreateProfile(name);
                ProfilesList.Items.Add(p);
                ProfilesList.SelectedItem = p;
                selected = true;
                UpdateButtons();
            }), true);
        }
        else if (ProfilesList.SelectedItem is Profile profile)
        {
            Profile newProfile = Tools.DeepCopy((DataContext as ProfilesScreenViewModel).Draft);
            newProfile.Name = profile.Name;
            ProfilesList.Items.Insert(ProfilesList.SelectedIndex, newProfile);
            ProfilesList.Items.Remove(ProfilesList.SelectedItem);

            (DataContext as ProfilesScreenViewModel).Config.Remove(profile.Key);
            (DataContext as ProfilesScreenViewModel).Config.Add(newProfile);
        }
    }

    private void LoadButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem as Profile is not null)
        {
            (DataContext as ProfilesScreenViewModel).LoadProfile((Profile)ProfilesList.SelectedItem);
            Dispose();
        }
    }

    private void RenameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is not Profile profile)
            return;

        ScreenTools.GetSharedUIComponent().CreateScreen<TextInputDialog.TextInputDialog>(new TextInputDialogViewModel("Profile Name", profile.Name, delegate (string name)
        {
            if (!(DataContext as ProfilesScreenViewModel).Config.Exists(Tools.CleanFileName(name)))
            {
                (DataContext as ProfilesScreenViewModel).Config.Rename(profile.Key, name);
                profile.Name = name;
                ProfilesList.Items.Insert(ProfilesList.SelectedIndex + 1, profile);
                ProfilesList.Items.Remove(ProfilesList.SelectedItem);
            }
            else
                (DataContext as ProfilesScreenViewModel).ShowDuplicateWarning(name);
        }), true);
    }

    private void DeleteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is not Profile profile)
            return;

        var definition = ScreenTools.GetDefaultYesNoDialog();
        definition.Title = ScreenTools.GetKeyFromString("Delete Profile");
        definition.Content = ScreenTools.GetKeyFromString($"Are you sure you want to delete \"{profile.Name}\"?");

        ScreenTools.GetSharedUIComponent().ShowDialog(new TwoOptionsDialogViewModel(definition) 
        {
            ConfirmAction = () =>
            {
                (DataContext as ProfilesScreenViewModel).Config.Remove(profile.Key);
                ProfilesList.Items.Remove(ProfilesList.SelectedItem);
                UpdateButtons();
            },
        });
    }

    private void ProfilesList_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        justSelected = true;
        selected = true;
        UpdateButtons();
    }

    private void ProfilesList_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (ProfilesList.SelectedItem as Profile is not null)
        {
            (DataContext as ProfilesScreenViewModel).LoadProfile((Profile)ProfilesList.SelectedItem);
            Dispose();
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    private void ProfilesList_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (justSelected)
        {
            justSelected = false;
            return;
        }

        if (ProfilesList.SelectedItem as Profile is not null && selected)
        {
            ProfilesList.SelectedIndex = -1;
            selected = false;
        }
    }
}