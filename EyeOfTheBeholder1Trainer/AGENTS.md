# Eye of the Beholder I Trainer — Agent Notes

## Overview

Live-memory trainer for Eye of the Beholder (Westwood Studios / SSI, 1991), a first-person AD&D 2nd-edition dungeon crawl running under DOSBox / DOSBox-X. References `GameTrainers.Common` for both `Memory` (`ProcessMemory`/`MemoryRegion`) and `Mvvm` (`ObservableObject`/`RelayCommand`).

## Architecture

`src/EyeOfTheBeholder1Trainer/` (`net8.0-windows`, references `GameTrainers.Common`):

- **Game layer** — `CharacterFormat` (243-byte record offset table, lookup tables for races/classes/alignments/abilities), `CharacterRecord` (typed mutable view with ASCII name, ability setters that update modified+base together, signed AC, uint32 XP), `SpellBook` (46 spells: 23 cleric + 23 mage, levels 1–5), `GameFacts` (confirmed constants), `SaveFile` (offline `EOBDATA.SAV` reader/writer with backup).
- **Memory layer** — `PartyLocator` (structural scan: walks every readable region looking for six contiguous 243-byte records whose CharId matches slot index, active slots pass strict validation, empty slots are zeroed).
- **ViewModels** — `MainViewModel` (process attach/scan/poll/freeze), `CharacterViewModel` (per-character editable view with live-write-through), `SaveEditorViewModel` (offline save file editing), `ReferenceViewModel` (read-only spell/class/race/alignment tables), `NamedValueViewModel` (labelled integer rows), `ICharacterHost` (write channel).
- **UI** — `MainWindow.xaml` with three tabs: Party (live editing), Save Editor (offline), Reference (spell/class/race/alignment tables).

`test/FormatCheck/` (`net8.0-windows`): headless harness that validates format constants, lookup tables, spell book counts, character encode/decode, name round-trip, ability setters, signed AC, XP uint32 round-trip, save-file round-trip with synthetic fixture, and PartyLocator structural validation against a synthetic 2 MiB buffer.

## Character Record Format (243 bytes)

Derived from the ModdingWiki format specification, the Synalysis grammar file, and the EOB2 hex list by Marc Rene Delhalle (EOB1 and EOB2 share the same character structure), verified against the shipped `EOBDATA.SAV`.

Key offsets:
- `0x00` CharId, `0x01` Active, `0x02` Name (10 chars)
- `0x0D`–`0x1A` Six abilities as (modified, base) byte pairs + exceptional strength
- `0x1B`/`0x1C` HP current/max (uint8), `0x1D` AC (signed int8)
- `0x1F` Race (0–11), `0x20` Class (0–14), `0x21` Alignment (0–8)
- `0x23` Food %, `0x24`–`0x26` Levels (3 classes), `0x27`–`0x32` XP (3 × uint32 LE)
- `0x33`–`0x76` Spell data (68 bytes, round-tripped untouched)
- `0x77`–`0xF2` Equipment slots (round-tripped untouched)

## Party Locator

The roster's live address changes every DOSBox session. The locator uses a structural scan (no static anchor): walks every readable region looking for a window of six contiguous 243-byte records. Each slot's CharId must match its index, active characters must pass strict validation (name starts with a letter, abilities 3–25, HP max 1–255, race/class/alignment in range, level 1–40), and empty slots must be zeroed. At least one slot must be occupied.

## Save File

`EOBDATA.SAV` has no header: six 243-byte character records (1458 bytes) followed by game-state data (~31,643 bytes). The trainer reads and edits the character portion; the remainder is preserved byte-for-byte. A one-shot `.bak` is taken before the first write.

## Coding Conventions

C# targeting `net8.0-windows`, x64, `Nullable` and `ImplicitUsings` enabled. 4-space indent, file-scoped namespaces, `sealed` classes, PascalCase members, `_camelCase` fields, `const` hex for offsets. XML `///` docs on public members. Global usings bring `GameTrainers.Common.Memory` and `GameTrainers.Common.Mvvm` into scope via csproj `<Using>` items; `ObservableObject` uses `SetField`.

## RE and Strategy Guide

`docs/ReverseEngineering.md` and `docs/StrategyGuide.md` contain the full reverse-engineering analysis and a play/strategy guide with maps.
