using System.Diagnostics;

namespace KeefTrainer.Core;

public sealed class GameSnapshot
{
    private readonly Dictionary<KeefField, int> _values = new();
    public int this[KeefField f] => _values.TryGetValue(f, out int v) ? v : 0;
    internal void Set(KeefField f, int v) => _values[f] = v;

    /// <summary>Heuristic: a game is actually in progress (not the title screen).</summary>
    public bool LooksInGame =>
        this[KeefField.Level] >= 1 && this[KeefField.Level] <= 24 &&
        this[KeefField.Constitution] > 0 && this[KeefField.Nutrition] <= 100;
}

/// <summary>
/// Attaches to a running DOSBox / DOSBox-X process, locates the Keef the Thief
/// stat-table anchor in emulated guest RAM, and provides snapshot reads,
/// field writes and value freezing.
/// </summary>
public sealed class TrainerEngine : IDisposable
{
    // DOS conventional memory sits in the first MB of guest RAM, so 2 MB per
    // region is enough for the anchor scan. Buffer is reused across attach
    // attempts (all use is serialized by _gate).
    private const int ScanCap = 2 * 1024 * 1024;

    private ProcessMemory? _proc;
    private nuint _dseg;                       // host address of the stat table (DSEG)
    private byte[]? _scanBuffer;
    private readonly byte[] _snapshotBuf = new byte[KeefMap.SnapshotSize];
    private readonly byte[] _anchorBuf = new byte[15];
    private readonly Dictionary<KeefField, int> _frozen = new();
    private readonly object _gate = new();

    public bool IsAttached { get; private set; }
    public string StatusText { get; private set; } = "Searching for DOSBox…";

    public event EventHandler? AttachmentChanged;

    // ---------------------------------------------------------------- attach

    /// <summary>Try to find a DOSBox-like process containing the Keef stat table.</summary>
    public bool TryAttach()
    {
        bool attached = false;
        lock (_gate)
        {
            DetachLocked();

            Process[] processes = Process.GetProcesses();
            try
            {
                foreach (var process in processes)
                {
                    if (attached) break;

                    bool candidate;
                    try
                    {
                        candidate = process.ProcessName.StartsWith("dosbox", StringComparison.OrdinalIgnoreCase);
                    }
                    catch { candidate = false; }
                    if (!candidate) continue;

                    var mem = ProcessMemory.Open(process);
                    if (mem is null) continue;

                    nuint anchor = FindAnchor(mem);
                    if (anchor != 0)
                    {
                        _proc = mem;
                        _dseg = anchor;
                        IsAttached = true;
                        StatusText = $"Attached to {mem.ProcessName} (PID {mem.ProcessId}) — table @ 0x{anchor:X}";
                        attached = true;
                    }
                    else
                    {
                        mem.Dispose();
                    }
                }
            }
            finally
            {
                foreach (var process in processes) process.Dispose();
            }

            if (!attached)
                StatusText = "Searching for DOSBox… run Keef the Thief in DOSBox / DOSBox-X.";
        }
        AttachmentChanged?.Invoke(this, EventArgs.Empty);
        return attached;
    }

