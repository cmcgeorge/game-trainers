namespace LegendOfGrimrock1Trainer.Lua;

/// <summary>
/// Object layout of the LuaJIT 2.0.0-beta9 VM that <c>grimrock.exe</c> statically links, for a
/// 32-bit build with <c>LJ_GC64</c> off.
///
/// Every constant here is a property of LuaJIT itself, not of Legend of Grimrock: the same numbers
/// hold for any 32-bit LuaJIT 2.0 host. They are what lets the trainer read and write game state
/// without a single hard-coded game address — Grimrock keeps the party, the champions, their stats
/// and the dungeon in ordinary Lua tables (see <c>docs/ReverseEngineering.md</c>), so walking the
/// VM's own structures reaches all of it by name.
///
/// The exe exports the whole Lua C API (<c>lua_newstate</c>, <c>luaJIT_version_2_0_0_beta9</c>, …),
/// which is how the version was pinned; the field offsets below were then confirmed field-by-field
/// against the live process (a <c>GCstr</c> whose <c>len</c> and inline characters matched a
/// champion name, a <c>GCtab</c> whose hash part yielded the stat table, and so on).
/// </summary>
public static class LuaLayout
{
    // --- TValue -----------------------------------------------------------------------------------
    // A TValue is 8 bytes: { uint32 lo; uint32 it; } on little-endian. When `it` is below
    // ItNumberBoundary the whole 8 bytes are instead an IEEE-754 double (NaN boxing), so the type
    // test is "is the high word a tag?" rather than a switch.

    /// <summary>Size of one <c>TValue</c>.</summary>
    public const int TValueSize = 8;

    /// <summary>Offset of the payload word (a GC pointer, or the low half of a double).</summary>
    public const int TValueLo = 0;

    /// <summary>Offset of the type tag (or the high half of a double).</summary>
    public const int TValueIt = 4;

    /// <summary>
    /// Any tag word below this is the top half of a double. This is LuaJIT's own predicate
    /// (<c>tvisnum(o)</c> is <c>itype(o) &lt; LJ_TISNUM</c>, and <c>LJ_TISNUM</c> is
    /// <c>LJ_TNUMX == ~13u</c>), which matters at the edges: the lowest real tag is
    /// <see cref="ItUserData"/> at 0xFFFFFFF3, so choosing a lower boundary — 0xFFF00000, say — would
    /// classify negative infinity and every negative NaN as "some other object" instead of as a
    /// number. Grimrock does not store those in a stat, but a reader that disagrees with the VM about
    /// what a number is fails silently rather than loudly, so it matches the VM exactly.
    /// </summary>
    public const uint ItNumberBoundary = 0xFFFFFFF2;

    /// <summary><c>LJ_TNIL</c> (~0).</summary>
    public const uint ItNil = 0xFFFFFFFF;

    /// <summary><c>LJ_TFALSE</c> (~1).</summary>
    public const uint ItFalse = 0xFFFFFFFE;

    /// <summary><c>LJ_TTRUE</c> (~2).</summary>
    public const uint ItTrue = 0xFFFFFFFD;

    /// <summary><c>LJ_TLIGHTUD</c> (~3).</summary>
    public const uint ItLightUserData = 0xFFFFFFFC;

    /// <summary><c>LJ_TSTR</c> (~4).</summary>
    public const uint ItString = 0xFFFFFFFB;

    /// <summary><c>LJ_TUPVAL</c> (~5).</summary>
    public const uint ItUpValue = 0xFFFFFFFA;

    /// <summary><c>LJ_TTHREAD</c> (~6).</summary>
    public const uint ItThread = 0xFFFFFFF9;

    /// <summary><c>LJ_TPROTO</c> (~7).</summary>
    public const uint ItProto = 0xFFFFFFF8;

    /// <summary><c>LJ_TFUNC</c> (~8).</summary>
    public const uint ItFunction = 0xFFFFFFF7;

    /// <summary><c>LJ_TTRACE</c> (~9).</summary>
    public const uint ItTrace = 0xFFFFFFF6;

    /// <summary><c>LJ_TCDATA</c> (~10).</summary>
    public const uint ItCData = 0xFFFFFFF5;

    /// <summary><c>LJ_TTAB</c> (~11).</summary>
    public const uint ItTable = 0xFFFFFFF4;

    /// <summary><c>LJ_TUDATA</c> (~12).</summary>
    public const uint ItUserData = 0xFFFFFFF3;

    // --- GCobj headers ----------------------------------------------------------------------------
    // GCHeader is { GCRef nextgc; uint8 marked; uint8 gct; }. `gct` holds ~itype, so a string object
    // carries 4 and a table carries 11 — the single most useful discriminator when scanning a heap.

    /// <summary>Offset of the GC chain link in every collectable object.</summary>
    public const int GcNextGc = 0;

    /// <summary>Offset of the GC colour/flags byte.</summary>
    public const int GcMarked = 4;

    /// <summary>Offset of the <c>gct</c> discriminator byte.</summary>
    public const int GcType = 5;

    /// <summary><c>gct</c> of a <c>GCstr</c>.</summary>
    public const byte GcTypeString = 4;

