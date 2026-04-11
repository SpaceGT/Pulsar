using Avalonia.Controls;

namespace Pulsar.Modern.Screens.PluginConfigurationScreen;

public partial class PluginConfigurationScreen : PluginScreenBase
{
    public PluginConfigurationScreen()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            PluginList.DataContext = PluginConfigurationScreenViewModel.GetDummyVm();
        }
    }

    private void CornerCancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    private void PluginItem_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (((Border)sender).DataContext is not PluginViewModel pluginVM)
            return;

        ScreenTools.PlayClickSound((Control)sender);

        pluginVM.TryOpenSettingsScreen();
    }
}