using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using static GameTrainers.Common.Memory.NativeMethods;

namespace GameTrainers.Common.Memory;

/// <summary>
/// Focuses a target process's main window and replays a short key sequence into it
/// using SendInput (scancode-based), so a clunky in-game menu walk — e.g. the
/// Might &amp; Magic spell-cast sequence "5 c 1 6 Enter" — becomes one click.
///
/// DOSBox and other SDL emulators read real hardware keyboard input rather than
/// posted window messages, so PostMessage doesn't work; SendInput (which injects at
/// the OS input layer and is delivered to the focused window) does.
/// </summary>
public static class KeyboardSender
{
    /// <summary>
    /// Brings <paramref name="pid"/>'s main window to the foreground and sends the
    /// parsed <paramref name="sequence"/>. Returns false (with a reason) if the
    /// window can't be found/focused.
    ///
    /// Sequence syntax: literal characters are sent as keystrokes; whitespace
    /// between them is ignored (use <c>{SPACE}</c> for an actual space). Tokens in
    /// braces: <c>{ENTER} {ESC} {SPACE} {TAB} {UP} {DOWN} {LEFT} {RIGHT}</c> and
    /// <c>{DELAY:ms}</c> to pause mid-sequence.
    /// </summary>
    public static bool Send(int pid, string sequence, int keyDelayMs, int focusDelayMs, out string error)
    {
        error = "";
        IntPtr hwnd;
        try
        {
            using var proc = Process.GetProcessById(pid);
            hwnd = proc.MainWindowHandle;
        }
        catch (Exception ex)
        {
            error = "Couldn't open the game process: " + ex.Message;
            return false;
        }

        if (hwnd == IntPtr.Zero)
        {
            error = "The game has no visible main window to send keys to.";
            return false;
        }

        // If the game is already the foreground window — the usual case inside a tight
        // re-roll loop that brought it forward on the previous tap — skip the focus
        // ceremony and its settle delay entirely. Re-activating the already-active window
        // just burns focusDelayMs every iteration for no benefit.
        if (GetForegroundWindow() != hwnd)
        {
            if (!Focus(hwnd))
            {
                error = "Couldn't bring the game window to the foreground.";
                return false;
            }

            Thread.Sleep(Math.Clamp(focusDelayMs, 0, 2000));
        }

        var tokens = Parse(sequence, out var parseError);
        if (parseError != null) { error = parseError; return false; }

        foreach (var t in tokens)
        {
            if (t.IsDelay) { Thread.Sleep(Math.Clamp(t.DelayMs, 0, 5000)); continue; }
            if (!TapScan(t.Scan, t.Extended, t.Shift))
            {
                error = "Windows blocked the synthetic keystroke (SendInput). "
                      + "The game window may have lost focus — try again and keep it in front.";
                return false;
            }
            Thread.Sleep(Math.Clamp(keyDelayMs, 0, 2000));
        }
        return true;
    }

    /// <summary>Validates a sequence without sending; null return = OK.</summary>
    public static string? Validate(string sequence)
    {
        Parse(sequence, out var err);
        return err;
    }

