using System.Text;
using WastelandRemasteredTrainer.Memory;

namespace WastelandRemasteredTrainer.Game;

/// <summary>
/// Layout of the Unity IL2CPP runtime structures the trainer walks, plus the typed
/// reads/writes that go with them.
///
/// <para>Wasteland Remastered is a 64-bit IL2CPP build (Unity 2018.4), so every managed object
/// on the heap starts with a 16-byte header — an <c>Il2CppClass*</c> then a monitor slot —
/// and the first instance field sits at +0x10. Managed arrays add a bounds pointer and a
/// length before their elements, putting element 0 at +0x20.</para>
///
/// <para>A static field is reached through the type's <c>Il2CppClass</c>: the generated code
/// loads the class pointer from a metadata-usage slot in <c>GameAssembly.dll</c>'s data
/// section, follows <c>static_fields</c>, then indexes the static storage. Since we do not
/// have the metadata-usage slot RVAs, the class pointer is found by sweeping the module's
/// data sections for a pointer that resolves to an <c>Il2CppClass</c> with the right name
/// (see <see cref="Il2CppClassLocator"/>).</para>
///
/// <para>Every read helper comes in two forms. The <c>Read*</c> form returns 0 on a failed
/// read, which is convenient but indistinguishable from a genuine zero; the <c>TryRead*</c>
/// form reports the failure. Anything that writes a value back — a freeze, a cached edit —
/// must use the <c>TryRead*</c> form, or a transient unreadable page turns into a zero
/// written straight into the character.</para>
/// </summary>
public static class Il2Cpp
{
    // --- object / array / string ------------------------------------------------
    /// <summary>Il2CppClass* + monitor: the first instance field is at +0x10.</summary>
    public const int ObjectHeaderSize = 0x10;

    /// <summary>Offset of the <c>Il2CppClass*</c> every managed object starts with.</summary>
    public const int ObjectClassOffset = 0x00;

    /// <summary>klass (8) + monitor (8) + bounds (8) + length (8, 4 used): element 0 is at +0x20.</summary>
    public const int ArrayHeaderSize = 0x20;

    /// <summary>Offset of an array's <c>max_length</c> field.</summary>
    public const int ArrayLengthOffset = 0x18;

    /// <summary>Character count of an <c>Il2CppString</c>.</summary>
    public const int StringLengthOffset = 0x10;

    /// <summary>First UTF-16 code unit of an <c>Il2CppString</c>.</summary>
    public const int StringCharsOffset = 0x14;

    // --- Il2CppClass ------------------------------------------------------------
    /// <summary><c>const char* name</c> — an ASCII type name, used to validate a class pointer.</summary>
    public const int ClassNameOffset = 0x10;

    /// <summary><c>const char* namespaze</c>.</summary>
    public const int ClassNamespaceOffset = 0x18;

    /// <summary>
    /// <c>void* static_fields</c> — the block holding the type's static field values.
    /// [Confirmed] by the generated code for every <c>m_instance</c> singleton access
    /// (<c>mov rcx,[rax+0xB8]</c> immediately after loading the class pointer).
    /// </summary>
    public const int ClassStaticFieldsOffset = 0xB8;

    /// <summary>Longest type name the validator will accept when probing a class pointer.</summary>
    private const int MaxTypeNameLength = 64;

    /// <summary>An IL2CPP string longer than this is treated as a bad read, not a name.</summary>
    private const int MaxStringLength = 512;

    // --- primitive reads (failure-reporting form) -------------------------------
    /// <summary>Reads a pointer, reporting whether the read succeeded.</summary>
    public static bool TryReadPtr(this IMemorySource mem, nuint address, out nuint value)
    {
        var buf = new byte[8];
        if (mem.Read(address, buf, 8) != 8) { value = 0; return false; }
        value = (nuint)BitConverter.ToUInt64(buf, 0);
        return true;
    }

    /// <summary>Reads an int32, reporting whether the read succeeded.</summary>
    public static bool TryReadI32(this IMemorySource mem, nuint address, out int value)
    {
        var buf = new byte[4];
        if (mem.Read(address, buf, 4) != 4) { value = 0; return false; }
        value = BitConverter.ToInt32(buf, 0);
        return true;
    }

    /// <summary>Reads a byte, reporting whether the read succeeded.</summary>
    public static bool TryReadByte(this IMemorySource mem, nuint address, out byte value)
    {
        var buf = new byte[1];
        if (mem.Read(address, buf, 1) != 1) { value = 0; return false; }
        value = buf[0];
        return true;
    }

    // --- primitive reads (convenience form; 0 means "zero or unreadable") -------
    public static nuint ReadPtr(this IMemorySource mem, nuint address) =>
        mem.TryReadPtr(address, out var v) ? v : 0;

