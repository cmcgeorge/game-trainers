# HillsfarTrainer — notes for agents

Read this before changing anything here. Most of it is about traps that cost real time to find.

## The target in one paragraph

*Hillsfar* (SSI / Westwood, 1989, build `v1.2`) is a DOS program run under DOSBox. `MAIN.EXE` is
**double-packed** — a SEA-AXE 12-bit LZW + `F0`-escape RLE stub wrapping an **EXEPACK** image — so
a `strings` pass or a Ghidra load of the shipped file recovers nothing useful. Unpacked, it is an
ordinary Microsoft C 1988 build with a **single data group at load segment + `0x277A`**, which means
every global sits at a constant `DGROUP` offset and **no value scanner is needed anywhere**. The whole
of game state that matters is one 188-byte character record at `DGROUP:0x094C`.

Full teardown, including both decompressors, in `docs/ReverseEngineering.md`.

## Things that will bite you

**Anchors must be raw bytes, not decoded text.** Most of the game's strings are digraph-compressed
(see `TextCodec`), so `"Temple of Tempus"` does not occur anywhere in memory. Every literal in
`CharacterFormat.PrimaryAnchor`/`Validators` was sliced out of the unpacked image. If you add one, take
it from the image, not from a decoded string dump.

**`TextCodec.ShippedTable` is a byte array on purpose.** Its 144th byte is `0x80`, which is not a
character. It was originally written as a string literal, an invisible U+0080 crept in, and
`Encoding.ASCII` silently turned it into `'?'` — so the constant read as 144 bytes, passed every
length assertion, and yet never compared equal to the table read from a live game. `FormatCheck`
now pins the final byte explicitly. Do not "tidy" it back into a string.

**Do not add a structural fallback to the locator.** The record has a printable name and plausible
attribute bytes; run against a process that is not the game, that shape will eventually match some
unrelated byte run in 16 MB of guest RAM, and a confident wrong address turns one "Max everything"
click into a write into another program's memory. Five candidate literals inside one 45 KB segment,
of which `MinValidators` = 2 plus the anchor must match (so three of five at minimum, four of five
in practice, and the actual ratio is reported to the user), is the stronger evidence. If a build
moves them, "not found" is the correct answer — and `Locate` additionally compares the game's
digraph table against the shipped one and warns when it differs, because a different release could
have moved the record offsets too.

**Keep "game not found" and "no character loaded" distinct.** `LocateResult.RejectedAddress` /
`AnchorsMatchedButRecordDidNot` exist because the advice differs completely: one means "check the game
is running", the other means "load a character at the camp menu". An **unreadable** record window must
count as neither — that address simply is not the game. `GameLocator.ReadRecord`'s `readable` out
parameter is what keeps those three cases apart.

**Write narrowly.** `CharacterRecord`'s setters report the exact byte range they touched through a
`flush` delegate, and the shell writes only those 1–4 bytes. That is not an optimisation: the record
sits next to the clock and its eighteen per-hour countdown timers, which the game rewrites constantly,
so whole-record writes would fight it. `FormatCheck` asserts the flush offset and length of every
setter from a table, and a coverage check forces that table to name every mutable property — so a new
setter cannot be added without pinning its range. (`HitPointsMax` flushes two ranges, because lowering
the maximum has to bring the current total down with it.)

**A successful read is not proof the address is still the game.** `PollTick` re-runs
`LooksLikeRecord` on every tick and drops the address when it fails. DOSBox allocates its guest RAM
once for the emulator's lifetime, so restarting `MAIN.EXE` relocates `DGROUP` while leaving the old
host memory mapped and readable — a read-success test alone would keep succeeding while every edit
and freeze write went to a stale address inside the guest. Do not "optimise" that check away.

**`ICharacterHost.WriteBytes` takes the caller's `DgroupBase`.** The shell must not substitute its
own current address: after a re-locate the two can differ, and a write raised by the previous
view-model must not land in the new one's segment.

**Freeze compares against the game, not against a shadow.** `CharacterViewModel.OnPolled` re-writes a
pinned value only when `LiveBuffer` disagrees with it. Comparing against a locally-cached copy would
be wrong for a freeze — the cache can already hold the pinned value while the game has moved on.

