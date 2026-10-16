using System.Windows;

namespace Intentionality.UI;

public static class Submit
{
    public static readonly DependencyProperty ActionProperty = DependencyProperty.RegisterAttached(
        "Action",
        typeof(Action),
        typeof(Submit));

    public static void SetAction(DependencyObject element, Action? value) =>
        element.SetValue(ActionProperty, value);

    public static Action? GetAction(DependencyObject element) =>
        (Action?)element.GetValue(ActionProperty);
}
