using System.Windows;
using System.Windows.Controls;

namespace Intentionality.UI.Steps;

static class BreakStep
{
    public static UIElement Create(Func<TimeSpan?> remaining)
    {
        var countdown = Theme.TimerDisplay(Theme.Accent);
        countdown.FontFamily = Theme.ClockFont;
        countdown.Margin = new Thickness(0, 18, 0, 0);

        void RefreshCountdown()
        {
            var left = remaining() ?? TimeSpan.Zero;
            if (left < TimeSpan.Zero)
                left = TimeSpan.Zero;
            countdown.Text = FormatCountdown(left);
        }

        var panel = new StackPanel();
        panel.Children.Add(Theme.Title("Pause"));
        panel.Children.Add(countdown);

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) => RefreshCountdown();
        panel.Loaded += (_, _) => RefreshCountdown();
        timer.Start();
        panel.Unloaded += (_, _) => timer.Stop();
        return panel;
    }

    static string FormatCountdown(TimeSpan left) =>
        $"{(int)left.TotalMinutes:D2}:{left.Seconds:D2}";
}
