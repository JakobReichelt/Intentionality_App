using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Intentionality.UI.Steps;

static class IfThenStep
{
    public static UIElement Create(string title, Action<string, string> onSubmit)
    {
        var distraction = Theme.Input(80);
        var response = Theme.Input(80);
        var panel = new StackPanel();
        var error = ErrorLine();

        panel.Children.Add(Theme.Title(title));
        panel.Children.Add(Label("Wenn …"));
        panel.Children.Add(new Border { Child = distraction, Margin = new Thickness(0, 4, 0, 0) });
        panel.Children.Add(Label("… dann"));
        panel.Children.Add(new Border { Child = response, Margin = new Thickness(0, 4, 0, 0) });
        panel.Children.Add(error);

        Submit.SetAction(panel, () =>
        {
            if (distraction.IsKeyboardFocusWithin)
            {
                if (string.IsNullOrWhiteSpace(distraction.Text))
                {
                    error.Text = "Bitte die Ablenkung ausfüllen.";
                    error.Visibility = Visibility.Visible;
                    return;
                }

                error.Visibility = Visibility.Collapsed;
                response.Focus();
                Keyboard.Focus(response);
                return;
            }

            if (string.IsNullOrWhiteSpace(distraction.Text) || string.IsNullOrWhiteSpace(response.Text))
            {
                error.Text = "Bitte beide Felder ausfüllen.";
                error.Visibility = Visibility.Visible;
                if (string.IsNullOrWhiteSpace(distraction.Text))
                {
                    distraction.Focus();
                    Keyboard.Focus(distraction);
                }
                else
                {
                    response.Focus();
                    Keyboard.Focus(response);
                }
                return;
            }

            error.Visibility = Visibility.Collapsed;
            onSubmit(distraction.Text, response.Text);
        });

        panel.Loaded += (_, _) =>
        {
            distraction.Focus();
            Keyboard.Focus(distraction);
        };

        return panel;
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = 16,
        Foreground = Theme.Accent,
        Margin = new Thickness(0, 16, 0, 0),
        Opacity = 0.9
    };

    private static TextBlock ErrorLine() => new()
    {
        Foreground = Theme.Error,
        Margin = new Thickness(0, 8, 0, 0),
        Visibility = Visibility.Collapsed
    };
}
