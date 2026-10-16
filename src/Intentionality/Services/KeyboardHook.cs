using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Intentionality.Services;

/// <summary>
/// Low-level keyboard hook active only while the session is locked.
/// Blocks Win, Alt+Tab, Alt+Esc, Alt+F4, Alt+Space, Ctrl+Esc, Ctrl+Shift+Esc.
/// Ctrl+Alt+Del cannot be intercepted by design.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmSyskeydown = 0x0104;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeyup = 0x0105;

    private const int LlkhfAltDown = 0x20;
    private const int LlkhfInjected = 0x10;
    private const int LlkhfLowerIlInjected = 0x02;
    private const uint KeyeventfExtendedKey = 0x0001;
    private const uint KeyeventfKeyup = 0x0002;
    private const int VkControl = 0x11;
    private const int VkTab = 0x09;
    private const int VkEscape = 0x1B;
    private const int VkSpace = 0x20;
    private const int VkLwin = 0x5B;
    private const int VkRwin = 0x5C;
    private const int VkF4 = 0x73;

    private LowLevelKeyboardProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _enabled;

    public void Install()
    {
        if (_hookId != IntPtr.Zero)
            return;

        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(curModule.ModuleName), 0);
        _enabled = _hookId != IntPtr.Zero;
        if (_enabled)
            ReleaseWinKeys();
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero)
            return;

        ReleaseWinKeys();
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _enabled = false;
    }

    public void Dispose() => Uninstall();

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _enabled)
        {
            var msg = wParam.ToInt32();
            if (msg is WmKeydown or WmSyskeydown or WmKeyup or WmSyskeyup)
            {
                var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                if (ShouldBlock(info))
                    return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool ShouldBlock(KbdLlHookStruct info)
    {
        var suppress = ShouldSuppress(info.VkCode, info.Flags, (GetAsyncKeyState(VkControl) & 0x8000) != 0);
        if (suppress && info.VkCode is VkLwin or VkRwin)
            ReleaseWinKey((int)info.VkCode);
        return suppress;
    }

    /// <summary>
    /// Swallowing a Win key-up leaves the shell modifier latched, so the next key is a
    /// shortcut (Win+Enter, Win+K). Injected events must pass so the forced key-up lands.
    /// </summary>
    internal static bool ShouldSuppress(uint vkCode, uint flags, bool controlDown)
    {
        if ((flags & (LlkhfInjected | LlkhfLowerIlInjected)) != 0)
            return false;

        if (vkCode is VkLwin or VkRwin)
            return true;

        // Use the hook's own Alt flag. GetAsyncKeyState(VK_MENU) stays true for AltGr
        // on a German keyboard and would treat normal typing as a shortcut.
        var alt = (flags & LlkhfAltDown) != 0;
        if (alt && vkCode is VkTab or VkEscape or VkF4 or VkSpace)
            return true;

        if (vkCode == VkEscape && controlDown)
            return true;

        return false;
    }

    private static void ReleaseWinKeys()
    {
        ReleaseWinKey(VkLwin);
        ReleaseWinKey(VkRwin);
    }

    private static void ReleaseWinKey(int vk)
    {
        // Ctrl held across the Win key-up keeps Windows from treating the release as a Start-menu tap.
        keybd_event((byte)VkControl, 0, 0, UIntPtr.Zero);
        keybd_event((byte)vk, 0, KeyeventfExtendedKey | KeyeventfKeyup, UIntPtr.Zero);
        keybd_event((byte)VkControl, 0, KeyeventfKeyup, UIntPtr.Zero);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
