using Avalonia.Controls;
using Pulsar.Interface.Protocol;

namespace Pulsar.Interface;

internal partial class PromptWindow : Window
{
    public PromptWindow(PromptRequest request)
    {
        InitializeComponent();

        Title = request.Caption;
        MessageText.Text = request.Message;

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

    private void AddButton(string text, PromptResult result, bool isDefault = false)
    {
        Button button = new()
        {
            Content = text,
            MinWidth = 90,
            IsDefault = isDefault,
        };
        button.Click += (_, _) => Close(result);
        ButtonsPanel.Children.Add(button);
    }
}
