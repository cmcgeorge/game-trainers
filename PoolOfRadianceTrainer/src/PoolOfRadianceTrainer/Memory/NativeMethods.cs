using System.Runtime.InteropServices;

namespace PoolOfRadianceTrainer.Memory;

internal static class NativeMethods
{
    // --- Global hotkeys (fire even while the game window has focus) -----------------
    // Memory P/Invokes (OpenProcess / Read / Write / VirtualQueryEx) now live in
    // GameTrainers.Common.Memory; only the hotkey imports remain PoR-local because
    // Common's NativeMethods doesn't expose them.
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_NOREPEAT = 0x4000;
    public const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