    /// <summary>
    /// Brings <paramref name="pid"/>'s main window to the foreground without sending any
    /// keys — used to surface the game so the user can act (e.g. pick a class once the
    /// roller stops on a good roll). Returns false if the window can't be found/focused.
    /// </summary>
    public static bool BringToFront(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            var hwnd = proc.MainWindowHandle;
            return hwnd != IntPtr.Zero && Focus(hwnd);
        }
        catch
        {
            return false;
        }
    }

    // --- window focus -----------------------------------------------------------
    private static bool Focus(IntPtr hwnd)
    {
        ShowWindow(hwnd, SW_RESTORE);

        // Foreground-lock workaround: attach our input thread to the current
        // foreground thread so SetForegroundWindow is honoured.
        uint thisThread = GetCurrentThreadId();
        uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);

        bool attached = fgThread != 0 && fgThread != thisThread
                        && AttachThreadInput(thisThread, fgThread, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) AttachThreadInput(thisThread, fgThread, false);
        }

        // The foreground switch can lag a few ms; poll briefly before giving up.
        for (int i = 0; i < 10; i++)
        {
            if (GetForegroundWindow() == hwnd) return true;
            Thread.Sleep(20);
        }
        return false;
    }

    // --- key injection ----------------------------------------------------------
    /// <summary>Injects one key tap; returns false if SendInput didn't deliver every event.</summary>
    private static bool TapScan(ushort scan, bool extended, bool shift)
    {
        const ushort vkShift = 0x10;
        ushort shiftScan = (ushort)MapVirtualKey(vkShift, MAPVK_VK_TO_VSC);

        var inputs = new List<INPUT>(6);
        if (shift) inputs.Add(KeyInput(shiftScan, false, false));
        inputs.Add(KeyInput(scan, false, extended));
        inputs.Add(KeyInput(scan, true, extended));
        if (shift) inputs.Add(KeyInput(shiftScan, true, false));

        var arr = inputs.ToArray();
        uint sent = SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
        return sent == (uint)arr.Length;
    }

    private static INPUT KeyInput(ushort scan, bool up, bool extended)
    {
        uint flags = KEYEVENTF_SCANCODE;
        if (up) flags |= KEYEVENTF_KEYUP;
        if (extended) flags |= KEYEVENTF_EXTENDEDKEY;
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT { wVk = 0, wScan = scan, dwFlags = flags, time = 0, dwExtraInfo = IntPtr.Zero }
            }
        };
    }

    // --- parsing ----------------------------------------------------------------
    private readonly record struct Key(ushort Scan, bool Extended, bool Shift, bool IsDelay, int DelayMs);

    private static List<Key> Parse(string sequence, out string? error)
    {
        error = null;
        var keys = new List<Key>();
        sequence ??= "";

        for (int i = 0; i < sequence.Length; i++)
        {
            char c = sequence[i];
            if (char.IsWhiteSpace(c)) continue;

            if (c == '{')
            {
                int end = sequence.IndexOf('}', i + 1);
                if (end < 0) { error = "Unclosed '{' in the key sequence."; return keys; }
                string token = sequence.Substring(i + 1, end - i - 1).Trim();
                i = end;

                if (token.StartsWith("DELAY:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(token.AsSpan(6), out int ms))
                    { error = $"Bad delay token '{{{token}}}'."; return keys; }
                    keys.Add(new Key(0, false, false, true, ms));
                    continue;
                }

                if (!NamedKeys.TryGetValue(token.ToUpperInvariant(), out var named))
                { error = $"Unknown key token '{{{token}}}'."; return keys; }
                keys.Add(named);
                continue;
            }

            if (!TryCharKey(c, out var key))
            { error = $"Can't send the character '{c}'."; return keys; }
            keys.Add(key);
        }
        return keys;
    }

    private static bool TryCharKey(char c, out Key key)
    {
        key = default;
        short vks = VkKeyScan(c);
        if (vks == -1) return false;
        ushort vk = (ushort)(vks & 0xFF);
        bool shift = (vks & 0x100) != 0;
        ushort scan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC);
        if (scan == 0) return false;
        key = new Key(scan, false, shift, false, 0);
        return true;
    }

    private static Key Named(ushort vk, bool extended)
    {
        ushort scan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC);
        return new Key(scan, extended, false, false, 0);
    }

    private static readonly Dictionary<string, Key> NamedKeys = new()
    {
        ["ENTER"] = Named(0x0D, false),
        ["RETURN"] = Named(0x0D, false),
        ["ESC"] = Named(0x1B, false),
        ["ESCAPE"] = Named(0x1B, false),
        ["SPACE"] = Named(0x20, false),
        ["TAB"] = Named(0x09, false),
        ["BACKSPACE"] = Named(0x08, false),
        ["UP"] = Named(0x26, true),
        ["DOWN"] = Named(0x28, true),
        ["LEFT"] = Named(0x25, true),
        ["RIGHT"] = Named(0x27, true),
    };
}
