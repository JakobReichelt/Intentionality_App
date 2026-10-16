using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Intentionality.UI;

public partial class LockWindow
{
    const int GwlExstyle = -20;
    const int WsExToolwindow = 0x00000080;
    const int WmWindowPosChanging = 0x0046;
    const uint SwpNoSize = 0x0001;
    const uint SwpNoMove = 0x0002;
    const uint SwpNoZOrder = 0x0004;
    const uint SwpNoActivate = 0x0010;
    const uint SwpShowWindow = 0x0040;
    const uint SwpHideWindow = 0x0080;
    const uint SwpFrameChanged = 0x0020;
    const uint SpiGetForegroundLockTimeout = 0x2000;
    const uint SpiSetForegroundLockTimeout = 0x2001;
    const uint SpifSendChange = 0x02;

    PixelRect _pixels;
    PixelRect _applied;
    bool _clamp;
    bool _allowActivate;

    public void Reassert(bool activate)
    {
        _allowActivate = activate;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || _pixels.Width <= 0 || _pixels.Height <= 0)
            return;

        var moved = _applied.Width != _pixels.Width
                    || _applied.Height != _pixels.Height
                    || _applied.X != _pixels.X
                    || _applied.Y != _pixels.Y;

        if (!activate && !moved)
        {
            SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
            return;
        }

        var flags = SwpShowWindow | SwpFrameChanged;
        if (!activate)
            flags |= SwpNoActivate;

        SetWindowPos(hwnd, new IntPtr(-1), _pixels.X, _pixels.Y, _pixels.Width, _pixels.Height, flags);
        _applied = _pixels;

        if (activate)
            ForceForeground(hwnd);
    }

    void ForceForeground(IntPtr hwnd)
    {
        // This process is elevated, so Windows ignores a normal SetForegroundWindow
        // unless the foreground lock is cleared first. Without that, the lock is
        // visible but keystrokes stay in the previous app until the user clicks.
        uint previous = 0;
        SystemParametersInfo(SpiGetForegroundLockTimeout, 0, ref previous, 0);
        SystemParametersInfo(SpiSetForegroundLockTimeout, 0, IntPtr.Zero, SpifSendChange);
        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var currentThread = GetCurrentThreadId();
        var attached = false;
        try
        {
            if (foreground != IntPtr.Zero && foregroundThread != 0 && foregroundThread != currentThread)
                attached = AttachThreadInput(foregroundThread, currentThread, true);

            BringWindowToTop(hwnd);
            Activate();
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
                AttachThreadInput(foregroundThread, currentThread, false);
            SystemParametersInfo(SpiSetForegroundLockTimeout, previous, IntPtr.Zero, SpifSendChange);
        }
    }

    void InstallToolWindow(IntPtr hwnd)
    {
        var style = GetWindowLong(hwnd, GwlExstyle);
        SetWindowLong(hwnd, GwlExstyle, style | WsExToolwindow);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!_clamp || msg != WmWindowPosChanging || _pixels.Width <= 0)
            return IntPtr.Zero;

        var pos = Marshal.PtrToStructure<WindowPos>(lParam);
        if ((pos.flags & SwpHideWindow) != 0)
            return IntPtr.Zero;
        if ((pos.flags & SwpNoMove) != 0 && (pos.flags & SwpNoSize) != 0)
            return IntPtr.Zero;

        pos.x = _pixels.X;
        pos.y = _pixels.Y;
        pos.cx = _pixels.Width;
        pos.cy = _pixels.Height;
        pos.flags &= ~SwpNoSize;
        pos.flags &= ~SwpNoMove;
        pos.hwndInsertAfter = new IntPtr(-1);
        pos.flags &= ~SwpNoZOrder;
        if (!_allowActivate)
            pos.flags |= SwpNoActivate;

        Marshal.StructureToPtr(pos, lParam, fDeleteOld: false);
        return IntPtr.Zero;
    }

    readonly struct PixelRect
    {
        public PixelRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WindowPos
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
