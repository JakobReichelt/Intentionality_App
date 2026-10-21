using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Intentionality.Core;

namespace Intentionality.Services;

/// <summary>
/// Polls the foreground window for known distractions (YouTube, Reddit, Spotify).
/// Closing: browsers get Ctrl+W on the active tab; Spotify is closed then killed.
/// </summary>
public sealed class DistractionGuard
{
    public string? DetectForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0 || processId == (uint)Environment.ProcessId)
            return null;

        string? processName = null;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        var title = GetWindowTitle(hwnd);
        return DistractionRules.Match(processName, title);
    }

    public void CloseOffending(string ruleId)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return;

        GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
            return;

        Process? process = null;
        try
        {
            process = Process.GetProcessById((int)processId);
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        using (process)
        {
            if (string.Equals(ruleId, DistractionRules.Spotify, StringComparison.OrdinalIgnoreCase)
                || process.ProcessName.Equals("Spotify", StringComparison.OrdinalIgnoreCase))
            {
                CloseSpotify(process);
                return;
            }

            CloseBrowserTab(hwnd);
        }
    }

    private static void CloseBrowserTab(IntPtr hwnd)
    {
        SetForegroundWindow(hwnd);
        // Small delay so focus settles before SendInput.
        Thread.Sleep(50);
        SendCtrlW();
    }

    private static void CloseSpotify(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.CloseMainWindow();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (process.WaitForExit(2000))
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Access denied or already exiting.
        }
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
            return "";

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static void SendCtrlW()
    {
        var inputs = new INPUT[4];
        inputs[0] = KeyDown(VirtualKey.VK_CONTROL);
        inputs[1] = KeyDown(VirtualKey.VK_W);
        inputs[2] = KeyUp(VirtualKey.VK_W);
        inputs[3] = KeyUp(VirtualKey.VK_CONTROL);
        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyDown(VirtualKey key) => new()
    {
        type = InputType.INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)key,
                dwFlags = 0
            }
        }
    };

    private static INPUT KeyUp(VirtualKey key) => new()
    {
        type = InputType.INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)key,
                dwFlags = KeyEventF.KEYEVENTF_KEYUP
            }
        }
    };

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private enum InputType : uint
    {
        INPUT_KEYBOARD = 1
    }

    private enum VirtualKey : ushort
    {
        VK_CONTROL = 0x11,
        VK_W = 0x57
    }

    [Flags]
    private enum KeyEventF : uint
    {
        KEYEVENTF_KEYUP = 0x0002
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public InputType type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public KeyEventF dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
