namespace SwordOfAragonTrainer.ViewModels;

/// <summary>How a value is encoded at the address a pin points at.</summary>
public enum PinKind
{
    /// <summary>A plain little-endian integer of the pin's width.</summary>
    Raw,

    /// <summary>A QuickBASIC (MBF) single-precision float, always four bytes.</summary>
    MbfSingle,
}

/// <summary>The read/write channel a scan-result or pinned-value row uses to reach game RAM.</summary>
public interface IScanHost
{
    /// <summary>Reads <paramref name="width"/> bytes as an unsigned little-endian value.</summary>
    bool Read(nuint address, ScanWidth width, out long value);

    /// <summary>Writes <paramref name="value"/> as exactly <paramref name="width"/> little-endian bytes.</summary>
    bool Write(nuint address, long value, ScanWidth width);

    /// <summary>Writes a byte sequence verbatim (used for MBF singles).</summary>
    bool WriteBytes(nuint address, byte[] bytes);

    /// <summary>Reports that a write failed, so the host can surface it in the status line.</summary>
    void ReportWriteFailure(nuint address);
}
