using Avalonia.Controls;
using Avalonia.Media;
using Pulsar.Interface.Protocol;

namespace Pulsar.Interface;

internal partial class PromptWindow : Window
{
    public PromptWindow(PromptRequest request)
    {
        InitializeComponent();

        Title = request.Caption;
        Heading.Text = request.Caption;
        MessageText.Text = request.Message;
        Header.Background = new SolidColorBrush(GetHeaderColor(request.Icon));

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

    private static Color GetHeaderColor(PromptIcon icon)
    {
        return icon switch
        {
            PromptIcon.Error => Color.Parse("#733A3A"),
            PromptIcon.Warning => Color.Parse("#765F2E"),
            PromptIcon.Question => Color.Parse("#355B70"),
            PromptIcon.Information => Color.Parse("#355B70"),
            _ => Color.Parse("#3C4C52"),
        };
    }
}
