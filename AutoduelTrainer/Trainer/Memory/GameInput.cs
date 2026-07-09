using System.Runtime.InteropServices;

namespace AutoduelTrainer.Memory;

/// <summary>
/// Sends a short key sequence to the DOSBox game window (used to drive the game's
/// own quit → reload flow so a teleport takes effect without a manual save/load).
/// Only the handful of keys the reload sequence needs are mapped.
/// </summary>
public static class GameInput
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const int SW_RESTORE = 9;

    // Virtual-key + set-1 scan code for each key the reload sequence uses.
    private static (byte vk, byte scan) Map(char c) => c switch
    {
        'q' or 'Q' => (0x51, 0x10),
        'y' or 'Y' => (0x59, 0x15),
        '1' => (0x31, 0x02),
        '4' => (0x34, 0x05),
        _ => (0, 0),
    };

    /// <summary>
    /// Bring <paramref name="hWnd"/> to the foreground and type <paramref name="keys"/>
    /// one at a time, pausing <paramref name="perKeyDelayMs"/> between them so the game
    /// has time to redraw each screen (quit prompt, menu, driver list, city load).
    /// </summary>
    public static async Task SendSequenceAsync(IntPtr hWnd, string keys, int perKeyDelayMs = 700)
    {
        if (hWnd == IntPtr.Zero) return;

        if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
        SetForegroundWindow(hWnd);
        await Task.Delay(300);

        foreach (char c in keys)
        {
            var (vk, scan) = Map(c);
            if (vk == 0) continue;
            keybd_event(vk, scan, 0, UIntPtr.Zero);            // key down
            await Task.Delay(40);
            keybd_event(vk, scan, KEYEVENTF_KEYUP, UIntPtr.Zero); // key up
            await Task.Delay(perKeyDelayMs);
        }
    }
}
