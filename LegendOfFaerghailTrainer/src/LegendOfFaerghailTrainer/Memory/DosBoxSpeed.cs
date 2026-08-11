using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LegendOfFaerghailTrainer.Memory;

/// <summary>
/// Slows down or speeds up the emulator by driving DOSBox's own cycle hotkeys
/// (<c>Ctrl+F11</c> / <c>Ctrl+F12</c>) at the attached window.
///
/// <para>Legend of Faerghail has no frame limiter: it redraws and polls the keyboard as fast as the
/// CPU allows, so on a modern host with DOSBox's default <c>cycles=auto</c> the wilderness scrolls
/// past faster than you can steer and the message pages flash by unread. The fix is emulator-side,
/// not game-side — there is nothing in the game's memory to slow down — so the trainer asks DOSBox
/// to do it.</para>
///
/// <para>Every DOSBox family binds these keys the same way, and with the stock
/// <c>cycleup</c>/<c>cycledown</c> of 10 each tap moves the cycle count by 10%. The trainer sends
/// scancodes through SendInput because SDL reads real keyboard input rather than posted window
/// messages, so <c>PostMessage</c> would be ignored. The emulator window must be focusable — this
/// is the one feature that will not work with the game minimised.</para>
/// </summary>
public static class DosBoxSpeed
{
    private const ushort ScanLeftControl = 0x1D;
    private const ushort ScanF11 = 0x57;
    private const ushort ScanF12 = 0x58;

    /// <summary>Taps Ctrl+F11 <paramref name="steps"/> times, each step cutting cycles by about 10%.</summary>
    public static bool Slower(int pid, int steps, out string error) => Adjust(pid, ScanF11, steps, out error);

    /// <summary>Taps Ctrl+F12 <paramref name="steps"/> times, each step raising cycles by about 10%.</summary>
    public static bool Faster(int pid, int steps, out string error) => Adjust(pid, ScanF12, steps, out error);

    private static bool Adjust(int pid, ushort scan, int steps, out string error)
    {
        error = "";
        steps = Math.Clamp(steps, 1, 40);

        IntPtr hwnd;
        try
        {
            using var proc = Process.GetProcessById(pid);
            hwnd = proc.MainWindowHandle;
        }
        catch (Exception ex)
        {
            error = "Couldn't open the emulator process: " + ex.Message;
            return false;
        }

        if (hwnd == IntPtr.Zero)
        {
            error = "The emulator has no visible window to send the cycle hotkey to.";
            return false;
        }

        if (GetForegroundWindow() != hwnd && !Focus(hwnd))
        {
            error = "Couldn't bring the emulator window to the foreground. "
                  + "Click the DOSBox window, then press Ctrl+F11 yourself — the trainer sends the same key.";
            return false;
        }

        // One Ctrl press wrapping the whole burst: DOSBox's mapper reacts to each F11 edge, and
        // holding Ctrl down avoids the modifier racing the function key on a slow SDL event pump.
        if (!Send(KeyDown(ScanLeftControl))) { error = SendBlocked; return false; }
        try
        {
            for (int i = 0; i < steps; i++)
            {
                if (!Send(KeyDown(scan), KeyUp(scan))) { error = SendBlocked; return false; }
                Thread.Sleep(40);
            }
        }
        finally
        {
            Send(KeyUp(ScanLeftControl));
        }
        return true;
    }

    private const string SendBlocked =
        "Windows blocked the synthetic keystroke (SendInput). Keep the emulator window in front and try again.";

    // --- window focus -----------------------------------------------------------

    private static bool Focus(IntPtr hwnd)
    {
        ShowWindow(hwnd, SW_RESTORE);
        uint thisThread = GetCurrentThreadId();
        uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
        bool attached = fgThread != 0 && fgThread != thisThread && AttachThreadInput(thisThread, fgThread, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) AttachThreadInput(thisThread, fgThread, false);
        }

        for (int i = 0; i < 10; i++)
        {
            if (GetForegroundWindow() == hwnd) return true;
            Thread.Sleep(20);
        }
        return false;
    }

    // --- input injection --------------------------------------------------------

    private static bool Send(params INPUT[] inputs) =>
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == (uint)inputs.Length;

    private static INPUT KeyDown(ushort scan) => Key(scan, KEYEVENTF_SCANCODE);
    private static INPUT KeyUp(ushort scan) => Key(scan, KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP);

    private static INPUT Key(ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION
        {
            ki = new KEYBDINPUT { wVk = 0, wScan = scan, dwFlags = flags, time = 0, dwExtraInfo = IntPtr.Zero }
        }
    };

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const int SW_RESTORE = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
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
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    /// <summary>
    /// Win32's INPUT is a tagged union, and the union is sized by its largest member — MOUSEINPUT,
    /// not KEYBDINPUT. Declaring only the keyboard arm produces a 32-byte INPUT on x64 where the
    /// real one is 40, and <c>SendInput</c> rejects any <c>cbSize</c> that is not exactly the
    /// native size: every call would fail and the speed hotkeys would silently never fire.
    /// Overlaying all three arms at offset 0 lets the runtime compute the right size and alignment
    /// on both x64 (40 bytes) and x86 (28), which hand-padding cannot do for both at once.
    /// <see cref="InputStructSize"/> exists so the verification harness can pin this down.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    /// <summary>
    /// The marshalled size of one INPUT record — what is handed to <c>SendInput</c> as
    /// <c>cbSize</c>. Must be 40 on a 64-bit process and 28 on a 32-bit one; anything else means
    /// the union above has lost an arm and every keystroke injection will fail.
    /// </summary>
    public static int InputStructSize => Marshal.SizeOf<INPUT>();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
