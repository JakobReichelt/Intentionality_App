using System.Windows;
using System.Windows.Controls;

namespace Intentionality.UI.Steps;

static class TextStep
{
    public static UIElement Create(string title, Action<string> onSubmit, string? hint = null)
    {
        var box = Theme.Input(120);
        var panel = new StackPanel();
        var error = ErrorLine();

        panel.Children.Add(Theme.Title(title));
        if (!string.IsNullOrWhiteSpace(hint))
            panel.Children.Add(HintLine(hint));
        panel.Children.Add(new Border { Child = box, Margin = new Thickness(0, 16, 0, 0) });
        panel.Children.Add(error);
        Submit.SetAction(panel, () =>
        {
            if (string.IsNullOrWhiteSpace(box.Text))
            {
                error.Text = "Bitte ausfüllen.";
                error.Visibility = Visibility.Visible;
                return;
            }

            error.Visibility = Visibility.Collapsed;
            onSubmit(box.Text);
        });
        return panel;
    }

    private static TextBlock HintLine(string text) => new()
    {
        Text = text,
        FontSize = 16,
        Foreground = Theme.Accent,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 8, 0, 0),
        Opacity = 0.85
    };

    private static TextBlock ErrorLine() => new()
    {
        Foreground = Theme.Error,
        Margin = new Thickness(0, 8, 0, 0),
        Visibility = Visibility.Collapsed
    };
}
