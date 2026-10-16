using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Intentionality.UI;

static class Theme
{
    public static readonly Brush Ink = Freeze(Color.FromRgb(0x1B, 0x24, 0x30));
    public static readonly Brush Accent = Freeze(Color.FromRgb(0x2C, 0x4A, 0x6E));
    public static readonly FontFamily ClockFont = new("Cascadia Mono, Consolas");
    public const double SelectorFontSize = 52;

    public static TextBlock Title(string text) => new()
    {
        Text = text,
        FontSize = 24,
        FontWeight = FontWeights.SemiBold,
        Foreground = Ink,
        TextWrapping = TextWrapping.Wrap
    };

    public static TextBox Input(double minHeight)
    {
        var box = new TextBox
        {
            MinHeight = minHeight,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 8, 0, 0),
            Foreground = Ink,
            CaretBrush = Ink,
            SelectionBrush = Accent,
            FontSize = 18
        };

        var host = new FrameworkElementFactory(typeof(ScrollViewer));
        host.Name = "PART_ContentHost";
        host.SetValue(ScrollViewer.FocusableProperty, false);
        box.Template = new ControlTemplate(typeof(TextBox)) { VisualTree = host };
        return box;
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
