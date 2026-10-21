using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Intentionality.Core;

namespace Intentionality.Services;

public sealed record DistractionHit(string RuleId, IntPtr Hwnd, int ProcessId);

/// <summary>
/// Polls the foreground window for known distractions (YouTube, Reddit, Spotify).
/// Closing uses the window captured at detection time (not the current foreground),
/// because the lock overlay is usually in front when the user answers.
/// </summary>
public sealed class DistractionGuard
{
    public DistractionHit? DetectForeground()
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
        var ruleId = DistractionRules.Match(processName, title);
        return ruleId is null ? null : new DistractionHit(ruleId, hwnd, (int)processId);
    }

    public void Close(DistractionHit hit)
    {
        if (string.Equals(hit.RuleId, DistractionRules.Spotify, StringComparison.OrdinalIgnoreCase))
        {
            CloseSpotify(hit.ProcessId);
            return;
        }

        CloseBrowserTab(hit.Hwnd, hit.ProcessId);
    }

    private static void CloseBrowserTab(IntPtr hwnd, int processId)
    {
        // Prefer the live main window if the captured hwnd went stale.
        var target = hwnd != IntPtr.Zero && IsWindow(hwnd)
            ? hwnd
            : FindMainWindow(processId);

        if (target == IntPtr.Zero || !IsWindow(target))
            return;

        ForceForeground(target);
        Thread.Sleep(150);
        SendCtrlW();
        Thread.Sleep(250);
    }

    private static IntPtr FindMainWindow(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.MainWindowHandle;
        }
        catch (ArgumentException)
        {
            return IntPtr.Zero;
        }
        catch (InvalidOperationException)
        {
            return IntPtr.Zero;
        }
    }

    private static void ForceForeground(IntPtr hwnd)
    {
        // Allow ourselves to steal focus back to the browser after the lock hides.
        AllowSetForegroundWindow(UncheckedAsfwAny);

        ShowWindow(hwnd, SwRestore);

        var foreground = GetForegroundWindow();
        var currentThread = GetCurrentThreadId();
        var foregroundThread = foreground == IntPtr.Zero
            ? 0u
            : GetWindowThreadProcessId(foreground, out _);
        var targetThread = GetWindowThreadProcessId(hwnd, out _);

        var attachedForeground = false;
        var attachedTarget = false;
        if (foregroundThread != 0 && foregroundThread != currentThread)
            attachedForeground = AttachThreadInput(currentThread, foregroundThread, true);
        if (targetThread != 0 && targetThread != currentThread)
            attachedTarget = AttachThreadInput(currentThread, targetThread, true);

        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);

        if (attachedTarget)
            AttachThreadInput(currentThread, targetThread, false);
        if (attachedForeground)
            AttachThreadInput(currentThread, foregroundThread, false);
    }

    private static void CloseSpotify(int processId)
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            process = Process.GetProcessesByName("Spotify").FirstOrDefault();
        }

        if (process is null)
            return;

        using (process)
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

            if (process.WaitForExit(1500))
                return;

            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
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
        // Scan-code path is more reliable across browsers than virtual-key-only input.
        var ctrl = (ushort)MapVirtualKey((uint)VirtualKey.VK_CONTROL, 0);
        var w = (ushort)MapVirtualKey((uint)VirtualKey.VK_W, 0);

        var inputs = new INPUT[4];
        inputs[0] = Key(ctrl, KeyEventF.KEYEVENTF_SCANCODE);
        inputs[1] = Key(w, KeyEventF.KEYEVENTF_SCANCODE);
        inputs[2] = Key(w, KeyEventF.KEYEVENTF_SCANCODE | KeyEventF.KEYEVENTF_KEYUP);
        inputs[3] = Key(ctrl, KeyEventF.KEYEVENTF_SCANCODE | KeyEventF.KEYEVENTF_KEYUP);

        var size = Marshal.SizeOf<INPUT>();
        var sent = SendInput((uint)inputs.Length, inputs, size);
        if (sent == inputs.Length)
            return;

        // Fallback: virtual-key codes.
        inputs[0] = Vk(VirtualKey.VK_CONTROL, down: true);
        inputs[1] = Vk(VirtualKey.VK_W, down: true);
        inputs[2] = Vk(VirtualKey.VK_W, down: false);
        inputs[3] = Vk(VirtualKey.VK_CONTROL, down: false);
        _ = SendInput((uint)inputs.Length, inputs, size);
    }

    private static INPUT Key(ushort scan, KeyEventF flags) => new()
    {
        type = InputType.INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = scan,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        }
    };

    private static INPUT Vk(VirtualKey key, bool down) => new()
    {
        type = InputType.INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)key,
                wScan = 0,
                dwFlags = down ? 0 : KeyEventF.KEYEVENTF_KEYUP,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        }
    };

    private const int SwRestore = 9;
    private const uint UncheckedAsfwAny = 0xFFFFFFFF;

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

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

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
        KEYEVENTF_KEYUP = 0x0002,
        KEYEVENTF_SCANCODE = 0x0008
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public InputType type;
        public InputUnion U;
    }

    // Union must be sized like MOUSEINPUT or SendInput fails on 64-bit.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }
}
