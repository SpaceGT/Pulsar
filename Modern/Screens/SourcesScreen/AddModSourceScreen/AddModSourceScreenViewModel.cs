using System;
using Keen.VRage.UI.Screens;

namespace Pulsar.Modern.Screens.SourcesScreen.AddModSourceScreen;

internal class AddModSourceScreenViewModel : ScreenViewModel
{
    public string Title { get; private set; }
    public string Text { get; set; }
    public readonly Action<string> OnComplete;

    public AddModSourceScreenViewModel(
        string title,
        string defaultText = null,
        Action<string> onComplete = null
    )
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;
        InitializeInputContext();
        Title = title;
        Text = defaultText;
        OnComplete = onComplete;
    }
}
