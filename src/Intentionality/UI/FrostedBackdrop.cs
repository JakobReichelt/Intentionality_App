using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Intentionality.UI;

/// <summary>
/// Full-screen windows do not get live Acrylic from Windows, so the lock
/// captures each monitor's visible pixels and blurs that snapshot into a milk-glass plate.
/// </summary>
public static class FrostedBackdrop
{
    const uint SrcCopy = 0x00CC0020;
    const uint CaptureBlt = 0x40000000;
    const int EnumCurrentSettings = -1;
    static readonly IntPtr PerMonitorV2 = new(-4);

    /// <summary>
    /// Physical rectangle of the monitor, in virtual-screen pixels.
    /// </summary>
    public static bool TryGetVisibleBounds(string deviceName, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (string.IsNullOrWhiteSpace(deviceName))
            return false;

        Rectangle found = Rectangle.Empty;
        var matched = false;
        MonitorEnumProc callback = (hMonitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { cbSize = Marshal.SizeOf<MonitorInfoEx>() };
            if (!GetMonitorInfo(hMonitor, ref info))
                return true;
            if (!string.Equals(info.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                return true;

            var rect = info.rcMonitor;
            found = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            matched = true;
            return false;
        };

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        if (!matched || found.Width <= 0 || found.Height <= 0)
            return false;

        bounds = found;
        return true;
    }

    /// <summary>
    /// Safe to call off the UI thread. Returns a small blurred bitmap of the monitor.
    /// </summary>
    public static Bitmap? CaptureBitmap(string deviceName, Rectangle fallback)
    {
        // A scaled process reports a smaller size than the panel. Blitting that
        // size copies only the top-left of the framebuffer. Capture at the real mode.
        var previous = SetThreadDpiAwarenessContext(PerMonitorV2);
        try
        {
            var bounds = fallback;
            if (TryGetModePixels(deviceName, out var modeWidth, out var modeHeight))
                bounds = new Rectangle(0, 0, modeWidth, modeHeight);
            else if (TryGetVisibleBounds(deviceName, out var visible))
                bounds = new Rectangle(0, 0, visible.Width, visible.Height);

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            using var screen = CaptureMonitor(deviceName, bounds);
            if (screen is null)
                return null;

            var longEdge = Math.Max(screen.Width, screen.Height);
            const double workingLongEdge = 256;
            var scale = Math.Min(1.0, workingLongEdge / longEdge);
            var width = Math.Max(1, (int)Math.Round(screen.Width * scale));
            var height = Math.Max(1, (int)Math.Round(screen.Height * scale));
            var soft = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(soft))
            {
                ConfigureQuality(graphics);
                graphics.PageUnit = GraphicsUnit.Pixel;
                graphics.DrawImage(screen, 0, 0, width, height);
            }

            ApplySilkyBlur(soft, passes: 4);
            return soft;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (previous != IntPtr.Zero)
                SetThreadDpiAwarenessContext(previous);
        }
    }

    public static BitmapSource? ToBitmapSource(Bitmap? bitmap)
    {
        if (bitmap is null)
            return null;

        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(handle);
            bitmap.Dispose();
        }
    }

    private static bool TryGetModePixels(string deviceName, out int width, out int height)
    {
        width = 0;
        height = 0;
        var mode = new DevMode { dmSize = (short)Marshal.SizeOf<DevMode>() };
        if (!EnumDisplaySettings(deviceName, EnumCurrentSettings, ref mode))
            return false;
        if (mode.dmPelsWidth <= 0 || mode.dmPelsHeight <= 0)
            return false;

        width = mode.dmPelsWidth;
        height = mode.dmPelsHeight;
        return true;
    }

    private static Bitmap? CaptureMonitor(string deviceName, Rectangle bounds)
    {
        var screenDc = CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
            return CopyFromVirtualScreen(bounds);

        var memoryDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, bounds.Width, bounds.Height);
        var previous = SelectObject(memoryDc, bitmap);
        try
        {
            var copied = BitBlt(
                memoryDc,
                0,
                0,
                bounds.Width,
                bounds.Height,
                screenDc,
                0,
                0,
                SrcCopy | CaptureBlt);
            if (!copied)
                return CopyFromVirtualScreen(bounds);

            SelectObject(memoryDc, previous);
            previous = IntPtr.Zero;
            return Image.FromHbitmap(bitmap);
        }
        finally
        {
            if (previous != IntPtr.Zero)
                SelectObject(memoryDc, previous);
            DeleteObject(bitmap);
            DeleteDC(memoryDc);
            DeleteDC(screenDc);
        }
    }

    static void ConfigureQuality(Graphics graphics)
    {
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    /// <summary>
    /// Kawase-style down/up passes: cheap, smooth bloom without harsh pixel blocks.
    /// </summary>
    static void ApplySilkyBlur(Bitmap bitmap, int passes)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        Bitmap work = bitmap;
        for (var pass = 0; pass < passes; pass++)
        {
            var smallWidth = Math.Max(1, work.Width / 2);
            var smallHeight = Math.Max(1, work.Height / 2);
            using var down = new Bitmap(smallWidth, smallHeight, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(down))
            {
                ConfigureQuality(graphics);
                graphics.DrawImage(work, 0, 0, smallWidth, smallHeight);
            }

            if (!ReferenceEquals(work, bitmap))
                work.Dispose();

            var up = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(up))
            {
                ConfigureQuality(graphics);
                graphics.DrawImage(down, 0, 0, width, height);
            }

            work = up;
        }

        if (ReferenceEquals(work, bitmap))
            return;

        using (var graphics = Graphics.FromImage(bitmap))
        {
            ConfigureQuality(graphics);
            graphics.DrawImage(work, 0, 0, width, height);
        }

        work.Dispose();
    }

    private static Bitmap CopyFromVirtualScreen(Rectangle bounds)
    {
        var screen = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(screen);
        graphics.PageUnit = GraphicsUnit.Pixel;
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, screen.Size, CopyPixelOperation.SourceCopy);
        return screen;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int cbSize;
        public NativeRect rcMonitor;
        public NativeRect rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DevMode devMode);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDC(string pwszDriver, string? pwszDevice, string? pszPort, IntPtr pdm);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
