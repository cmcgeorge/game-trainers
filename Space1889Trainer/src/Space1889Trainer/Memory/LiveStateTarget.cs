using System.ComponentModel;
using System.Diagnostics;
using Space1889Trainer.Game;

namespace Space1889Trainer.Memory;

/// <summary>Reads and writes the state block inside a running emulator.</summary>
public sealed class LiveStateTarget : IStateTarget, IDisposable
{
    private readonly ProcessMemory _memory;
    private readonly IMemorySource _source;
    private readonly Process? _process;

    public LiveStateTarget(ProcessMemory memory, nuint blockHost)
    {
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
        _source = new ProcessMemorySource(memory);
        BlockHost = blockHost;
        // Hold the process object from attach time: asking HasExited of it cannot be fooled by the pid
        // being reused by some other program after DOSBox closes.
        try { _process = Process.GetProcessById(memory.ProcessId); }
        catch (ArgumentException) { _process = null; }
    }

    /// <summary>Host address of offset 0 of the block.</summary>
    public nuint BlockHost { get; }

    /// <summary>False once the handle is closed or the emulator process has exited.</summary>
    public bool IsAvailable
    {
        get
        {
            if (!_memory.IsOpen) return false;
            if (_process is null) return false;   // already gone by the time the target was built
            try { return !_process.HasExited; }
            catch (InvalidOperationException) { return true; }   // not queryable; reads and writes still fail safely
            catch (Win32Exception) { return true; }
        }
    }

    public void Dispose() => _process?.Dispose();

    public byte[]? Read(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > StateFormat.BlockLength - count) return null;
        var data = _memory.Read(BlockHost + (nuint)offset, count);
        return data.Length == count ? data : null;
    }

    public bool Write(int offset, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (offset < 0 || offset > StateFormat.BlockLength - data.Length) return false;
        // Re-validate immediately before committing: between one tick and the next the player may
        // have quit to DOS, and a stale address would put these bytes into someone else's memory.
        if (!GameLocator.StillValid(_source, BlockHost)) return false;
        return _memory.Write(BlockHost + (nuint)offset, data);
    }
}
