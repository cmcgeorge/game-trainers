using System.Text;
using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// Layout of the Unity IL2CPP runtime structures the trainer walks, plus the typed
/// reads/writes that go with them.
///
/// <para>The remaster is a 64-bit IL2CPP build (Unity 2018.4), so every managed object
/// on the heap starts with a 16-byte header — an <c>Il2CppClass*</c> then a monitor slot —
/// and the first instance field sits at +0x10. Managed arrays add a bounds pointer and a
/// length before their elements, putting element 0 at +0x20.</para>
///
/// <para>A static field is reached through the type's <c>Il2CppClass</c>: the generated code
/// does <c>mov rax,[rip+slot]; mov rcx,[rax+0xB8]; mov rcx,[rcx]</c>, i.e. load the class
/// pointer from a metadata-usage slot in <c>GameAssembly.dll</c>'s data section, follow
/// <c>static_fields</c>, then index the static storage. All three steps are modelled here.</para>
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
    /// [Confirmed] by the generated code for every <c>Instance</c> singleton access
    /// (<c>mov rcx,[rax+0xB8]</c> immediately after loading the class pointer).
    /// </summary>
    public const int ClassStaticFieldsOffset = 0xB8;

    /// <summary>Longest type name the validator will accept when probing a class pointer.</summary>
    private const int MaxTypeNameLength = 64;

    /// <summary>An IL2CPP string longer than this is treated as a bad read, not a name.</summary>
    private const int MaxStringLength = 512;

    // --- primitive reads --------------------------------------------------------
    public static nuint ReadPtr(this IMemorySource mem, nuint address)
    {
        var buf = new byte[8];
        return mem.Read(address, buf, 8) == 8 ? (nuint)BitConverter.ToUInt64(buf, 0) : 0;
    }

    public static int ReadI32(this IMemorySource mem, nuint address)
    {
        var buf = new byte[4];
        return mem.Read(address, buf, 4) == 4 ? BitConverter.ToInt32(buf, 0) : 0;
    }

    public static long ReadI64(this IMemorySource mem, nuint address)
    {
        var buf = new byte[8];
        return mem.Read(address, buf, 8) == 8 ? BitConverter.ToInt64(buf, 0) : 0;
    }

    public static bool ReadBool(this IMemorySource mem, nuint address)
    {
        var buf = new byte[1];
        return mem.Read(address, buf, 1) == 1 && buf[0] != 0;
    }

    public static bool WriteI32(this IMemorySource mem, nuint address, int value) =>
        mem.Write(address, BitConverter.GetBytes(value));

    public static bool WriteI64(this IMemorySource mem, nuint address, long value) =>
        mem.Write(address, BitConverter.GetBytes(value));

    public static bool WriteBool(this IMemorySource mem, nuint address, bool value) =>
        mem.Write(address, new[] { value ? (byte)1 : (byte)0 });

    public static bool WritePtr(this IMemorySource mem, nuint address, nuint value) =>
        mem.Write(address, BitConverter.GetBytes((ulong)value));

    // --- managed strings and arrays ---------------------------------------------
    /// <summary>Reads the UTF-16 contents of an <c>Il2CppString</c>. Empty on any bad read.</summary>
    public static string ReadManagedString(this IMemorySource mem, nuint stringObject)
    {
        if (stringObject == 0) return "";
        int len = mem.ReadI32(stringObject + StringLengthOffset);
        if (len <= 0 || len > MaxStringLength) return "";
        var chars = new byte[len * 2];
        if (mem.Read(stringObject + StringCharsOffset, chars, chars.Length) != chars.Length) return "";
        return Encoding.Unicode.GetString(chars);
    }

    /// <summary>
    /// Reads a NUL-terminated ASCII string — the form IL2CPP uses for type names. Read in
    /// small steps rather than one long grab, because a string near the end of a committed
    /// region would make an over-long read fail outright and lose a name that is really there.
    /// </summary>
    public static string ReadNativeString(this IMemorySource mem, nuint address, int maxLength = MaxTypeNameLength)
    {
        if (address == 0) return "";

        const int step = 16;
        var chunk = new byte[step];
        var text = new StringBuilder(maxLength);

        for (int offset = 0; offset < maxLength; offset += step)
        {
            if (mem.Read(address + (nuint)offset, chunk, step) != step) return "";
            for (int i = 0; i < step; i++)
            {
                byte b = chunk[i];
                if (b == 0) return text.ToString();
                if (b < 0x20 || b > 0x7E) return "";   // not a plausible identifier
                if (text.Length >= maxLength) return "";
                text.Append((char)b);
            }
        }
        return "";   // no terminator within the bound: not a type name
    }

    /// <summary>Element count of a managed array, or 0 when it is null or unreadable.</summary>
    public static int ReadArrayLength(this IMemorySource mem, nuint array) =>
        array == 0 ? 0 : mem.ReadI32(array + ArrayLengthOffset);

    /// <summary>Address of element <paramref name="index"/> in a managed array of references.</summary>
    public static nuint ArrayElement(nuint array, int index) =>
        array + (nuint)(ArrayHeaderSize + index * 8);

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
        if (mem.ReadNativeString(mem.ReadPtr(klass + ClassNameOffset)) != name) return false;
        return mem.ReadNativeString(mem.ReadPtr(klass + ClassNamespaceOffset)) == namespaceName;
    }

    /// <summary>The type's static-field storage block, or 0 when the class has none.</summary>
    public static nuint ReadStaticFields(this IMemorySource mem, nuint klass) =>
        klass == 0 ? 0 : mem.ReadPtr(klass + ClassStaticFieldsOffset);

    /// <summary>
    /// Follows class → <c>static_fields</c> → the reference stored at
    /// <paramref name="staticOffset"/>. That is how every <c>Instance</c> singleton in the
    /// game is reached; the <c>Instance</c> field is always first, at offset 0.
    /// </summary>
    public static nuint ReadStaticRef(this IMemorySource mem, nuint klass, int staticOffset = 0)
    {
        nuint statics = mem.ReadStaticFields(klass);
        return statics == 0 ? 0 : mem.ReadPtr(statics + (nuint)staticOffset);
    }

    /// <summary>Reads a static <c>int</c> (e.g. <c>GlobalMaps.m_gameChapter</c>).</summary>
    public static int ReadStaticI32(this IMemorySource mem, nuint klass, int staticOffset)
    {
        nuint statics = mem.ReadStaticFields(klass);
        return statics == 0 ? 0 : mem.ReadI32(statics + (nuint)staticOffset);
    }

    // --- System.Collections.Generic.List<T> -------------------------------------
    /// <summary><c>_items</c> — the backing array. Never null; an empty list shares a zero-length one.</summary>
    public const int ListItemsOffset = 0x10;

    /// <summary><c>_size</c> — how much of <c>_items</c> is in use.</summary>
    public const int ListSizeOffset = 0x18;

    /// <summary><c>_version</c> — bumped on every mutation so live enumerators fail fast.</summary>
    public const int ListVersionOffset = 0x1C;

    /// <summary>The backing array of a <c>List&lt;T&gt;</c>.</summary>
    public static nuint ReadListItems(this IMemorySource mem, nuint list) =>
        list == 0 ? 0 : mem.ReadPtr(list + ListItemsOffset);

    /// <summary>Element count of a <c>List&lt;T&gt;</c> — <c>_size</c>, not the array's length.</summary>
    public static int ReadListCount(this IMemorySource mem, nuint list) =>
        list == 0 ? 0 : mem.ReadI32(list + ListSizeOffset);

    /// <summary>How many more elements fit before the backing array has to grow.</summary>
    public static int ReadListSpare(this IMemorySource mem, nuint list)
    {
        if (list == 0) return 0;
        return mem.ReadArrayLength(mem.ReadListItems(list)) - mem.ReadListCount(list);
    }

    /// <summary>
    /// Reads a <c>List&lt;T&gt;</c> of 4-byte values — an <c>int</c> list, or any enum backed by
    /// one, which is what the game's <c>List&lt;Spell&gt;</c> is.
    /// </summary>
    public static int[] ReadListInt32(this IMemorySource mem, nuint list, int sanityLimit = 4096)
    {
        nuint items = mem.ReadListItems(list);
        int count = mem.ReadListCount(list);
        if (count <= 0 || count > sanityLimit) return Array.Empty<int>();
        if (count > mem.ReadArrayLength(items)) return Array.Empty<int>();   // torn read

        var values = new int[count];
        for (int i = 0; i < count; i++)
            values[i] = mem.ReadI32(items + (nuint)(ArrayHeaderSize + i * 4));
        return values;
    }

    /// <summary>
    /// Appends to a <c>List&lt;int&gt;</c> without allocating, which is only possible while the
    /// backing array still has room. Mirrors what <c>List&lt;T&gt;.Add</c> compiles to: store at
    /// <c>_items[_size]</c>, then bump <c>_size</c> and <c>_version</c>.
    ///
    /// <para>The element is written before the count so the game never sees a list whose
    /// <c>_size</c> covers a slot that has not been filled in yet.</para>
    /// </summary>
    public static bool TryAppendInt32(this IMemorySource mem, nuint list, int value)
    {
        if (list == 0) return false;

        nuint items = mem.ReadListItems(list);
        int count = mem.ReadListCount(list);
        int capacity = mem.ReadArrayLength(items);
        if (items == 0 || count < 0 || count >= capacity) return false;

        if (!mem.WriteI32(items + (nuint)(ArrayHeaderSize + count * 4), value)) return false;
        if (!mem.WriteI32(list + ListSizeOffset, count + 1)) return false;

        mem.WriteI32(list + ListVersionOffset, mem.ReadI32(list + ListVersionOffset) + 1);
        return true;
    }

    /// <summary>
    /// Removes the first occurrence of <paramref name="value"/> from a <c>List&lt;int&gt;</c> by
    /// shifting the tail down, the same way <c>List&lt;T&gt;.RemoveAt</c> does. Used to take a
    /// granted spell back off a character.
    /// </summary>
    public static bool TryRemoveInt32(this IMemorySource mem, nuint list, int value)
    {
        var values = mem.ReadListInt32(list);
        int index = Array.IndexOf(values, value);
        if (index < 0) return false;

        nuint items = mem.ReadListItems(list);
        for (int i = index; i < values.Length - 1; i++)
        {
            if (!mem.WriteI32(items + (nuint)(ArrayHeaderSize + i * 4), values[i + 1])) return false;
        }

        if (!mem.WriteI32(list + ListSizeOffset, values.Length - 1)) return false;
        mem.WriteI32(list + ListVersionOffset, mem.ReadI32(list + ListVersionOffset) + 1);
        return true;
    }
}