    public static int ReadI32(this IMemorySource mem, nuint address) =>
        mem.TryReadI32(address, out var v) ? v : 0;

    public static long ReadI64(this IMemorySource mem, nuint address)
    {
        var buf = new byte[8];
        return mem.Read(address, buf, 8) == 8 ? BitConverter.ToInt64(buf, 0) : 0;
    }

    public static byte ReadByte(this IMemorySource mem, nuint address) =>
        mem.TryReadByte(address, out var v) ? v : (byte)0;

    public static bool WriteI32(this IMemorySource mem, nuint address, int value) =>
        mem.Write(address, BitConverter.GetBytes(value));

    public static bool WriteByte(this IMemorySource mem, nuint address, byte value) =>
        mem.Write(address, new[] { value });

    public static bool WritePtr(this IMemorySource mem, nuint address, nuint value) =>
        mem.Write(address, BitConverter.GetBytes((ulong)value));

    // --- managed strings and arrays ---------------------------------------------
    /// <summary>Reads the UTF-16 contents of an <c>Il2CppString</c>. Empty on any bad read.</summary>
    public static string ReadManagedString(this IMemorySource mem, nuint stringObject)
    {
        if (stringObject == 0) return "";
        if (!mem.TryReadI32(stringObject + StringLengthOffset, out int len)) return "";
        if (len <= 0 || len > MaxStringLength) return "";
        var chars = new byte[len * 2];
        if (mem.Read(stringObject + StringCharsOffset, chars, chars.Length) != chars.Length) return "";
        return Encoding.Unicode.GetString(chars);
    }

    /// <summary>
    /// Reads a NUL-terminated ASCII string — the form IL2CPP uses for type names — and reports
    /// whether the read succeeded.
    ///
    /// <para>Distinguishing "read failed" from "the string is empty" matters: the game's own
    /// types live in the global namespace, so an empty namespace is the expected value. If a
    /// failed read also produced "", an unreadable candidate would sail through the namespace
    /// check and the validation would rest on the name alone.</para>
    ///
    /// <para>Read in 16-byte steps for speed — the class sweep calls this once per candidate,
    /// so a byte-at-a-time read would cost 16x the syscalls. When a step fails, fall back to
    /// reading that step one byte at a time: a name near the end of a committed region would
    /// make the wider read fail outright and lose a string that is really there.</para>
    /// </summary>
    public static bool TryReadNativeString(this IMemorySource mem, nuint address, out string text,
        int maxLength = MaxTypeNameLength)
    {
        text = "";
        if (address == 0) return false;

        const int step = 16;
        var chunk = new byte[step];
        var sb = new StringBuilder(maxLength);

        for (int offset = 0; offset < maxLength; offset += step)
        {
            int want = Math.Min(step, maxLength - offset);
            int got = mem.Read(address + (nuint)offset, chunk, want);
            if (got != want)
            {
                // Wide read failed — the string may still be readable up to a region edge.
                got = 0;
                var one = new byte[1];
                for (int i = 0; i < want; i++)
                {
                    if (mem.Read(address + (nuint)(offset + i), one, 1) != 1) break;
                    chunk[i] = one[0];
                    got = i + 1;
                }
                if (got == 0) return false;
            }

            for (int i = 0; i < got; i++)
            {
                byte b = chunk[i];
                if (b == 0) { text = sb.ToString(); return true; }
                if (b < 0x20 || b > 0x7E) return false;
                sb.Append((char)b);
            }

            if (got < want) return false;   // ran into an unreadable page before the terminator
        }

        // No terminator within the bound: not a type name.
        return false;
    }

    /// <summary>
    /// Reads a NUL-terminated ASCII string, returning "" for both an empty string and a failed
    /// read. Prefer <see cref="TryReadNativeString"/> wherever the difference matters.
    /// </summary>
    public static string ReadNativeString(this IMemorySource mem, nuint address, int maxLength = MaxTypeNameLength) =>
        mem.TryReadNativeString(address, out var text, maxLength) ? text : "";

    /// <summary>Element count of a managed array, or 0 when it is null or unreadable.</summary>
    public static int ReadArrayLength(this IMemorySource mem, nuint array) =>
        array == 0 ? 0 : mem.ReadI32(array + ArrayLengthOffset);

    /// <summary>Address of element <paramref name="index"/> in a managed byte array.</summary>
    public static nuint ByteArrayElement(nuint array, int index) =>
        array + (nuint)(ArrayHeaderSize + index);

    /// <summary>Reads a byte from a managed byte array at <paramref name="index"/>.</summary>
    public static byte ReadByteArrayElement(this IMemorySource mem, nuint array, int index) =>
        array == 0 ? (byte)0 : mem.ReadByte(ByteArrayElement(array, index));

