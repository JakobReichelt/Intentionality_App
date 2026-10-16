using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Intentionality.UI.Steps;

static class SpinnerView
{
    public const string Tag = "spinner";

    public static UIElement Create()
    {
        var ring = new Ellipse
        {
            Width = 36,
            Height = 36,
            Stroke = Theme.Accent,
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 1, 1.35 },
            RenderTransformOrigin = new Point(0.5, 0.5),
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = Tag
        };

        var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(700))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        Timeline.SetDesiredFrameRate(spin, 60);
        ring.RenderTransform = new RotateTransform();
        ring.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, spin);
        return ring;
    }
}
