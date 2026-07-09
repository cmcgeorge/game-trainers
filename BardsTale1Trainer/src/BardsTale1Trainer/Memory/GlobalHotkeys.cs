using System.Windows;
using System.Windows.Interop;
using static BardsTale1Trainer.Memory.NativeMethods;

namespace BardsTale1Trainer.Memory;

/// <summary>
/// Registers system-wide hotkeys against a window and dispatches WM_HOTKEY to callbacks.
/// Global (RegisterHotKey) rather than WPF input bindings because the whole point is to
/// fire while the *game* window has focus — pressing the keys must not require alt-tabbing
/// to the trainer. Construct after the window has a handle (e.g. in OnSourceInitialized);
/// dispose to unregister (also runs implicitly at process exit).
/// </summary>
public sealed class GlobalHotkeys : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 1;
    private bool _disposed;

    public GlobalHotkeys(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _source = HwndSource.FromHwnd(helper.Handle)
            ?? throw new InvalidOperationException("The window has no handle yet — register hotkeys in OnSourceInitialized.");
        _source.AddHook(WndProc);
    }

    /// <summary>Registers Ctrl+<paramref name="vk"/>. Returns false (with the callback NOT
    /// registered) when the combination is already claimed by another application.</summary>
    public bool RegisterCtrl(uint vk, Action callback)
    {
        int id = _nextId++;
        if (!RegisterHotKey(_source.Handle, id, MOD_CONTROL | MOD_NOREPEAT, vk))
            return false;
        _handlers[id] = callback;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _handlers.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            foreach (var id in _handlers.Keys)
                UnregisterHotKey(_source.Handle, id);
            _source.RemoveHook(WndProc);
        }
        catch
        {
            // The window/handle may already be torn down when this runs from OnClosed;
            // the OS releases hotkey registrations with the hwnd, so nothing is leaked.
        }
        _handlers.Clear();
    }
}
