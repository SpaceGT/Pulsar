using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Pulsar.Protocol.Interface;

namespace Pulsar.Interface;

internal partial class PromptWindow : Window
{
    private readonly TaskCompletionSource<PromptResult> completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    public Task<PromptResult> Completion => completion.Task;

    public PromptWindow(PromptRequest request)
    {
        InitializeComponent();
        Closed += (_, _) => completion.TrySetResult(PromptResult.Cancel);

        Title = request.Caption;
        MessageText.Text = request.Message;
        SetIcon(request.Icon);

        switch (request.Buttons)
        {
            case PromptButtons.YesNoCancel:
                AddButton("Cancel", PromptResult.Cancel);
                AddButton("No", PromptResult.No);
                AddButton("Yes", PromptResult.Yes, true);
                break;
            case PromptButtons.YesNo:
                AddButton("No", PromptResult.No);
                AddButton("Yes", PromptResult.Yes, true);
                break;
            default:
                AddButton("OK", PromptResult.Ok, true);
                break;
        }
    }

    private void SetIcon(PromptIcon icon)
    {
        switch (icon)
        {
            case PromptIcon.Error:
                IconText.Text = "X";
                IconPanel.Background = Brushes.Firebrick;
                break;
            case PromptIcon.Warning:
                IconText.Text = "!";
                IconPanel.Background = Brushes.DarkOrange;
                break;
            case PromptIcon.Question:
                IconText.Text = "?";
                IconPanel.Background = Brushes.DodgerBlue;
                break;
            case PromptIcon.Information:
                IconText.Text = "i";
                IconPanel.Background = Brushes.DodgerBlue;
                break;
            default:
                IconPanel.IsVisible = false;
                break;
        }
    }

    private void AddButton(string text, PromptResult result, bool isDefault = false)
    {
        Button button = new()
        {
            Content = text,
            MinWidth = 90,
            IsDefault = isDefault,
        };
        button.Click += (_, _) =>
        {
            completion.TrySetResult(result);
            Close();
        };
        ButtonsPanel.Children.Add(button);
    }
}
