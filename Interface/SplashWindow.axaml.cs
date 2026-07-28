using Avalonia.Controls;

namespace Pulsar.Interface;

internal partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void SetText(string text)
    {
        ProgressText.Text = text;
        ProgressBar.IsVisible = false;
    }

    public void SetProgress(float? progress)
    {
        ProgressBar.IsVisible = progress.HasValue;
        if (progress.HasValue)
            ProgressBar.Value = progress.Value;
    }
}