    /// <summary>Writes a byte into a managed byte array at <paramref name="index"/>.</summary>
    public static bool WriteByteArrayElement(this IMemorySource mem, nuint array, int index, byte value) =>
        array != 0 && mem.WriteByte(ByteArrayElement(array, index), value);

    /// <summary>Reads up to <paramref name="count"/> bytes from a managed byte array.</summary>
    public static byte[] ReadByteArray(this IMemorySource mem, nuint array, int count)
    {
        if (array == 0 || count <= 0) return Array.Empty<byte>();
        int len = mem.ReadArrayLength(array);
        if (len < count) count = len;
        if (count <= 0) return Array.Empty<byte>();
        var buf = new byte[count];
        if (mem.Read(array + (nuint)ArrayHeaderSize, buf, count) != count) return Array.Empty<byte>();
        return buf;
    }

    /// <summary>Address of element <paramref name="index"/> in a managed array of references.</summary>
    public static nuint ArrayElement(nuint array, int index) =>
        array + (nuint)(ArrayHeaderSize + (long)index * 8);

    /// <summary>Reads reference element <paramref name="index"/> out of a managed array.</summary>
    public static nuint ReadArrayRef(this IMemorySource mem, nuint array, int index) =>
        array == 0 ? 0 : mem.ReadPtr(ArrayElement(array, index));

    // --- classes and statics ----------------------------------------------------
    /// <summary>The <c>Il2CppClass*</c> stored in an object's header.</summary>
    public static nuint ReadObjectClass(this IMemorySource mem, nuint obj) =>
        obj == 0 ? 0 : mem.ReadPtr(obj + ObjectClassOffset);

    /// <summary>
    /// True when <paramref name="klass"/> is a readable <c>Il2CppClass</c> whose name and
    /// namespace match. This is what makes a class pointer safe to trust: a stale RVA or a
    /// mis-scanned candidate almost never spells out the right type name.
    /// </summary>
    public static bool ClassMatches(this IMemorySource mem, nuint klass, string name, string namespaceName)
    {
        if (klass == 0) return false;
        if (!mem.TryReadPtr(klass + ClassNameOffset, out nuint namePtr)) return false;
        if (!mem.TryReadPtr(klass + ClassNamespaceOffset, out nuint nsPtr)) return false;
        if (!mem.TryReadNativeString(namePtr, out string actualName) || actualName != name) return false;
        return mem.TryReadNativeString(nsPtr, out string actualNs) && actualNs == namespaceName;
    }

    /// <summary>True when <paramref name="obj"/> is an instance of exactly <paramref name="klass"/>.</summary>
    public static bool IsInstanceOf(this IMemorySource mem, nuint obj, nuint klass) =>
        obj != 0 && klass != 0 && mem.ReadObjectClass(obj) == klass;

    /// <summary>The type's static-field storage block, or 0 when the class has none.</summary>
    public static nuint ReadStaticFields(this IMemorySource mem, nuint klass) =>
        klass == 0 ? 0 : mem.ReadPtr(klass + ClassStaticFieldsOffset);

    /// <summary>
    /// Follows class → <c>static_fields</c> → the reference stored at
    /// <paramref name="staticOffset"/>. That is how every <c>m_instance</c> singleton in the
    /// game is reached; the <c>m_instance</c> field is always first, at offset 0.
    /// </summary>
    public static nuint ReadStaticRef(this IMemorySource mem, nuint klass, int staticOffset = 0)
    {
        nuint statics = mem.ReadStaticFields(klass);
        return statics == 0 ? 0 : mem.ReadPtr(statics + (nuint)staticOffset);
    }

    // --- System.Collections.Generic.List<T> -------------------------------------
    /// <summary><c>_items</c> — the backing array.</summary>
    public const int ListItemsOffset = 0x10;

    /// <summary><c>_size</c> — how much of <c>_items</c> is in use.</summary>
    public const int ListSizeOffset = 0x18;

    /// <summary>The backing array of a <c>List&lt;T&gt;</c>.</summary>
    public static nuint ReadListItems(this IMemorySource mem, nuint list) =>
        list == 0 ? 0 : mem.ReadPtr(list + ListItemsOffset);

    /// <summary>Element count of a <c>List&lt;T&gt;</c> — <c>_size</c>, not the array's length.</summary>
    public static int ReadListCount(this IMemorySource mem, nuint list) =>
        list == 0 ? 0 : mem.ReadI32(list + ListSizeOffset);

    /// <summary>Reads reference element <paramref name="index"/> out of a <c>List&lt;T&gt;</c>.</summary>
    public static nuint ReadListRef(this IMemorySource mem, nuint list, int index)
    {
        nuint items = mem.ReadListItems(list);
        return items == 0 ? 0 : mem.ReadArrayRef(items, index);
    }
}
