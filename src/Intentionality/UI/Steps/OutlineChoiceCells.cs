using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Intentionality.UI.Steps;

static class OutlineChoiceCells
{
    public const double NormalStroke = 2.25;
    public const double SelectedStroke = 5;
    public const double CornerRadius = 10;
    public const double Gap = 12;
    const double CellWidth = 138;
    const double CellHeight = 64;
    const double OutlineInset = SelectedStroke / 2;

    public static Grid Create(string label)
    {
        var text = Theme.ChoiceAnswer();
        text.Text = label;
        text.TextWrapping = TextWrapping.NoWrap;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextAlignment = TextAlignment.Center;
        text.Padding = new Thickness(0, 0, 0, 2);
        text.IsHitTestVisible = false;

        var outline = new Rectangle
        {
            RadiusX = CornerRadius,
            RadiusY = CornerRadius,
            Stroke = Theme.Ink,
            Fill = Brushes.Transparent,
            StrokeThickness = NormalStroke,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(OutlineInset),
            SnapsToDevicePixels = true,
            IsHitTestVisible = false
        };

        var host = new Grid
        {
            Width = CellWidth,
            Height = CellHeight,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            ClipToBounds = false,
            Tag = outline
        };
        host.Children.Add(outline);
        host.Children.Add(text);
        Panel.SetZIndex(text, 1);

        return host;
    }

    public static void SetSelected(Grid cell, bool selected)
    {
        var outline = (Rectangle)cell.Tag;
        outline.StrokeThickness = selected ? SelectedStroke : NormalStroke;
    }

    public static StackPanel Row()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 24, 0, 0),
            Focusable = true,
            FocusVisualStyle = null
        };
    }

    public static void AddSpaced(StackPanel row, params Grid[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0)
                row.Children.Add(new Border { Width = Gap });
            row.Children.Add(cells[i]);
        }
    }
}
