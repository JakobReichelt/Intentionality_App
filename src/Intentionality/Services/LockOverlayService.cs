using System.Windows;
using System.Windows.Forms;
using System.Threading;
using System.Windows.Threading;
using Intentionality.UI;
using WpfApp = System.Windows.Application;

namespace Intentionality.Services;

public sealed class LockOverlayService : IDisposable
{
    private readonly List<LockWindow> _windows = new();
    private readonly KeyboardHook _keyboardHook = new();
    private DispatcherTimer? _topmostTimer;
    private bool _isLocked;

    public bool IsLocked => _isLocked;

    public void ShowLock(UIElement? primaryContent)
    {
        EnsureWindows();
        SyncMonitorBounds();
        var hidden = _windows.Where(window => !window.IsVisible).ToList();
        if (hidden.Count > 0)
            ApplyBackdrops(hidden);

        foreach (var window in _windows)
        {
            window.PrepareForShow();
            window.EnableClamp(true);
            if (window.IsPrimary)
                window.SetStepContent(primaryContent);
            if (!window.IsVisible)
                window.Show();
            window.Reassert(activate: window.IsPrimary);
        }

        _windows.FirstOrDefault(window => window.IsPrimary)?.FocusForTyping();
        _keyboardHook.Install();
        StartTopmostEnforcement();
        _isLocked = true;
    }

    private static void ApplyBackdrops(List<LockWindow> windows)
    {
        var shots = windows.Select(window => (window.DeviceName, window.PixelBounds)).ToArray();
        var bitmaps = new System.Drawing.Bitmap?[shots.Length];
        var thread = new Thread(() =>
        {
            for (var i = 0; i < shots.Length; i++)
                bitmaps[i] = FrostedBackdrop.CaptureBitmap(shots[i].DeviceName, shots[i].PixelBounds);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        for (var i = 0; i < windows.Count; i++)
            windows[i].SetBackdrop(FrostedBackdrop.ToBitmapSource(bitmaps[i]));
    }

    public void UpdatePrimaryContent(UIElement content)
    {
        var primary = _windows.FirstOrDefault(w => w.IsPrimary);
        primary?.SetStepContent(content);
    }

    public void FadeOutThen(Action completed)
    {
        StopTopmostEnforcement();
        if (_windows.Count == 0)
        {
            completed();
            return;
        }

        var pending = _windows.Count;
        foreach (var window in _windows)
        {
            window.FadeOut(() =>
            {
                if (Interlocked.Decrement(ref pending) == 0)
                    completed();
            });
        }
    }

    public void HideLock()
    {
        _keyboardHook.Uninstall();
        StopTopmostEnforcement();

        foreach (var window in _windows)
        {
            window.EnableClamp(false);
            window.Hide();
            window.SetStepContent(null);
        }

        _isLocked = false;
    }

    public void Dispose()
    {
        HideLock();
        foreach (var window in _windows.ToList())
        {
            try
            {
                window.ForceClose();
            }
            catch
            {
                // ignore
            }
        }
        _windows.Clear();
        _keyboardHook.Dispose();
    }

    private void EnsureWindows()
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
        {
            if (_windows.Count == 0)
                _windows.Add(new LockWindow(isPrimary: true));
            return;
        }

        if (_windows.Count == screens.Length
            && _windows.Select(w => w.DeviceName).OrderBy(n => n).SequenceEqual(screens.Select(s => s.DeviceName).OrderBy(n => n)))
        {
            SyncMonitorBounds();
            return;
        }

        foreach (var window in _windows.ToList())
            window.ForceClose();
        _windows.Clear();

        foreach (var screen in screens)
        {
            var window = new LockWindow(isPrimary: screen.Primary);
            window.BindMonitor(screen.DeviceName, VisibleBounds(screen));
            _windows.Add(window);
        }
    }

    private void SyncMonitorBounds()
    {
        foreach (var screen in Screen.AllScreens)
        {
            var window = _windows.FirstOrDefault(w => w.DeviceName == screen.DeviceName);
            window?.BindMonitor(screen.DeviceName, VisibleBounds(screen));
        }
    }

    private static System.Drawing.Rectangle VisibleBounds(Screen screen) =>
        FrostedBackdrop.TryGetVisibleBounds(screen.DeviceName, out var bounds)
            ? bounds
            : screen.Bounds;

    private void StartTopmostEnforcement()
    {
        if (_topmostTimer is not null)
            return;

        _topmostTimer = new DispatcherTimer(DispatcherPriority.Background, WpfApp.Current.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _topmostTimer.Tick += (_, _) =>
        {
            if (!_isLocked)
                return;

            GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundProcess);
            if (foregroundProcess == (uint)Environment.ProcessId)
                return;

            SyncMonitorBounds();
            foreach (var window in _windows)
                window.Reassert(activate: false);
        };
        _topmostTimer.Start();
    }

    private void StopTopmostEnforcement()
    {
        if (_topmostTimer is null)
            return;
        _topmostTimer.Stop();
        _topmostTimer = null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
