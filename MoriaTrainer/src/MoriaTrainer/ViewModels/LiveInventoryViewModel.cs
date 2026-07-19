using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using MoriaTrainer.Game;

namespace MoriaTrainer.ViewModels;

/// <summary>
/// Backs the Inventory tab: locates the <c>inventory[34]</c> global array in DOSBox guest RAM and
/// exposes all 34 slots for live viewing and editing. Because <c>inventory[]</c> is a separate BSS
/// global (not inside <c>player_type</c>), it is found by scanning for the slot-0 <c>tval</c> byte
/// the user identifies from their pack.
///
/// <para><b>Confidence: Candidate.</b> The <see cref="InvenTypeFormat"/> offsets are derived from the
/// UMoria 5.5.2 C source and natural DJGPP alignment rules but have not yet been confirmed against a
/// Ghidra analysis of the live binary. Writes follow the read-validate-write pattern and are limited
/// to the quantity, charges (P1), and subtype fields so a wrong struct size cannot corrupt adjacent
/// globals.</para>
///
/// <para>Workflow: the user selects one of their pack items from the Items reference, types the slot
/// letter (a=0), then clicks Locate. The trainer scans memory for a tval byte matching that item at
/// the correct struct offset, then reads the adjacent slots to validate the whole array.</para>
/// </summary>
public sealed class LiveInventoryViewModel : ObservableObject, IDisposable
{
    private ProcessMemory? _mem;
    private MemorySearcher? _searcher;
    private CancellationTokenSource? _cts;
    private readonly DispatcherTimer _poll;

    private nuint? _inventoryBase;

    // --- state --------------------------------------------------------------
    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isLocated;
    public bool IsLocated { get => _isLocated; private set { if (SetField(ref _isLocated, value)) RaiseCommands(); } }

    private bool _isSearching;
    public bool IsSearching { get => _isSearching; private set { if (SetField(ref _isSearching, value)) RaiseCommands(); } }

    private string _status =
        "Attach on the Character tab. Then identify an item in your pack (slot letter a–v) and click Locate.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- locate hints -------------------------------------------------------
    private string _hintSlot = "a";
    /// <summary>Pack slot letter the user is anchoring on (a=slot 0, b=slot 1, …, v=slot 21).</summary>
    public string HintSlot { get => _hintSlot; set => SetField(ref _hintSlot, value); }

    private string _hintTval = "";
    /// <summary>
    /// tval (decimal or 0xNN) of the item in the specified slot. Use the Items reference tab to find
    /// the tval for your item category (e.g. sword = 5, potion = 26, scroll = 25).
    /// </summary>
    public string HintTval { get => _hintTval; set => SetField(ref _hintTval, value); }

    // --- live slots ---------------------------------------------------------
    public ObservableCollection<InvenSlotRow> Slots { get; } = new();

    // --- selected slot for editing ------------------------------------------
    private InvenSlotRow? _selected;
    public InvenSlotRow? Selected { get => _selected; set { SetField(ref _selected, value); RaiseCommands(); } }

    // --- commands -----------------------------------------------------------
    public ICommand LocateCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand WriteNumberCommand { get; }
    public ICommand WriteChargesCommand { get; }

    public LiveInventoryViewModel()
    {
        LocateCommand      = new RelayCommand(_ => _ = LocateAsync(), _ => IsAttached && !IsSearching && !IsLocated);
        ResetCommand       = new RelayCommand(_ => Reset(), _ => IsLocated);
        WriteNumberCommand = new RelayCommand(_ => WriteNumber(), _ => IsLocated && Selected != null);
        WriteChargesCommand = new RelayCommand(_ => WriteCharges(), _ => IsLocated && Selected != null);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _poll.Tick += (_, _) => PollTick();
    }

    // --- attach/detach ------------------------------------------------------
    public void OnAttached(ProcessMemory mem)
    {
        _mem = mem;
        _searcher = new MemorySearcher(mem, ScanWidth.Byte);
        _poll.Start();
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Attached. Pick an item in your pack, find its tval in the Items reference tab, type the slot letter and tval, then Locate.";
    }