    public void Detach()
    {
        lock (_gate) DetachLocked();
        AttachmentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DetachLocked()
    {
        _proc?.Dispose();
        _proc = null;
        _dseg = 0;
        IsAttached = false;
    }

    /// <summary>
    /// Scan candidate guest-RAM regions (large private RW blocks) for the stat-table
    /// label signature and return the host address of DSEG, or 0.
    /// </summary>
    private nuint FindAnchor(ProcessMemory mem)
    {
        var sig0 = KeefMap.SignatureParts[0];
        byte[] buffer = _scanBuffer ??= new byte[ScanCap];

        // Guest RAM is one big private RW allocation (16 MB by default in DOSBox-X;
        // allow 1 MB..512 MB to cover custom memsize settings).
        foreach (var (regionBase, regionSize) in mem.EnumerateRwPrivateRegions(1024 * 1024, 512 * 1024 * 1024))
        {
            int want = (int)Math.Min(regionSize, (nuint)buffer.Length);
            if (want < sig0.Length || !mem.Read(regionBase, buffer, want)) continue;

            var span = buffer.AsSpan(0, want);
            int at = 0;
            while (at <= span.Length - sig0.Length)
            {
                int idx = span[at..].IndexOf(sig0);
                if (idx < 0) break;
                int hit = at + idx;
                if (VerifySignature(span, hit))
                {
                    long dseg = (long)regionBase + hit - KeefMap.SignatureToDseg;
                    if (dseg >= 0) return (nuint)dseg;
                }
                at = hit + 1;
            }
        }
        return 0;
    }

    private static bool VerifySignature(ReadOnlySpan<byte> data, int hit)
    {
        for (int p = 1; p < KeefMap.SignatureParts.Length; p++)
        {
            int off = hit + KeefMap.SignaturePartOffsets[p];
            var part = KeefMap.SignatureParts[p];
            if (off + part.Length > data.Length) return false;
            if (!data.Slice(off, part.Length).SequenceEqual(part)) return false;
        }
        return true;
    }

    /// <summary>Cheap per-tick re-validation that the anchor is still where we found it.</summary>
    private bool AnchorStillValid()
    {
        if (_proc is null || _dseg == 0) return false;
        if (!_proc.Read(_dseg + KeefMap.SignatureToDseg, _anchorBuf, _anchorBuf.Length)) return false;
        return _anchorBuf.AsSpan().SequenceEqual(KeefMap.SignatureParts[0]);
    }

    // ------------------------------------------------------------- tick/read

    /// <summary>Read a snapshot, enforce freezes. Returns null when detached/invalid.</summary>
    public GameSnapshot? Tick()
    {
        bool lost = false;
        GameSnapshot? snap = null;
        lock (_gate)
        {
            if (!IsAttached || _proc is null) return null;

            if (!_proc.IsAlive || !AnchorStillValid())
            {
                DetachLocked();
                StatusText = "Lost DOSBox — searching…";
                lost = true;
            }
            else
            {
                // Enforce frozen values first so the snapshot reflects them.
                foreach (var (field, value) in _frozen)
                    WriteFieldLocked(field, value);

                if (_proc.Read(_dseg, _snapshotBuf, _snapshotBuf.Length))
                {
                    snap = new GameSnapshot();
                    foreach (var info in KeefMap.Fields)
                    {
                        int v = info.Is32Bit
                            ? unchecked((int)BitConverter.ToUInt32(_snapshotBuf, info.Delta))
                            : BitConverter.ToUInt16(_snapshotBuf, info.Delta);
                        snap.Set(info.Field, v);
                    }
                }
            }
        }
        if (lost) AttachmentChanged?.Invoke(this, EventArgs.Empty);
        return snap;
    }

    // ----------------------------------------------------------------- write

    public bool WriteField(KeefField field, int value)
    {
        lock (_gate) return WriteFieldLocked(field, value);
    }

    private bool WriteFieldLocked(KeefField field, int value)
    {
        if (!IsAttached || _proc is null) return false;
        var info = KeefMap.Info(field);
        value = Math.Clamp(value, info.Min, info.Max);
        nuint address = _dseg + (nuint)info.Delta;
        return info.Is32Bit
            ? _proc.WriteUInt32(address, (uint)value)
            : _proc.WriteUInt16(address, (ushort)value);
    }

    // ---------------------------------------------------------------- freeze

    public void SetFrozen(KeefField field, int? value)
    {
        lock (_gate)
        {
            if (value is int v)
            {
                var info = KeefMap.Info(field);
                _frozen[field] = Math.Clamp(v, info.Min, info.Max);
            }
            else
            {
                _frozen.Remove(field);
            }
        }
    }

    public bool IsFrozen(KeefField field)
    {
        lock (_gate) return _frozen.ContainsKey(field);
    }

    public void Dispose()
    {
        lock (_gate) DetachLocked();
    }
}
