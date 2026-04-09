using System.Collections.Generic;
using Avalonia.Controls;
using Keen.VRage.UI.AvaloniaInterface.Services;

namespace Pulsar.Modern.Screens.ProfilesScreen;

[NeedsWindowStyles]
public partial class ProfilesScreen : PluginScreenBase
{
    private Control selectedProfileControl;
    private bool itemSelected = false;

    public ProfilesScreen()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            List<ProfileViewModel> dummyProfiles = [];

            for (int i = 0; i < 25; i++)
            {
                dummyProfiles.Add(ProfileViewModel.GetDummyProfileViewModel());
            }

            ProfilesList.ItemsSource = dummyProfiles;
        }
    }

    private void NewButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (selectedProfileControl is null)
        {
            ((ProfilesScreenViewModel)DataContext).CreateProfile();
        }
        else if (selectedProfileControl.DataContext is ProfileViewModel)
        {
            ((ProfilesScreenViewModel)DataContext).UpdateProfile();
        }
    }

    private void LoadButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (selectedProfileControl.DataContext is not ProfileViewModel)
            return;

        ((ProfilesScreenViewModel)DataContext).LoadProfile();
    }

    private void RenameButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (selectedProfileControl.DataContext is not ProfileViewModel)
            return;

        ((ProfilesScreenViewModel)DataContext).RenameProfile();
    }

    private void DeleteButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (selectedProfileControl.DataContext is not ProfileViewModel)
            return;

        ((ProfilesScreenViewModel)DataContext).DeleteProfile();
    }

    private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    private void ProfileItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (selectedProfileControl != null)
            ((IPseudoClasses)selectedProfileControl.Classes).Remove(":selected");

        selectedProfileControl = (Control)sender;
        ((IPseudoClasses)selectedProfileControl.Classes).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        ((ProfilesScreenViewModel)DataContext).SelectedProfile = (ProfileViewModel)
            ((Control)sender).DataContext;

        NewButton.Content = "Update";
        LoadButton.IsEnabled = true;
        RenameButton.IsEnabled = true;
        DeleteButton.IsEnabled = true;

        itemSelected = true;

        if (e.ClickCount > 1)
        {
            ((ProfilesScreenViewModel)DataContext).LoadProfile();
            Dispose();
        }
    }

    private void UserControl_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (itemSelected)
        {
            itemSelected = false;
            return;
        }

        if (selectedProfileControl != null)
            ((IPseudoClasses)selectedProfileControl.Classes).Remove(":selected");

        selectedProfileControl = null;

        NewButton.Content = "New";
        LoadButton.IsEnabled = false;
        RenameButton.IsEnabled = false;
        DeleteButton.IsEnabled = false;
    }
}
