using System.Windows;
using System.Windows.Input;

namespace Intentionality.UI;

static class KeyRouter
{
    public static void WhileLoaded(FrameworkElement panel, Func<Key, bool> handle)
    {
        Window? host = null;
        KeyEventHandler onKey = (_, e) =>
        {
            if (handle(e.Key))
                e.Handled = true;
        };

        panel.Loaded += (_, _) =>
        {
            host = Window.GetWindow(panel);
            host?.AddHandler(UIElement.PreviewKeyDownEvent, onKey, handledEventsToo: true);
        };
        panel.Unloaded += (_, _) => host?.RemoveHandler(UIElement.PreviewKeyDownEvent, onKey);
    }
}
