using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Intentionality.UI.Steps;

static class OptionStep
{
    public static UIElement Create(string title, string? detail, IReadOnlyList<string> options, int start, Action<int> onConfirm)
    {
        var selected = start;
        var choice = new TextBlock
        {
            FontSize = Theme.SelectorFontSize,
            FontWeight = FontWeights.Light,
            Foreground = Theme.Ink,
            HorizontalAlignment = HorizontalAlignment.Left,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 18, 0, 0),
            RenderTransformOrigin = new Point(0, 0.5)
        };

        var panel = new StackPanel();
        panel.Children.Add(Theme.Title(title));
        if (!string.IsNullOrWhiteSpace(detail))
        {
            panel.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = 16,
                Foreground = Theme.Ink,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 16, 0, 0)
            });
        }

        void Show(int direction) => Motion.Roll(choice, options[selected], direction);

        Show(0);
        Submit.SetAction(panel, () => onConfirm(selected));
        KeyRouter.WhileLoaded(panel, key =>
        {
            var delta = key switch
            {
                Key.Up or Key.I => -1,
                Key.Down or Key.K => 1,
                _ => 0
            };
            if (delta == 0)
                return false;
            selected = (selected + delta + options.Count) % options.Count;
            Show(delta);
            return true;
        });

        panel.Children.Add(choice);
        return panel;
    }
}
