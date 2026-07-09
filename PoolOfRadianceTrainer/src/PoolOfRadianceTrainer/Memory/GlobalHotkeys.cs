using System.Windows.Interop;
using static PoolOfRadianceTrainer.Memory.NativeMethods;

namespace PoolOfRadianceTrainer.Memory;

/// <summary>
/// Registers system-wide Ctrl+F1/F2/F3 hotkeys that fire even while the game window has
/// focus, so the player never has to alt-tab mid-fight. Each key raises an event the
/// view-model handles (god mode, heal party, max everything).
/// </summary>
public sealed class GlobalHotkeys : IDisposable
{
    private const int IdGod = 0xB01;
    private const int IdHeal = 0xB02;
    private const int IdMax = 0xB03;

    private const uint VK_F1 = 0x70, VK_F2 = 0x71, VK_F3 = 0x72;

    private readonly IntPtr _hwnd;
    private readonly HwndSource? _source;

    public event Action? GodModeToggled;
    public event Action? HealRequested;
    public event Action? MaxRequested;

    /// <summary>True for each hotkey that registered successfully (some may be taken by other apps).</summary>
    public bool GodModeRegistered { get; }
    public bool HealRegistered { get; }
    public bool MaxRegistered { get; }

    public GlobalHotkeys(IntPtr windowHandle)
    {
        _hwnd = windowHandle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        GodModeRegistered = RegisterHotKey(_hwnd, IdGod, MOD_CONTROL | MOD_NOREPEAT, VK_F1);
        HealRegistered = RegisterHotKey(_hwnd, IdHeal, MOD_CONTROL | MOD_NOREPEAT, VK_F2);
        MaxRegistered = RegisterHotKey(_hwnd, IdMax, MOD_CONTROL | MOD_NOREPEAT, VK_F3);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            switch (wParam.ToInt32())
            {
                case IdGod: GodModeToggled?.Invoke(); handled = true; break;
                case IdHeal: HealRequested?.Invoke(); handled = true; break;
                case IdMax: MaxRequested?.Invoke(); handled = true; break;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _source?.RemoveHook(WndProc);
        if (GodModeRegistered) UnregisterHotKey(_hwnd, IdGod);
        if (HealRegistered) UnregisterHotKey(_hwnd, IdHeal);
        if (MaxRegistered) UnregisterHotKey(_hwnd, IdMax);
    }
}
