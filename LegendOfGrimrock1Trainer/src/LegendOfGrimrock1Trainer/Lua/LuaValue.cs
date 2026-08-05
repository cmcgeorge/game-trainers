using System.Globalization;

namespace LegendOfGrimrock1Trainer.Lua;

/// <summary>The Lua type of a <see cref="LuaValue"/>, collapsed to what this trainer cares about.</summary>
public enum LuaKind
{
    /// <summary>The slot could not be read at all (unreadable page, short read).</summary>
    Unreadable,

    /// <summary>Lua <c>nil</c>.</summary>
    Nil,

    /// <summary>Lua <c>false</c> or <c>true</c>.</summary>
    Boolean,

    /// <summary>A Lua number (always an IEEE-754 double in LuaJIT 2.0).</summary>
    Number,

    /// <summary>An interned <c>GCstr</c>.</summary>
    String,

    /// <summary>A <c>GCtab</c>.</summary>
    Table,

    /// <summary>A Lua or C function.</summary>
    Function,

    /// <summary>Full userdata — Grimrock uses these to hand C++ objects to Lua.</summary>
    UserData,

    /// <summary>Anything else LuaJIT can tag (thread, proto, cdata, light userdata, trace).</summary>
    Other,
}

/// <summary>
/// One <c>TValue</c> read out of the target, together with the address it was read from.
///
/// <see cref="Slot"/> is the point of this type: a numeric field can be written back by putting
/// eight bytes at that address, which is how every edit in this trainer is applied. Nothing is
/// cached across a refresh — LuaJIT's collector never moves objects, but growing a table rehashes
/// its node array, so slot addresses are treated as valid only for the tick that produced them.
/// </summary>
public readonly record struct LuaValue(LuaKind Kind, uint Slot, double Number, uint Reference)
{
    /// <summary>A value that could not be read.</summary>
    public static LuaValue Unreadable(uint slot) => new(LuaKind.Unreadable, slot, 0, 0);

    /// <summary>Whether this is a number.</summary>
    public bool IsNumber => Kind == LuaKind.Number;

    /// <summary>Whether this is a table (and therefore has a usable <see cref="Reference"/>).</summary>
    public bool IsTable => Kind == LuaKind.Table;

    /// <summary>Whether this is a string (and therefore has a usable <see cref="Reference"/>).</summary>
    public bool IsString => Kind == LuaKind.String;

    /// <summary>Number value, or <paramref name="fallback"/> when this is not a number.</summary>
    public double AsNumber(double fallback = 0) => Kind == LuaKind.Number ? Number : fallback;

    /// <summary>Number value rounded to the nearest <see cref="int"/>, or <paramref name="fallback"/>.</summary>
    public int AsInt(int fallback = 0)
    {
        if (Kind != LuaKind.Number || double.IsNaN(Number)) return fallback;
        double r = Math.Round(Number, MidpointRounding.AwayFromZero);
        if (r > int.MaxValue) return int.MaxValue;
        if (r < int.MinValue) return int.MinValue;
        return (int)r;
    }

    /// <summary>Boolean value, or <paramref name="fallback"/> when this is not a boolean.</summary>
    public bool AsBool(bool fallback = false) => Kind == LuaKind.Boolean ? Number != 0 : fallback;

    /// <summary>Parses the eight bytes at <paramref name="offset"/> as a <c>TValue</c>.</summary>
    public static LuaValue Parse(ReadOnlySpan<byte> buffer, int offset, uint slot)
    {
        if (offset < 0 || offset + LuaLayout.TValueSize > buffer.Length) return Unreadable(slot);

        uint lo = BitConverter.ToUInt32(buffer.Slice(offset + LuaLayout.TValueLo, 4));
        uint it = BitConverter.ToUInt32(buffer.Slice(offset + LuaLayout.TValueIt, 4));

        if (it < LuaLayout.ItNumberBoundary)
            return new LuaValue(LuaKind.Number, slot, BitConverter.ToDouble(buffer.Slice(offset, 8)), 0);

        return it switch
        {
            LuaLayout.ItNil => new LuaValue(LuaKind.Nil, slot, 0, 0),
            LuaLayout.ItFalse => new LuaValue(LuaKind.Boolean, slot, 0, 0),
            LuaLayout.ItTrue => new LuaValue(LuaKind.Boolean, slot, 1, 0),
            LuaLayout.ItString => new LuaValue(LuaKind.String, slot, 0, lo),
            LuaLayout.ItTable => new LuaValue(LuaKind.Table, slot, 0, lo),
            LuaLayout.ItFunction => new LuaValue(LuaKind.Function, slot, 0, lo),
            LuaLayout.ItUserData => new LuaValue(LuaKind.UserData, slot, 0, lo),
            _ => new LuaValue(LuaKind.Other, slot, 0, lo),
        };
    }

    /// <summary>Short debug rendering; strings show as their address because resolving needs a reader.</summary>
    public override string ToString() => Kind switch
    {
        LuaKind.Number => Number.ToString("0.####", CultureInfo.InvariantCulture),
        LuaKind.Boolean => Number != 0 ? "true" : "false",
        LuaKind.Nil => "nil",
        LuaKind.Unreadable => "<unreadable>",
        _ => $"<{Kind.ToString().ToLowerInvariant()} {Reference:x8}>",
    };
}
