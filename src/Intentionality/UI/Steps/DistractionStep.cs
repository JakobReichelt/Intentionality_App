using System.Windows;
using System.Windows.Input;

namespace Intentionality.UI.Steps;

static class DistractionStep
{
    const int Yes = 0;
    const int No = 1;

    static readonly string[] Labels = ["Ja", "Nein"];
    static readonly Dictionary<Key, int> Shortcuts = new() { [Key.J] = Yes, [Key.L] = No };

    public static UIElement Create(
        string lead,
        string goal,
        string distractionLabel,
        Action<bool> onConfirm)
    {
        var subline = distractionLabel;
        return ChoiceStep.Create(
            lead,
            goal,
            Labels,
            startSelected: No,
            Shortcuts,
            index => onConfirm(index == Yes),
            subline);
    }
}
