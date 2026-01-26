using Keen.VRage.UI.Screens;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pulsar.Modern.Screens.SourcesScreen.SourceInfoScreen;

internal class SourceInfoScreenViewModel : ScreenViewModel
{
    public SourceInfoScreenViewModel(object originalList, List<object> removeList, object sourceVm) 
    {
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;

        InitializeInputContext();
    }
}
