using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Intentionality.UI.Steps;

static class DurationStep
{
    const int MinimumMinutes = 10;
    const int MaximumMinutes = 8 * 60;

    public static UIElement Create(Action<TimeSpan> onSubmit, string title = "Wie viel Zeit brauchst du?")
    {
        var minutes = 10;
        var ready = false;
        var hoursDigit = Digit();
        var minutesDigit = Digit();
        var panel = new StackPanel();
        panel.Children.Add(Theme.Title(title));

        var clock = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 18, 0, 0),
            Focusable = true,
            FocusVisualStyle = null
        };
        clock.Children.Add(Slot(hoursDigit));
        clock.Children.Add(Colon());
        clock.Children.Add(Slot(minutesDigit));

        void Show(int direction)
        {
            Motion.Roll(hoursDigit, (minutes / 60).ToString("00"), ready ? direction : 0);
            Motion.Roll(minutesDigit, (minutes % 60).ToString("00"), ready ? direction : 0);
        }

        void Nudge(int delta)
        {
            var next = Math.Clamp(minutes + delta, MinimumMinutes, MaximumMinutes);
            if (next == minutes)
                return;
            var direction = next > minutes ? 1 : -1;
            minutes = next;
            Show(direction);
        }

        Show(0);
        ready = true;
        Submit.SetAction(panel, () => onSubmit(TimeSpan.FromMinutes(minutes)));
        KeyRouter.WhileLoaded(panel, key =>
        {
            var delta = key switch
            {
                Key.Up => 10,
                Key.I => 5,
                Key.Down => -10,
                Key.K => -5,
                Key.Right or Key.L => 60,
                Key.Left or Key.J => -60,
                _ => 0
            };
            if (delta == 0)
                return false;
            Nudge(delta);
            return true;
        });
        panel.Loaded += (_, _) => clock.Focus();
        panel.Children.Add(clock);
        return panel;
    }

    private static TextBlock Digit() => new()
    {
        FontFamily = Theme.ClockFont,
        FontSize = Theme.TimerFontSize,
        FontWeight = FontWeights.Bold,
        Foreground = Theme.Ink,
        Text = "00",
        RenderTransformOrigin = new Point(0, 0.5)
    };

    private static Border Slot(TextBlock digit) => new()
    {
        ClipToBounds = true,
        Height = 64,
        Child = digit,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static TextBlock Colon() => new()
    {
        Text = ":",
        FontFamily = Theme.ClockFont,
        FontSize = Theme.TimerColonFontSize,
        FontWeight = FontWeights.Bold,
        Foreground = Theme.Ink,
        Margin = new Thickness(2, 0, 2, 6),
        VerticalAlignment = VerticalAlignment.Center
    };
}
