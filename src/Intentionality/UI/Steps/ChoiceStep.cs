using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Intentionality.UI.Steps;

/// <summary>
/// A "lead + bold goal" prompt above a row of outlined choices.
/// Left/Right move the selection; <paramref name="shortcuts"/> jump to a fixed index.
/// </summary>
static class ChoiceStep
{
    public static UIElement Create(
        string lead,
        string goal,
        IReadOnlyList<string> labels,
        int startSelected,
        IReadOnlyDictionary<Key, int> shortcuts,
        Action<int> onConfirm,
        string? subline = null)
    {
        var cells = labels.Select(OutlineChoiceCells.Create).ToArray();
        var row = OutlineChoiceCells.Row();
        OutlineChoiceCells.AddSpaced(row, cells);

        var selected = startSelected;
        void Select(int index)
        {
            selected = Math.Clamp(index, 0, cells.Length - 1);
            for (var i = 0; i < cells.Length; i++)
                OutlineChoiceCells.SetSelected(cells[i], i == selected);
        }

        Select(startSelected);

        var panel = new StackPanel();
        panel.Children.Add(GoalPrompt(lead, goal));
        if (!string.IsNullOrWhiteSpace(subline))
            panel.Children.Add(Subline(subline));
        panel.Children.Add(row);

        Submit.SetAction(panel, () => onConfirm(selected));
        KeyRouter.WhileLoaded(panel, key =>
        {
            int? next = key switch
            {
                Key.Left => selected - 1,
                Key.Right => selected + 1,
                _ when shortcuts.TryGetValue(key, out var index) => index,
                _ => null
            };
            if (next is not int target)
                return false;

            Select(target);
            return true;
        });
        panel.Loaded += (_, _) => row.Focus();

        return panel;
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
