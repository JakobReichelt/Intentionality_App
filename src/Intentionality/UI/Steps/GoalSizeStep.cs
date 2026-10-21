using System.Windows;
using System.Windows.Input;
using Intentionality.Core;

namespace Intentionality.UI.Steps;

static class GoalSizeStep
{
    static readonly AmbitionLevel[] Levels =
    [
        AmbitionLevel.SlightlyTooEasy,
        AmbitionLevel.AboutRight,
        AmbitionLevel.ABitTooAmbitious
    ];

    static readonly string[] Labels = Levels.Select(AmbitionLevelLabels.ToDisplay).ToArray();
    static readonly Dictionary<Key, int> Shortcuts = new() { [Key.J] = 0, [Key.K] = 1, [Key.L] = 2 };
    static readonly int DefaultSelection = Array.IndexOf(Levels, AmbitionLevel.AboutRight);

    public static UIElement Create(string goal, Action<AmbitionLevel> onConfirm) =>
        ChoiceStep.Create("Wie war: ", goal, Labels, DefaultSelection, Shortcuts, index => onConfirm(Levels[index]));
}
