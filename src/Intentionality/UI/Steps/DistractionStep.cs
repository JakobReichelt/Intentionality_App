using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Intentionality.UI.Steps;

static class DistractionStep
{
    const int Yes = 0;
    const int No = 1;
    const double LockedOpacity = 0.35;
    static readonly TimeSpan YesDelay = TimeSpan.FromSeconds(10);

    public static UIElement Create(
        string lead,
        string goal,
        string distractionLabel,
        Action<bool> onConfirm)
    {
        var yesCell = OutlineChoiceCells.Create("Ja");
        var noCell = OutlineChoiceCells.Create("Nein");
        var cells = new[] { yesCell, noCell };
        var row = OutlineChoiceCells.Row();
        OutlineChoiceCells.AddSpaced(row, cells);

        var yesReady = false;
        var selected = No;

        var progress = ProgressOutline();
        yesCell.Children.Add(progress);
        SetLockedLook(yesCell, locked: true);

        void Select(int index)
        {
            if (index == Yes && !yesReady)
                index = No;

            selected = Math.Clamp(index, 0, cells.Length - 1);
            for (var i = 0; i < cells.Length; i++)
                OutlineChoiceCells.SetSelected(cells[i], i == selected);
        }

        Select(No);

        var panel = new StackPanel();
        panel.Children.Add(GoalPrompt(lead, goal));
        panel.Children.Add(Subline(distractionLabel));
        panel.Children.Add(row);

        Submit.SetAction(panel, () =>
        {
            if (selected == Yes && !yesReady)
                return;
            onConfirm(selected == Yes);
        });

        KeyRouter.WhileLoaded(panel, key =>
        {
            int? next = key switch
            {
                Key.Left => selected - 1,
                Key.Right => selected + 1,
                Key.J when yesReady => Yes,
                Key.L => No,
                _ => null
            };
            if (next is not int target)
                return false;

            Select(target);
            return true;
        });

        panel.Loaded += (_, _) =>
        {
            row.Focus();
            StartProgress(progress, () =>
            {
                yesReady = true;
                yesCell.Children.Remove(progress);
                SetLockedLook(yesCell, locked: false);
                Select(selected);
            });
        };
        panel.Unloaded += (_, _) => progress.BeginAnimation(Shape.StrokeDashOffsetProperty, null);

        return panel;
    }

    private static Rectangle ProgressOutline() => new()
    {
        RadiusX = OutlineChoiceCells.CornerRadius,
        RadiusY = OutlineChoiceCells.CornerRadius,
        Stroke = Theme.Ink,
        Fill = Brushes.Transparent,
        StrokeThickness = OutlineChoiceCells.NormalStroke,
        StrokeDashCap = PenLineCap.Flat,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        Margin = new Thickness(OutlineChoiceCells.SelectedStroke / 2),
        SnapsToDevicePixels = true,
        IsHitTestVisible = false,
        Visibility = Visibility.Hidden
    };

    private static void StartProgress(Rectangle progress, Action completed)
    {
        progress.UpdateLayout();
        var thickness = progress.StrokeThickness;
        var width = Math.Max(0, progress.ActualWidth - thickness);
        var height = Math.Max(0, progress.ActualHeight - thickness);
        var radius = Math.Min(OutlineChoiceCells.CornerRadius, Math.Min(width, height) / 2);
        var perimeter = 2 * (width - 2 * radius) + 2 * (height - 2 * radius) + 2 * Math.PI * radius;

        // Dash lengths are in multiples of the stroke thickness.
        var length = perimeter / thickness + 1;
        progress.StrokeDashArray = new DoubleCollection { length, length };
        progress.StrokeDashOffset = length;
        progress.Visibility = Visibility.Visible;

        var draw = new DoubleAnimation(length, 0, new Duration(YesDelay)) { FillBehavior = FillBehavior.HoldEnd };
        draw.Completed += (_, _) => completed();
        progress.BeginAnimation(Shape.StrokeDashOffsetProperty, draw);
    }

    private static void SetLockedLook(Grid cell, bool locked)
    {
        var opacity = locked ? LockedOpacity : 1;
        foreach (UIElement child in cell.Children)
        {
            if (child is Rectangle outline && ReferenceEquals(outline, cell.Tag))
                outline.Opacity = opacity;
            else if (child is TextBlock text)
                text.Opacity = opacity;
        }
    }

    private static TextBlock GoalPrompt(string lead, string goal)
    {
        var prompt = new TextBlock
        {
            FontSize = Theme.StepFontSize,
            Foreground = Theme.Ink,
            TextWrapping = TextWrapping.Wrap
        };
        prompt.Inlines.Add(new Run(lead));
        prompt.Inlines.Add(new Run(goal) { FontWeight = FontWeights.Bold });
        return prompt;
    }

    private static TextBlock Subline(string text) => new()
    {
        Text = text,
        FontSize = 16,
        Foreground = Theme.Accent,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 8, 0, 0),
        Opacity = 0.85
    };
}