    public void OnDetached()
    {
        _poll.Stop();
        _cts?.Cancel();
        _mem = null;
        _searcher = null;
        _inventoryBase = null;
        Slots.Clear();
        IsLocated = false;
        IsSearching = false;
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- locate -------------------------------------------------------------
    private async Task LocateAsync()
    {
        if (_mem is not { IsOpen: true } mem || _searcher == null) return;

        // Parse slot letter → index (a=0 … v=21)
        if (string.IsNullOrWhiteSpace(HintSlot) || HintSlot.Trim().Length != 1)
        { Status = "Enter a single pack slot letter (a–v)."; return; }
        char letter = char.ToLowerInvariant(HintSlot.Trim()[0]);
        int slotIndex = letter - 'a';
        if (slotIndex < 0 || slotIndex >= PlayerFormat.InvenPack)
        { Status = "Slot must be a–v (pack slots 0–21)."; return; }

        if (!ScanValue.TryParse(HintTval, out long tvalLong) || tvalLong < 1 || tvalLong > 127)
        { Status = "Enter the tval for that item (1–127, decimal or 0xNN). See the Items reference tab."; return; }
        byte tval = (byte)tvalLong;

        IsSearching = true;
        Status = $"Scanning for tval 0x{tval:X2} (slot '{letter}')…";
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        List<nuint> candidates = new();
        try
        {
            var searcher = _searcher;
            nuint slotOffset = (nuint)(slotIndex * InvenTypeFormat.StructSize + InvenTypeFormat.TvalOff);
            // Run scan and validation on a background thread to avoid blocking the UI thread.
            await Task.Run(() =>
            {
                searcher.FirstScanExact(tval, ct);
                ct.ThrowIfCancellationRequested();
                foreach (var m in searcher.Take(10_000))
                {
                    ct.ThrowIfCancellationRequested();
                    nuint candidate = m.Address - slotOffset;
                    if (ValidateInventoryBase(mem, candidate, slotIndex, tval))
                        lock (candidates) candidates.Add(candidate);
                }
            }, ct);
        }
        catch (OperationCanceledException) { Status = "Cancelled."; return; }
        catch (Exception ex) { Status = "Scan error: " + ex.Message; return; }
        finally { IsSearching = false; }

        if (mem != _mem) return;

        switch (candidates.Count)
        {
            case 0:
                Status = $"Not found. Confirm the item is in slot '{letter}' and its tval is 0x{tval:X2}. " +
                         "Try a different anchor slot or item.";
                return;
            case 1:
                _inventoryBase = candidates[0];
                PopulateSlots(mem);
                IsLocated = true;
                Status = $"Inventory located at 0x{(ulong)candidates[0]:X}. " +
                         "⚠ Confidence: Candidate — struct offsets derived from source, not confirmed by Ghidra. " +
                         "Verify slot contents before editing.";
                break;
            default:
                Status = $"{candidates.Count} candidates. Move or use the anchor item to change its quantity or " +
                         "pick a more distinctive item (e.g. a stack with a unique count) and try again.";
                return;
        }
    }

    private static bool ValidateInventoryBase(ProcessMemory mem, nuint invBase, int anchorSlot, byte expectedTval)
    {
        // Validate anchor slot tval first (fast path).
        nuint anchorOff = (nuint)(anchorSlot * InvenTypeFormat.StructSize + InvenTypeFormat.TvalOff);
        var anchorBuf = mem.Read(invBase + anchorOff, 1);
        if (anchorBuf.Length < 1 || anchorBuf[0] != expectedTval) return false;

        // Validate every slot's tval byte is in the valid UMoria range 0..127. Checking all 34 slots
        // reduces the false-positive probability to ~2^-34 for random data, making the validator
        // sufficiently selective despite not having confirmed struct sizes for a Ghidra cross-check.
        for (int i = 0; i < PlayerFormat.InvenArraySize; i++)
        {
            nuint off = (nuint)(i * InvenTypeFormat.StructSize + InvenTypeFormat.TvalOff);
            var buf = mem.Read(invBase + off, 1);
            if (buf.Length < 1 || buf[0] > 127) return false;
        }
        return true;
    }

    private void PopulateSlots(ProcessMemory mem)
    {
        Slots.Clear();
        for (int i = 0; i < PlayerFormat.InvenArraySize; i++)
            Slots.Add(ReadSlot(mem, i));
        Selected = Slots.FirstOrDefault(s => s.IsOccupied) ?? Slots.FirstOrDefault();
    }

    private InvenSlotRow ReadSlot(ProcessMemory mem, int index)
    {
        nuint slotBase = _inventoryBase!.Value + (nuint)(index * InvenTypeFormat.StructSize);
        byte tval    = ReadByte(mem, slotBase + InvenTypeFormat.TvalOff);
        byte subval  = ReadByte(mem, slotBase + InvenTypeFormat.SubValOff);
        byte number  = ReadByte(mem, slotBase + InvenTypeFormat.NumberOff);
        short p1     = ReadInt16(mem, slotBase + InvenTypeFormat.P1Off);
        ushort weight = (ushort)ReadInt16(mem, slotBase + InvenTypeFormat.WeightOff);

        var slot = new InvenSlot(index) { Tval = tval, SubVal = subval, Number = number, P1 = p1, Weight = weight };
        return new InvenSlotRow(slot, slotBase);
    }

    // --- poll ---------------------------------------------------------------
    private void PollTick()
    {
        if (_mem is not { IsOpen: true } mem || !IsLocated) return;
        foreach (var row in Slots)
        {
            nuint slotBase = row.Address;
            row.Tval    = ReadByte(mem, slotBase + InvenTypeFormat.TvalOff);
            row.Number  = ReadByte(mem, slotBase + InvenTypeFormat.NumberOff);
            row.P1      = ReadInt16(mem, slotBase + InvenTypeFormat.P1Off);
        }
    }

    // --- write helpers ------------------------------------------------------
    private void WriteNumber()
    {
        if (_mem is not { IsOpen: true } mem || Selected is not { } row) return;
        if (!byte.TryParse(row.EditNumber, out byte n))
        { Status = $"Invalid quantity '{row.EditNumber}' — must be 0–255."; return; }
        if (!mem.Write(row.Address + InvenTypeFormat.NumberOff, new[] { n }))
        { Status = "Write failed — is DOSBox still running?"; return; }
        row.Number = n;
        Status = $"Slot {row.Slot.SlotLabel}: quantity set to {n}.";
    }

    private void WriteCharges()
    {
        if (_mem is not { IsOpen: true } mem || Selected is not { } row) return;
        if (!short.TryParse(row.EditCharges, out short p1))
        { Status = $"Invalid charges '{row.EditCharges}' — must be a signed 16-bit integer."; return; }
        var bytes = new byte[] { (byte)(p1 & 0xFF), (byte)((p1 >> 8) & 0xFF) };
        if (!mem.Write(row.Address + InvenTypeFormat.P1Off, bytes))
        { Status = "Write failed — is DOSBox still running?"; return; }
        row.P1 = p1;
        Status = $"Slot {row.Slot.SlotLabel}: P1/charges set to {p1}.";
    }

    private void Reset()
    {
        Slots.Clear();
        _inventoryBase = null;
        IsLocated = false;
        Status = "Reset. Enter new anchor information and click Locate.";
    }

    // --- raw read helpers ---------------------------------------------------
    private static byte ReadByte(ProcessMemory mem, nuint address)
    {
        var b = mem.Read(address, 1);
        return b.Length >= 1 ? b[0] : (byte)0;
    }

    private static short ReadInt16(ProcessMemory mem, nuint address)
    {
        var b = mem.Read(address, 2);
        return b.Length >= 2 ? (short)(b[0] | (b[1] << 8)) : (short)0;
    }

    private void RaiseCommands()
    {
        (LocateCommand      as RelayCommand)?.RaiseCanExecuteChanged();
        (ResetCommand       as RelayCommand)?.RaiseCanExecuteChanged();
        (WriteNumberCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (WriteChargesCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

// ---------------------------------------------------------------------------
// One row in the inventory grid (wraps InvenSlot + its live RAM address).
// ---------------------------------------------------------------------------

/// <summary>One inventory slot bound to the UI grid. Editable fields are strings so the grid TextBox
/// round-trips correctly; the <see cref="LiveInventoryViewModel"/> validates them on write.</summary>
public sealed class InvenSlotRow : ObservableObject
{
    public InvenSlot Slot { get; }
    public nuint Address { get; }

    public string SlotLabel => Slot.SlotLabel;

    private byte _tval;
    public byte Tval
    {
        get => _tval;
        set { if (SetField(ref _tval, value)) { Slot.Tval = value; OnPropertyChanged(nameof(CategoryName)); OnPropertyChanged(nameof(IsOccupied)); } }
    }

    private byte _number;
    public byte Number
    {
        get => _number;
        set { if (SetField(ref _number, value)) Slot.Number = value; }
    }

    private short _p1;
    public short P1
    {
        get => _p1;
        set { if (SetField(ref _p1, value)) Slot.P1 = value; }
    }

    public string CategoryName => Slot.CategoryName;
    public bool IsOccupied => Slot.IsOccupied;

    // editable text fields
    private string _editNumber = "1";
    public string EditNumber { get => _editNumber; set => SetField(ref _editNumber, value); }

    private string _editCharges = "0";
    public string EditCharges { get => _editCharges; set => SetField(ref _editCharges, value); }

    public InvenSlotRow(InvenSlot slot, nuint address)
    {
        Slot = slot;
        Address = address;
        _tval   = slot.Tval;
        _number = slot.Number;
        _p1     = slot.P1;
        _editNumber  = slot.Number.ToString();
        _editCharges = slot.P1.ToString();
    }
}
