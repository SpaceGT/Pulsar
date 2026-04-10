using Keen.VRage.UI.Screens;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using System.Linq;

namespace Pulsar.Modern.Screens.SourcesScreen.AddRemoteSourceScreen;

internal class AddRemoteSourceScreenViewModel : ScreenViewModel
{
    public enum RemoteSourceType
    {
        Hub,
        Plugin,
        Mod
    }

    public RemoteSourceType SourceType { get; private set; }

    public string DisplayName { get; set; } = string.Empty;
    public string GithubUser { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string BranchName { get; set; } = "main";
    public string MetadataFile { get; set; } = "PluginHub.xml";
    public string SteamId 
    { 
        get;
        set
        {
            string text = value;
            if (text.All(c => char.IsDigit(c)))
            {
                field = text;
                return;
            }
                
            field = string.Concat(text.Where(char.IsNumber));
        }
    } = string.Empty;

    private readonly SourcesScreenViewModel parentVm;

    public AddRemoteSourceScreenViewModel(SourcesScreenViewModel vm, RemoteSourceType sourceType)
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;
        InitializeInputContext();

        parentVm = vm;
        SourceType = sourceType;

        CheckClipboard();
    }

    private void CheckClipboard()
    {
        string clipboard = Tools.GetClipboard();

        if (string.IsNullOrEmpty(clipboard))
            return;


        string[] parts = null;
        switch (SourceType)
        {
            case RemoteSourceType.Hub:
                parts = clipboard.Split('/');
                if (parts.Length == 4 && parts[0] == "sehub")
                {
                    DisplayName = parts[1] + "/" + parts[2];
                    GithubUser = parts[1];
                    RepoName = parts[2];
                    BranchName = parts[3];
                }
                break;
            case RemoteSourceType.Plugin:
                parts = clipboard.Split('/');
                if (parts.Length == 5 && parts[0] == "seplugin")
                {
                    GithubUser = parts[1];
                    RepoName = parts[2];
                    BranchName = parts[3];
                    MetadataFile = parts[4];
                }
                break;
            case RemoteSourceType.Mod:
                parts = clipboard.Split('/');
                if (parts.Length == 3 && parts[0] == "semod")
                {
                    DisplayName = parts[1];
                    SteamId = parts[2];
                }
                break;
        }
    }

    public void AddSource()
    {
        switch (SourceType)
        {
            case RemoteSourceType.Hub:
                RemoteHubConfig hubSource = new()
                {
                    Name = DisplayName,
                    Repo = GithubUser + "/" + RepoName,
                    Branch = BranchName,
                    LastCheck = null,
                    Hash = null,
                    Enabled = true,
                    Trusted = false,
                };
                parentVm.AddRemoteHub(new(hubSource));
                break;
            case RemoteSourceType.Plugin:
                RemotePluginConfig pluginSource = new()
                {
                    Name = RepoName,
                    Repo = GithubUser + "/" + RepoName,
                    Branch = BranchName,
                    File = MetadataFile,
                    LastCheck = null,
                    Enabled = true,
                    Trusted = false,
                };
                parentVm.AddRemotePlugin(new(pluginSource));
                break;
            case RemoteSourceType.Mod:
                ModConfig source = new()
                {
                    Name = DisplayName,
                    ID = long.Parse(SteamId),
                    Enabled = true,
                };
                parentVm.AddMod(new(source));
                break;
        }
    }
}
