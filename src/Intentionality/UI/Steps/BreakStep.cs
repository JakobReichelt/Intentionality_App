using System.Windows;
using System.Windows.Controls;

namespace Intentionality.UI.Steps;

static class BreakStep
{
    public static UIElement Create(Func<TimeSpan?> remaining)
    {
        var countdown = new TextBlock
        {
            Text = "05:00",
            FontFamily = Theme.ClockFont,
            FontSize = Theme.SelectorFontSize,
            FontWeight = FontWeights.Light,
            Foreground = Theme.Accent,
            Margin = new Thickness(0, 18, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var panel = new StackPanel();
        panel.Children.Add(Theme.Title("Pause"));
        panel.Children.Add(countdown);

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            var left = remaining() ?? TimeSpan.Zero;
            if (left < TimeSpan.Zero)
                left = TimeSpan.Zero;
            countdown.Text = $"{left.Minutes:D2}:{left.Seconds:D2}";
        };
        timer.Start();
        panel.Unloaded += (_, _) => timer.Stop();
        return panel;
    }
}
