using System.Windows;
using System.Windows.Input;

namespace Intentionality.UI.Steps;

static class CompletionStep
{
    const int Yes = 0;
    const int No = 1;

    static readonly string[] Labels = ["Ja", "Nein"];
    static readonly Dictionary<Key, int> Shortcuts = new() { [Key.J] = Yes, [Key.L] = No };

    public static UIElement Create(
        string lead,
        string goal,
        Action<bool> onConfirm,
        string? doneCriterion = null)
    {
        var subline = string.IsNullOrWhiteSpace(doneCriterion)
            ? null
            : $"Fertig, wenn: {doneCriterion}";
        return ChoiceStep.Create(lead, goal, Labels, startSelected: No, Shortcuts, index => onConfirm(index == Yes), subline);
    }
}
