using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Intentionality.Services;

static class DesktopGate
{
    const uint DesktopSwitchDesktop = 0x0100;

    public static async Task WaitForHomeScreenAsync()
    {
        if (!IsLockScreenUp())
            return;

        var unlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SessionSwitchEventHandler handler = (_, e) =>
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock)
                unlocked.TrySetResult();
        };

        SystemEvents.SessionSwitch += handler;
        try
        {
            if (IsLockScreenUp())
                await unlocked.Task;
        }
        finally
        {
            SystemEvents.SessionSwitch -= handler;
        }

        await Task.Delay(1200);
    }

    static bool IsLockScreenUp()
    {
        if (ForegroundIsLockApp())
            return true;

        var desktop = OpenInputDesktop(0, false, DesktopSwitchDesktop);
        if (desktop != IntPtr.Zero)
        {
            CloseDesktop(desktop);
            return false;
        }

        return Marshal.GetLastWin32Error() != 5;
    }

    static bool ForegroundIsLockApp()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        GetWindowThreadProcessId(hwnd, out var processId);
        try
        {
            return Process.GetProcessById((int)processId).ProcessName
                .Equals("LockApp", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint desiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool CloseDesktop(IntPtr desktop);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
}