    /// <summary><c>gct</c> of a <c>lua_State</c>.</summary>
    public const byte GcTypeThread = 6;

    /// <summary><c>gct</c> of a <c>GCfunc</c>.</summary>
    public const byte GcTypeFunction = 8;

    /// <summary><c>gct</c> of a <c>GCtab</c>.</summary>
    public const byte GcTypeTable = 11;

    /// <summary><c>gct</c> of a <c>GCudata</c>.</summary>
    public const byte GcTypeUserData = 12;

    // --- GCstr ------------------------------------------------------------------------------------
    // { GCHeader; uint8 reserved; uint8 unused; MSize hash; MSize len; } followed by the characters
    // and a NUL, inline in the same allocation. Strings are interned, so a given literal exists once.

    /// <summary>Offset of the interning hash.</summary>
    public const int StringHash = 8;

    /// <summary>Offset of the character count (excludes the trailing NUL).</summary>
    public const int StringLength = 12;

    /// <summary>Bytes before the inline character data.</summary>
    public const int StringHeaderSize = 16;

    // --- GCtab -----------------------------------------------------------------------------------
    // { GCHeader; uint8 nomm; int8 colo; MRef array; GCRef gclist; GCRef metatable; MRef node;
    //   uint32 asize; uint32 hmask; }
    // The array part holds keys 0..asize-1 as bare TValues; the hash part is hmask+1 Nodes.

    /// <summary>Offset of the pointer to the array part.</summary>
    public const int TableArray = 8;

    /// <summary>Offset of the metatable reference.</summary>
    public const int TableMetatable = 16;

    /// <summary>Offset of the pointer to the hash part.</summary>
    public const int TableNode = 20;

    /// <summary>Offset of the array-part length.</summary>
    public const int TableArraySize = 24;

    /// <summary>Offset of the hash mask; the hash part holds <c>hmask + 1</c> nodes.</summary>
    public const int TableHashMask = 28;

    /// <summary>Size of a <c>GCtab</c>.</summary>
    public const int TableSize = 32;

    // --- Node -------------------------------------------------------------------------------------
    // { TValue val; TValue key; MRef next; MRef freetop; } — value first, which is why a write only
    // needs the node address.

    /// <summary>Offset of the value inside a hash node.</summary>
    public const int NodeValue = 0;

    /// <summary>Offset of the key inside a hash node.</summary>
    public const int NodeKey = 8;

    /// <summary>Size of one hash node.</summary>
    public const int NodeSize = 24;

    // --- lua_State --------------------------------------------------------------------------------
    // { GCHeader; uint8 dummy_ffid; uint8 status; MRef glref; GCRef gclist; TValue *base; TValue *top;
    //   MRef maxstack; MRef stack; GCRef openupval; GCRef env; void *cframe; MSize stacksize; }

    /// <summary>Offset of <c>dummy_ffid</c>, which LuaJIT always initialises to <c>FF_C</c> (1).</summary>
    public const int StateDummyFfid = 6;

    /// <summary>Offset of <c>status</c>.</summary>
    public const int StateStatus = 7;

    /// <summary>Offset of the link to the <c>global_State</c>.</summary>
    public const int StateGlobalRef = 8;

    /// <summary>Offset of the current frame base.</summary>
    public const int StateBase = 16;

    /// <summary>Offset of the stack top.</summary>
    public const int StateTop = 20;

    /// <summary>Offset of the stack ceiling.</summary>
    public const int StateMaxStack = 24;

    /// <summary>Offset of the stack base.</summary>
    public const int StateStack = 28;

    /// <summary>Offset of the thread environment — for the main thread this is the globals table.</summary>
    public const int StateEnv = 36;

    /// <summary>Offset of the stack size in slots.</summary>
    public const int StateStackSize = 44;

    /// <summary>Size of a <c>lua_State</c>.</summary>
    public const int StateSize = 48;

    /// <summary>
    /// LuaJIT allocates the main thread and the global state together as one <c>GG_State</c>
    /// (<c>{ lua_State L; global_State g; … }</c>), so for the main thread — and only for the main
    /// thread — <c>glref == (char *)L + sizeof(lua_State)</c>. That single equality is what turns a
    /// heap scan for "some thread object" into a scan for "the VM", and it is why the locator does
    /// not need a hard-coded address to work on an unknown build.
    /// </summary>
    public const int MainThreadGlobalStateDelta = StateSize;

    /// <summary><c>FF_C</c>, the value LuaJIT stores in <c>dummy_ffid</c> for every thread.</summary>
    public const byte FastFunctionC = 1;

    /// <summary>Highest legal <c>lua_State.status</c> (<c>LUA_ERRERR</c>).</summary>
    public const byte MaxThreadStatus = 6;

    /// <summary>Longest string this reader will materialise; guards against a bogus <c>len</c>.</summary>
    public const int MaxStringLength = 4096;

    /// <summary>Largest table this reader will walk; guards against a bogus <c>asize</c>/<c>hmask</c>.</summary>
    public const int MaxTableEntries = 65536;
}
