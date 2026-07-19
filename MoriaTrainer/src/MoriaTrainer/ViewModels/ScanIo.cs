using GameTrainers.Common.Memory;

namespace MoriaTrainer.ViewModels;

/// <summary>
/// Little-endian read/write helpers shared by <see cref="MainViewModel"/> and
/// <see cref="TeleportViewModel"/>. Both view-models need the same byte-by-byte translation between a
/// signed <see cref="long"/> and the 1/2/4-byte cell a <see cref="ScanWidth"/> pin was captured at, so
/// the logic lives here rather than being duplicated in each host. The helpers are null-safe against
/// the host's <see cref="ProcessMemory"/> being disposed between a scan and a write.
/// </summary>
internal static class ScanIo
{
    /// <summary>
    /// Reads <paramref name="width"/> bytes at <paramref name="address"/> as an unsigned little-endian
    /// value. Returns false (with <paramref name="value"/> set to 0) if the process is gone or the read
    /// came back short.
    /// </summary>
    public static bool ReadAt(ProcessMemory? mem, nuint address, ScanWidth width, out long value)
    {
        value = 0;
        if (mem is not { IsOpen: true }) return false;
        int w = (int)width;
        var buf = mem.Read(address, w);
        if (buf.Length < w) return false;
        long result = 0;
        for (int i = 0; i < w; i++) result |= (long)buf[i] << (8 * i);
        value = result;
        return true;
    }

    /// <summary>
    /// Writes <paramref name="value"/> as little-endian, exactly <paramref name="width"/> bytes, at
    /// <paramref name="address"/>. Returns false if the process is gone or the write was rejected by
    /// the OS. The caller is responsible for width-fit (see <see cref="ScanValue.FitsWidth"/>); these
    /// helpers do not re-validate, so a pin's width is trusted.
    /// </summary>
    public static bool WriteAt(ProcessMemory? mem, nuint address, long value, ScanWidth width)
    {
        if (mem is not { IsOpen: true }) return false;
        int w = (int)width;
        var buf = new byte[w];
        ulong v = unchecked((ulong)value);
        for (int k = 0; k < w; k++) { buf[k] = (byte)(v & 0xFF); v >>= 8; }
        return mem.Write(address, buf);
    }
}
