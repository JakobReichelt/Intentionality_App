using System.Runtime.InteropServices;
using System.Windows.Forms;
using Intentionality.UI;

namespace Intentionality.Tests;

public class FrostedBackdropTests
{
    static readonly IntPtr PerMonitorV2 = new(-4);

    [DllImport("user32.dll")]
    static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [Fact]
    public void EachMonitorSnapshot_KeepsTheFramebufferAspect()
    {
        var previous = SetThreadDpiAwarenessContext(PerMonitorV2);
        try
        {
            var screens = Screen.AllScreens;
            Assert.NotEmpty(screens);

            foreach (var screen in screens)
            {
                Assert.True(FrostedBackdrop.TryGetVisibleBounds(screen.DeviceName, out var bounds));
                Assert.True(bounds.Width > 0);
                Assert.True(bounds.Height > 0);

                using var bitmap = FrostedBackdrop.CaptureBitmap(screen.DeviceName, bounds);
                Assert.NotNull(bitmap);

                var monitorAspect = bounds.Width / (double)bounds.Height;
                var imageAspect = bitmap!.Width / (double)bitmap.Height;
                Assert.InRange(imageAspect, monitorAspect * 0.98, monitorAspect * 1.02);
            }
        }
        finally
        {
            if (previous != IntPtr.Zero)
                SetThreadDpiAwarenessContext(previous);
        }
    }
}
