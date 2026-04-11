using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Keen.VRage.UI.AvaloniaInterface.Services;

namespace Pulsar.Modern.Screens.SourcesScreen.AddRemoteSourceScreen;

[NeedsWindowStyles]
public partial class AddRemoteSourceScreen : PluginScreenBase
{
    public AddRemoteSourceScreen()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            AddInputField("Input Field 1", null, "test123");
            AddInputField("Input Field 2", null, "test123");
        }
        else
        {
            switch (((AddRemoteSourceScreenViewModel)DataContext).SourceType)
            {
                case AddRemoteSourceScreenViewModel.RemoteSourceType.Hub:
                    AddHubControls();
                    break;
                case AddRemoteSourceScreenViewModel.RemoteSourceType.Plugin:
                    AddPluginControls();
                    break;
                case AddRemoteSourceScreenViewModel.RemoteSourceType.Mod:
                    AddModControls();
                    break;
            }
        }
    }

    private void AddButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((AddRemoteSourceScreenViewModel)DataContext).AddSource();
        Dispose();
    }

    private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    private void AddHubControls()
    {
        AddInputField("Display Name", nameof(AddRemoteSourceScreenViewModel.DisplayName));
        AddInputField("GitHub User", nameof(AddRemoteSourceScreenViewModel.GithubUser));
        AddInputField("Repo Name", nameof(AddRemoteSourceScreenViewModel.RepoName));
        AddInputField("Branch Name", nameof(AddRemoteSourceScreenViewModel.BranchName), "main");
    }

    private void AddPluginControls()
    {
        AddInputField("GitHub User", nameof(AddRemoteSourceScreenViewModel.GithubUser));
        AddInputField("Repo Name", nameof(AddRemoteSourceScreenViewModel.RepoName));
        AddInputField("Branch Name", nameof(AddRemoteSourceScreenViewModel.BranchName), "main");
        AddInputField(
            "Metadata File",
            nameof(AddRemoteSourceScreenViewModel.MetadataFile),
            "PluginHub.xml"
        );
    }

    private void AddModControls()
    {
        AddInputField("Display Name", nameof(AddRemoteSourceScreenViewModel.DisplayName));
        AddInputField("Steam ID", nameof(AddRemoteSourceScreenViewModel.SteamId));
    }

    private void AddInputField(string name, string propertyName, string defaultText = "")
    {
        TextBlock inputName = new()
        {
            Text = name,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0),
            FontSize = 18,
            FontWeight = FontWeight.Normal,
            Foreground = Brushes.White,
        };

        ControlsPanel.Children.Add(inputName);

        TextBox inputBox = new()
        {
            Text = defaultText,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 8, 0, 0),
            MinHeight = 55.5,
        };

        if (propertyName is not null && propertyName != string.Empty)
            inputBox[!TextBox.TextProperty] = new Binding(propertyName);

        ControlsPanel.Children.Add(inputBox);
    }
}
