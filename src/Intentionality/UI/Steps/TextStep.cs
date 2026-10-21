using System.Windows;
using System.Windows.Controls;

namespace Intentionality.UI.Steps;

static class TextStep
{
    public static UIElement Create(string title, Action<string> onSubmit)
    {
        var box = Theme.Input(120);
        var panel = new StackPanel();
        var error = ErrorLine();

        panel.Children.Add(Theme.Title(title));
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

    private static TextBlock ErrorLine() => new()
    {
        Foreground = Theme.Error,
        Margin = new Thickness(0, 8, 0, 0),
        Visibility = Visibility.Collapsed
    };
}