**The freeze path is the one write that bypasses `CharacterRecord`, so `FreezeTarget` carries its own
`Min`/`Max`.** Every entry is seeded from the character's current value and clamps on the way in.
Both matter: an unseeded entry defaulted to 0, so merely *ticking* the hit-points box pinned the
character to zero HP and killed it, and an unclamped one could pin the hour byte outside 1..24 — a
value `LooksLikeRecord` then rejects, making the trainer unable to find the character it just broke.

**`Name`'s setter rewrites the whole 16-byte field.** The game builds the save filename from the raw
leading bytes and ignores the NUL terminator: overwriting `Christopher` with `ZZTOP` and leaving the
tail intact made the game write `ZZTOPOPH.HIL`. Observed, not theorised.

**The class mask is authoritative, the class index is not.** `DGROUP:0x0981` (record `+0x35`) is what
the code tests — 45 `test byte` sites — and it stores the mask in **both nibbles**. The index at
`+0x24` is a creation-menu index mapped through a 16-byte table that has no slot for Magic-User/Thief.
`CharacterRecord.ClassMask` sets both and refuses an illegal mask; never write the index alone.

**Never invent lock-pick shapes.** The four shape bytes per slot decide which tumblers a pick fits.
`LockPickSet.RepairAll` only touches the condition byte of slots that already have shape data, and
`RepairPicksCommand` explains itself when there is nothing to repair rather than silently doing
nothing.

## What is Confirmed, and what is not

`docs/ReverseEngineering.md` marks every field. The short version: **everything the trainer writes is
Confirmed** by putting a sentinel into the running game and reading it back off the game's own screen.
Do not promote an Inferred field to an editable one without doing the same.

Still open, and deliberately not exposed: the 32-bit counter at `+0x00` (the game maintains it; a
near-total rewrite of the record did not change it, so it is not a content checksum), the flag byte at
`+0x45`, `+0x22` (read as `HPmax − this` in the arena/damage path), and several state bytes. The three
thief-skill bytes at `+0x32` are Inferred: `CharacterRecord` decodes them and round-trips them, but no
view-model or XAML exposes them, and that is deliberate — do not add an edit box for a field no live
write test has confirmed. The twelve `Q*.BIN` quest scripts are identified but not decoded, so there
is no mission-progress editing.

## The file format is genuinely trivial

`.HIL` and `.PRE` are a **raw dump of the same 188 bytes** — no header, no checksum, no encryption.
Confirmed three ways: memory matched the file byte-for-byte; a game-written save matched edited memory
exactly, carrying bytes `0x00`–`0x03` across unchanged; and a disk-edited file loaded with every value
on the character sheet. That is why offline editing is safe. Keep it that way: round-trip anything the
trainer does not understand, and keep the one-shot `.bak`.

## Build and verify

```powershell
.\Run.ps1 -Test -NoRun      # 2,058 checks with the .HIL/.PRE corpus, 1,991 without
```

The corpus group is **skipped with a note**, never failed, when the copyrighted files are absent — put
a copy in `.game\` (git-ignored), or point `HILLSFAR_DIR` at your install, to run it. There are no
machine-specific paths in either the trainer or the harness; `HILLSFAR_DIR` is the only override, and
the trainer honours it too when guessing the character-file folder. Keep the harness green; it is the
only automated check, since the GUI needs an interactive desktop and a running game.

**One headless check stands in for the UI: `BindabilityChecks`.** WPF resolves binding paths through
property descriptors only, so a `ValueTuple` in a bound collection renders every cell blank with
nothing but a debug-output warning — which no harness that never builds the XAML can see. That
happened once here (the Overland tab). The group asserts, via `TypeDescriptor`, that every type the
XAML puts in an `ItemsSource` exposes real properties. **Use a `record`, never a tuple, for anything
bound.**

When you change a locator anchor, a record offset or the clock arithmetic, **re-verify against the live
game**, not just the harness. The harness proves the code is self-consistent; only the game proves the
offsets are right.
