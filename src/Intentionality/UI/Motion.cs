using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Intentionality.UI;

static class Motion
{
    public static void Roll(TextBlock target, string text, int direction)
    {
        if (target.Text == text)
            return;

        target.Text = text;
        if (direction == 0)
            return;

        var shift = new TranslateTransform();
        target.RenderTransform = shift;
        shift.BeginAnimation(
            TranslateTransform.YProperty,
            Slide(direction > 0 ? 14 : -14));
        target.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(180)));
    }

    private static DoubleAnimation Slide(double from) => new(from, 0, TimeSpan.FromMilliseconds(180))
    {
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
    };
}
