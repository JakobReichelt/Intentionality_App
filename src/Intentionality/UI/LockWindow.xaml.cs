using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Intentionality.UI.Steps;

namespace Intentionality.UI;

public partial class LockWindow : Window
{
    public bool IsPrimary { get; }
    public string DeviceName { get; private set; } = "";

    bool _forceClose;

    public LockWindow(bool isPrimary)
    {
        InitializeComponent();
        IsPrimary = isPrimary;
        ShowActivated = isPrimary;
        ContentHost.Visibility = isPrimary ? Visibility.Visible : Visibility.Collapsed;
    }

    public void BindMonitor(string deviceName, System.Drawing.Rectangle pixels)
    {
        DeviceName = deviceName;
        _pixels = new PixelRect(pixels.Left, pixels.Top, pixels.Width, pixels.Height);
    }

    public void EnableClamp(bool clamp) => _clamp = clamp;

    public System.Drawing.Rectangle PixelBounds =>
        new(_pixels.X, _pixels.Y, _pixels.Width, _pixels.Height);

    public void PrepareForShow()
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        BackdropImage.BeginAnimation(OpacityProperty, null);
        BackdropImage.Opacity = 1;
        ContentHost.BeginAnimation(OpacityProperty, null);
        ContentHost.Opacity = 1;
        ContentHost.Visibility = IsPrimary ? Visibility.Visible : Visibility.Collapsed;
    }

    public void FadeOut(Action completed)
    {
        var contentFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        contentFade.Completed += (_, _) =>
        {
            ContentHost.BeginAnimation(OpacityProperty, null);
            ContentHost.Opacity = 0;
            ContentHost.Visibility = Visibility.Collapsed;
        };
        ContentHost.BeginAnimation(UIElement.OpacityProperty, contentFade);

        var glassFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(460))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        glassFade.Completed += (_, _) =>
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            BackdropImage.BeginAnimation(OpacityProperty, null);
            BackdropImage.Opacity = 0;
            completed();
        };
        BeginAnimation(OpacityProperty, glassFade);
    }

    public void SetBackdrop(BitmapSource? image)
    {
        BackdropImage.Source = image;
    }

    public void SetStepContent(UIElement? content)
    {
        if (!IsPrimary)
            return;

        StepHost.Content = content;
        var spinner = content is FrameworkElement element && Equals(element.Tag, SpinnerView.Tag);
        if (spinner)
        {
            Grid.SetRow(ContentHost, 0);
            Grid.SetRowSpan(ContentHost, 2);
            ContentHost.VerticalAlignment = VerticalAlignment.Center;
            ContentHost.Margin = new Thickness(0);
        }
        else
        {
            Grid.SetRow(ContentHost, 1);
            Grid.SetRowSpan(ContentHost, 1);
            ContentHost.VerticalAlignment = VerticalAlignment.Top;
            ContentHost.Margin = new Thickness(0, -88, 0, 0);
        }
        if (content is null || !IsVisible)
            return;

        FocusForTyping();
    }

    public void FocusForTyping()
    {
        if (!IsPrimary)
            return;

        Dispatcher.BeginInvoke(FocusForTypingNow, System.Windows.Threading.DispatcherPriority.Input);
    }

    void FocusForTypingNow()
    {
        if (!IsPrimary || !IsVisible)
            return;

        Reassert(activate: true);
        var box = FindChild<TextBox>(StepHost);
        if (box is null)
        {
            Activate();
            Focus();
            Keyboard.Focus(this);
            return;
        }

        box.Focus();
        Keyboard.Focus(box);
        box.CaretIndex = box.Text.Length;
    }

    public void ForceClose()
    {
        _forceClose = true;
        _clamp = false;
        Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WndProc);
        if (source?.CompositionTarget is not null)
            source.CompositionTarget.BackgroundColor = Colors.Transparent;

        InstallToolWindow(helper.Handle);
        Reassert(activate: IsPrimary);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || !IsPrimary)
            return;

        if (e.Key != Key.Return)
            return;

        // A latched Win key must not turn Enter into a rejected shortcut.
        var modifiers = Keyboard.Modifiers & ~ModifierKeys.Windows;
        if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            return;

        if (modifiers != ModifierKeys.None && (modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            return;

        var submit = FindSubmitAction(StepHost);
        if (submit is null)
            return;

        submit();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Only Alt+F4. Do not handle Key.System — that also swallows AltGr characters.
        var altF4 = e.SystemKey == Key.F4
                    || (e.Key == Key.F4 && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt);
        if (altF4)
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClose)
            e.Cancel = true;
        base.OnClosing(e);
    }

    private static Action? FindSubmitAction(DependencyObject parent)
    {
        if (Submit.GetAction(parent) is Action action)
            return action;

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var nested = FindSubmitAction(VisualTreeHelper.GetChild(parent, i));
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;
            var nested = FindChild<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
