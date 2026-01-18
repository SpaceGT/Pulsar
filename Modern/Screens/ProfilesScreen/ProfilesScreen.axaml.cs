using Avalonia.Controls;
using Keen.Game2.Client.UI.Menu.News;
using Keen.VRage.UI.AvaloniaInterface.Services;
using Pulsar.Modern.Screens.PluginsScreen;
using System.Collections.Generic;
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
            (DataContext as ProfilesScreenViewModel).CreateProfile();
        }
        else if (selectedProfileControl.DataContext is ProfileViewModel profile)
        {
            (DataContext as ProfilesScreenViewModel).UpdateProfile();
        }
    }

    private void LoadButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (selectedProfileControl.DataContext is not ProfileViewModel profile)
            return;

        (DataContext as ProfilesScreenViewModel).LoadProfile();
    }

    private void RenameButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (selectedProfileControl.DataContext is not ProfileViewModel profile)
            return;

        (DataContext as ProfilesScreenViewModel).RenameProfile();
    }

    private void DeleteButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (selectedProfileControl.DataContext is not ProfileViewModel profile)
            return;

        (DataContext as ProfilesScreenViewModel).DeleteProfile();
    }

    private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    private void ProfileItem_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (selectedProfileControl != null)
            (selectedProfileControl.Classes as IPseudoClasses).Remove(":selected");

        selectedProfileControl = sender as Control;
        (selectedProfileControl.Classes as IPseudoClasses).Add(":selected");

        ScreenTools.PlayClickSound((Control)sender);

        (DataContext as ProfilesScreenViewModel).SelectedProfile = (ProfileViewModel)(sender as Control).DataContext;

        NewButton.Content = "Update";
        LoadButton.IsEnabled = true;
        RenameButton.IsEnabled = true;
        DeleteButton.IsEnabled = true;

        itemSelected = true;
    }

    private void UserControl_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (itemSelected)
        {
            itemSelected = false;
            return;
        }

        if (selectedProfileControl != null)
            (selectedProfileControl.Classes as IPseudoClasses).Remove(":selected");

        selectedProfileControl = null;

        NewButton.Content = "New";
        LoadButton.IsEnabled = false;
        RenameButton.IsEnabled = false;
        DeleteButton.IsEnabled = false;
    }
}