using System;
using Keen.VRage.UI.Screens;
using Pulsar.Shared.Config;

namespace Pulsar.Modern.Screens.SourcesScreen.SourceWarningScreen;

internal class SourceWarningScreenViewModel : ScreenViewModel
{
    public bool HideWarning { get; set; }

    public Action OnAcknowledge { get; private set; }

    private readonly SourcesConfig sourcesConfig;

    public SourceWarningScreenViewModel(SourcesConfig config, Action onAcknowledge)
    {
        sourcesConfig = config;
        OnAcknowledge = onAcknowledge;

        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;
        InitializeInputContext();
    }

    public void SaveConfig()
    {
        if (HideWarning)
        {
            sourcesConfig.ShowWarning = false;
            sourcesConfig.Save();
        }
    }
}
