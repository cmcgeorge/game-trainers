namespace DarklandsTrainer.ViewModels;

/// <summary>The read/write channel a scan-result or frozen-value row uses to reach guest RAM.</summary>
public interface IScanHost
{
    /// <summary>
    /// Writes <paramref name="value"/> (little-endian, exactly <paramref name="width"/> bytes) at an
    /// absolute address. The width is passed by the caller — a pinned row carries the width it was
    /// captured at, which can differ from the width the current scan uses.
    /// </summary>
    bool Write(nuint address, long value, ScanWidth width);

    /// <summary>Reads <paramref name="width"/> bytes at an absolute address as an unsigned little-endian value.</summary>
    bool Read(nuint address, ScanWidth width, out long value);

    /// <summary>Reports that a write failed, so the host can surface it in the status line.</summary>
    void ReportWriteFailure(nuint address);
}
