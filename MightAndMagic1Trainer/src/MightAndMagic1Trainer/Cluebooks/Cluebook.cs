using System.IO;
using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.Cluebooks;

/// <summary>What to put in a cluebook.</summary>
public sealed class CluebookOptions
{
    /// <summary>Draw each location's walls as a plan. This is most of the page's size.</summary>
    public bool IncludePlans { get; init; } = true;

    /// <summary>
    /// Include what each location says — every sign, hint and prompt in its overlay. Needs the
    /// player's own installation; without one there is nothing to include.
    /// </summary>
    public bool IncludeEventText { get; init; } = true;

    /// <summary>Include the 255-entry item table.</summary>
    public bool IncludeItems { get; init; } = true;

    /// <summary>Include the 195-entry bestiary.</summary>
    public bool IncludeBestiary { get; init; } = true;

    /// <summary>Include both spell lists.</summary>
    public bool IncludeSpells { get; init; } = true;

    /// <summary>Include the walkthrough.</summary>
    public bool IncludeWalkthrough { get; init; } = true;

    /// <summary>Include the classes, the levelling rules and the combat maths.</summary>
    public bool IncludeRules { get; init; } = true;

    /// <summary>Pixels per square in a rendered plan.</summary>
    public int PlanCellSize { get; init; } = 30;
}

/// <summary>A chapter of the gazetteer: one of the game's 55 places, its walls, and its words.</summary>
public sealed record LocationChapter
{
    /// <summary>The maze record — the walls, doors and secret passages.</summary>
    public required MazeMap Maze { get; init; }

    /// <summary>What the place is, or null when the record's name is not one of the 55 known ones.</summary>
    public required Place? Place { get; init; }

    /// <summary>The location's overlay, when the player's installation was read.</summary>
    public required Overlay? Overlay { get; init; }

    /// <summary>What the walls are made of.</summary>
    public required WallCounts Stats { get; init; }

    /// <summary>The squares worth walking to, in the order the landmark list gives them.</summary>
    public required IReadOnlyList<Landmark> Landmarks { get; init; }

    /// <summary>
    /// Every wall here you can walk straight through, as the square to walk out of and the way to go.
    ///
    /// Computed from the maze itself rather than quoted from anybody, which is what makes this the
    /// most trustworthy annotation in the book -- and, for a game whose best rewards are behind walls
    /// that are not there, the most useful.
    /// </summary>
    public required IReadOnlyList<(int X, int Y, int Dir)> SecretPassages { get; init; }

    /// <summary>
    /// Whether this place's walk-through-able walls are scenery rather than secrets.
    ///
    /// <b>Outdoors they are terrain.</b> A surface area draws scrub, trees and the edge of a wood as
    /// walls and lets you walk through them, which is why the sixteen overworld maps have between 89
    /// and 257 of them while a town has thirty. Listing those as secret passages would bury the real
    /// ones under a page of scenery and teach a reader to ignore the list — so outdoors they are
    /// counted and explained instead of enumerated.
    /// </summary>
    public bool PassagesAreTerrain => Maze.IsOutdoor;

    /// <summary>
    /// The ways out of a marked square that go through a drawn wall, or nothing.
    ///
    /// <para>This is the one place where the two halves of the annotation can check each other. A
    /// landmark is a coordinate somebody published; a walk-through wall is computed from the maze
    /// data. Where a landmark that is <i>described</i> as being behind a secret wall lands on a
    /// square this finds one on — Sorpigal's leprechaun and Portsmith's secret room both do, and
    /// neither does when the coordinate is mirrored top to bottom — the two sources have
    /// corroborated each other, and the reader is told how to get in.</para>
    ///
    /// <para>Only asked indoors. Outdoors a walk-through edge is scenery, so it would corroborate
    /// nothing: see <see cref="PassagesAreTerrain"/>.</para>
    /// </summary>
    public IReadOnlyList<string> WayInAt(int x, int y)
    {
        if (PassagesAreTerrain) return Array.Empty<string>();

        return Maze.WalkThroughSides(x, y).Select(MazeMap.DirectionName).ToList();
    }

    /// <summary>The landmarks as plan markers, numbered from one.</summary>
    public IReadOnlyList<PlanMarker> Markers =>
        Landmarks.Select((l, i) => new PlanMarker(l.X, l.Y, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), l.Name))
                 .ToList();

    /// <summary>The engine's own name for the place, e.g. <c>qvl1</c>.</summary>
    public string RawName => Maze.RawName;

    /// <summary>The record's index, which is also its position in <c>Mazedata.dta</c>.</summary>
    public int Index => Maze.Index;

    /// <summary>
    /// The place's name without the raw name trailing it.
    ///
    /// <see cref="MazeMap.DisplayName"/> appends "(sorpigal)" so that a map picker is never
    /// ambiguous; a cluebook has a whole chapter in which to say the raw name once.
    /// </summary>
    public string Name
    {
        get
        {
            string suffix = $"  ({RawName})";
            string display = Maze.DisplayName;
            return display.EndsWith(suffix, StringComparison.Ordinal)
                ? display[..^suffix.Length]
                : display;
        }
    }

    /// <summary>How firmly this record is tied to that place.</summary>
    public string Confidence => Place?.Confidence ?? "Uncertain";

    /// <summary>What a reader should expect to find here.</summary>
    public string Blurb => Place?.Blurb ?? "";

    /// <summary>Which chapter of the gazetteer it belongs in.</summary>
    public PlaceKind Kind => Place?.Kind ?? PlaceKind.Beyond;

    /// <summary>What the location says, or an empty list when its overlay was not read.</summary>
    public IReadOnlyList<OverlayMessage> Messages => Overlay?.Messages ?? Array.Empty<OverlayMessage>();
}

/// <summary>One fragment of the endgame's two ciphers, and the text of it if the player's files held it.</summary>
/// <param name="Fragment">Which fragment, and where it lives.</param>
/// <param name="Place">The place that holds it, when the name is a known one.</param>
/// <param name="Message">The message itself, when that overlay was read.</param>
public sealed record FoundFragment(PuzzleTrail.Fragment Fragment, Place? Place, OverlayMessage? Message);

/// <summary>
/// Where a cluebook's facts come from: the trainer's own decoded reference always, and the player's
/// installation when they have pointed at one.
///
/// <para><b>The split is the whole design.</b> Everything the trainer ships — the 55 wall layouts,
/// the item and monster tables, the spells, the walkthrough — is this project's own decode, so a
/// cluebook can be written with no game files at all. The one thing that cannot be shipped is the
/// game's own words, and those are read out of the installation the player already owns, exactly as
/// the drawn map reads their <c>Mazedata.dta</c>.</para>
/// </summary>
public sealed class CluebookSources
{
    /// <summary>The mazes: the player's own file when there is one, the bundled layouts otherwise.</summary>
    public required MazeData Mazes { get; init; }

    /// <summary>The <c>Mazedata.dta</c> the mazes came from, or the empty string for the bundled set.</summary>
    public string MazeFile { get; init; } = "";

    /// <summary>The overlays read from the installation, or null when there is no folder.</summary>
    public OverlaySet? Overlays { get; init; }

    /// <summary>The folder the game files were read from, or the empty string.</summary>
    public string GameFolder { get; init; } = "";

    /// <summary>Just the bundled reference: no game folder, no overlays, no location text.</summary>
    public static CluebookSources Bundled() => new() { Mazes = MazeData.BuiltIn() };

    /// <summary>
    /// Reads what a Might &amp; Magic 1 folder has to offer.
    ///
    /// A missing or unreadable <c>Mazedata.dta</c> is not a failure: the bundled layouts draw the
    /// same walls, and the point of the folder is the overlays. <paramref name="detail"/> says what
    /// was found either way, because a cluebook whose location chapters are silent should say
    /// whether that is because the files were absent or because they were not understood.
    /// </summary>
    public static CluebookSources FromFolder(string folder, out string detail)
    {
        var bundled = MazeData.BuiltIn();

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            detail = folder.Length == 0
                ? "No game folder given — writing from the bundled reference only."
                : $"{folder} is not a folder — writing from the bundled reference only.";
            return new CluebookSources { Mazes = bundled };
        }

        var mazes = bundled;
        string mazeFile = "";
        string mazeDetail = "no Mazedata.dta in that folder, so the bundled layouts were used";

        try
        {
            string? found = Directory.EnumerateFiles(folder, "*.dta")
                .FirstOrDefault(p => string.Equals(Path.GetFileName(p), "Mazedata.dta",
                                                   StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                var exact = MazeData.FromBytes(File.ReadAllBytes(found));
                if (exact is not null)
                {
                    mazes = exact;
                    mazeFile = found;
                    mazeDetail = $"walls read from {Path.GetFileName(found)}";
                }
                else
                {
                    mazeDetail = $"{Path.GetFileName(found)} is not a {MazeData.FileSize:N0}-byte maze file, " +
                                 "so the bundled layouts were used";
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            mazeDetail = $"could not read Mazedata.dta ({e.Message}), so the bundled layouts were used";
        }

        var overlays = OverlaySet.Load(folder, mazes.Maps.Select(m => m.RawName));
        detail = $"{overlays.Count} of {MazeData.MapCount} location overlays read, {mazeDetail}." +
                 (overlays.Problems.Count > 0 ? $" {overlays.Problems.Count} file(s) could not be read." : "");

        return new CluebookSources
        {
            Mazes = mazes,
            MazeFile = mazeFile,
            Overlays = overlays,
            GameFolder = folder,
        };
    }
}

/// <summary>
/// A Might &amp; Magic 1 cluebook, ready to render.
///
/// <para>The shape follows the cluebook in the sibling <c>TheQuestTrainer</c> deliberately: an
/// overview, a page of what not to trust, a walkthrough, a chapter per place, and the reference
/// tables behind it. The two games have nothing in common technically — one is a 1986 DOS binary,
/// the other a Palm database — but a strategy guide is a strategy guide, and the section that says
/// what the book does not know belongs at the front of both.</para>
///
/// <para>What is specific to this game is that the guide is assembled from two halves that have to
/// be kept honestly apart: the trainer's own decode of the game's tables, which is always present,
/// and the game's own words, which are present only when the reader owns the game and says where it
/// is. <see cref="Notes"/> is where that, and every other limit, is stated — on the document's own
/// first page rather than in a footnote nobody reaches.</para>
/// </summary>
public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }

    /// <summary>Every location, in the game's own record order.</summary>
    public required IReadOnlyList<LocationChapter> Chapters { get; init; }

    /// <summary>The nine gold fragments of the Inner Sanctum riddle, in reading order.</summary>
    public required IReadOnlyList<FoundFragment> Gold { get; init; }

    /// <summary>The six silver fragments, in label order.</summary>
    public required IReadOnlyList<FoundFragment> Silver { get; init; }

    /// <summary>What the reader should know about how this was produced.</summary>
    public required IReadOnlyList<string> Notes { get; init; }

    /// <summary>Files that looked like overlays but could not be read.</summary>
    public required IReadOnlyList<string> Problems { get; init; }

    /// <summary>Whether the walls came from the player's own <c>Mazedata.dta</c>.</summary>
    public required bool MazesAreExact { get; init; }

    /// <summary>Where the walls came from, for the overview.</summary>
    public required string MazeSource { get; init; }

    /// <summary>The installation the words came from, or the empty string.</summary>
    public required string GameFolder { get; init; }

    /// <summary>How many locations have their text.</summary>
    public int LocationsWithText => Chapters.Count(c => c.Messages.Count > 0);

    /// <summary>How many messages were recovered in total.</summary>
    public int MessageCount => Chapters.Sum(c => c.Messages.Count);

    /// <summary>Whether there is any location text at all — false makes several sections pointless.</summary>
    public bool HasEventText => Options.IncludeEventText && MessageCount > 0;

    /// <summary>The chapters of one kind of place, in record order.</summary>
    public IEnumerable<LocationChapter> Of(PlaceKind kind) => Chapters.Where(c => c.Kind == kind);

    /// <summary>Builds the book.</summary>
    public static Cluebook Build(CluebookSources sources, CluebookOptions options)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(options);

        var overlays = options.IncludeEventText ? sources.Overlays : null;

        var chapters = sources.Mazes.Maps.Select(maze => new LocationChapter
        {
            Maze = maze,
            Place = PlaceBook.For(maze.RawName),
            Overlay = overlays?.For(maze.RawName),
            Stats = maze.Counts(),
            Landmarks = LandmarkBook.For(maze.RawName),
            SecretPassages = maze.SecretPassages(),
        }).ToList();

        var gold = PuzzleTrail.Gold.Select(f => Resolve(f, overlays)).ToList();
        var silver = PuzzleTrail.Silver.Select(f => Resolve(f, overlays)).ToList();

        return new Cluebook
        {
            Options = options,
            Chapters = chapters,
            Gold = gold,
            Silver = silver,
            Problems = sources.Overlays?.Problems ?? Array.Empty<string>(),
            MazesAreExact = sources.Mazes.IsExact,
            MazeSource = sources.MazeFile.Length > 0 ? Path.GetFileName(sources.MazeFile) : "the bundled layouts",
            GameFolder = sources.GameFolder,
            Notes = BuildNotes(sources, options, chapters),
        };
    }

    /// <summary>Finds a cipher fragment in the overlay that is supposed to hold it.</summary>
    private static FoundFragment Resolve(PuzzleTrail.Fragment fragment, OverlaySet? overlays) =>
        new(fragment, PlaceBook.For(fragment.RawName), overlays?.For(fragment.RawName)?.Find(fragment.Marker));

    /// <summary>
    /// What the reader should not trust, and why.
    ///
    /// <b>This is the section the rest of the document is allowed to be confident because of.</b>
    /// Every claim here is one that was checked and came back short of certain: which record is which
    /// place, where a message is triggered, whether the walls are the game's own bytes or a
    /// transcription of them. Writing them down once at the front is what lets a chapter say "the
    /// Shrine of Okzar" without hedging every sentence — and it is why a fact moving from inferred to
    /// confirmed should change this list first.
    /// </summary>
    private static List<string> BuildNotes(CluebookSources sources, CluebookOptions options,
                                           IReadOnlyList<LocationChapter> chapters)
    {
        var notes = new List<string>();

        notes.Add(sources.Mazes.IsExact
            ? $"The walls, doors and secret passages are decoded from your own {Path.GetFileName(sources.MazeFile)}, " +
              "so every plan in this book is the bytes the game itself walks you around."
            : "The walls, doors and secret passages come from this project's transcription of the game's maze " +
              "file rather than from your copy of it. What an edge does survives that transcription exactly; " +
              "which of three wall graphics is drawn does not, and an edge that the two squares either side " +
              "of it record differently keeps the more solid of the two. Point the cluebook at your game " +
              "folder for the exact bytes.");

        int withText = chapters.Count(c => c.Messages.Count > 0);
        if (!options.IncludeEventText)
        {
            notes.Add("What each location says has been left out of this copy.");
        }
        else if (withText == 0)
        {
            notes.Add("No location text is in this book. The game's own words — every sign, riddle, offer and " +
                      "trap message — live in the 55 .ovr overlay files of an installation, and none were " +
                      "found. Point the cluebook at the folder holding MM.EXE to have them read from your own " +
                      "copy; nothing of the sort is shipped with the trainer.");
        }
        else
        {
            notes.Add($"The location text was read from your own installation ({withText} of " +
                      $"{MazeData.MapCount} locations). It is reproduced as the files hold it, including the " +
                      "line breaks the game's text window uses — which is why some words run together exactly " +
                      "as they do on the original screen.");
            notes.Add("A location's messages are listed in the order its file stores them, which is not the " +
                      "order you will meet them and says nothing about which square triggers which. The " +
                      "dispatcher's event-id table was decoded far enough to skip past it, not far enough to " +
                      "place a message on the map, and this book does not pretend otherwise.");
        }

        if (options.IncludePlans)
            notes.Add("The plans are drawn north up, which matches the game's compass. Whether the game counts " +
                      "its own y from the north or from the south when it shows you a coordinate was not " +
                      "established, so a square this book calls (3, 12) may be the one the game calls (3, 3). " +
                      "Read a plan by its shape and its landmarks rather than by its numbers.");

        int marked = chapters.Count(c => c.Landmarks.Count > 0);
        notes.Add($"The numbered marks on the plans are coordinates from walkthroughs and from the game's own " +
                  $"text, not squares this project decoded -- which is why there are {LandmarkBook.Landmarks.Count} " +
                  $"of them across {marked} places rather than one on every map. Each location's overlay does hold " +
                  "a table of the squares its events fire on, but what those numbers index has not been worked " +
                  "out, so the book can tell you a place has fourteen event squares and not which fourteen. The " +
                  "walls you can walk through are the opposite case: those are computed from the maze data and " +
                  "are exact.");

        notes.Add("Indoors, a wall that is drawn but walkable is a secret passage, and every one of them is " +
                  "listed under its plan. Outdoors it is terrain — scrub, trees, the edge of a wood — which is " +
                  "why a surface area has two hundred of them and a town has thirty, and why the surface maps " +
                  "get a count rather than a list.");

        notes.Add("Each place says how firmly its record is tied to it. The five towns, the twenty surface " +
                  "cells, the Soul Maze and the Astral Plane are confirmed; the castles, caves and lairs are " +
                  "inferred from their names and their contents; the four \"pp\" levels are a guess. A name " +
                  "marked inferred is a good bet, not a fact.");

        notes.Add("Where the surface cells are concerned this project's own references disagree: the maze atlas " +
                  "and the map reference put Sorpigal on cell A-1, while the community walkthrough puts it at " +
                  "B-4. The overlays do not say, so this book does not decide — treat any surface cell letter " +
                  "in the walkthrough as that guide's convention.");

        notes.Add("The walkthrough is summarised from community guides and cross-checked between them, not " +
                  "extracted from the game. A few coordinates differ between sources; where they conflicted the " +
                  "version two or more agreed on was used.");

        notes.Add("The item and monster tables are extracted verbatim from MM.EXE. The bytes that were not " +
                  "decoded — a monster's special attacks, resistances and treasure — are left out rather than " +
                  "guessed at, so a column that is absent is absent on purpose.");

        notes.Add("Item effects and who may use them are transcribed from a community item FAQ and joined to " +
                  "the extracted table by name. 254 of the 255 ids match; OBSIDIAN BOW (id 85) is missing from " +
                  "that list and so carries no effect line here.");

        notes.Add("The experience table in the classes chapter is the manual's own approximation — 2,000 to " +
                  "reach level 2, doubling thereafter. The game's real curve is described in the rules chapter: " +
                  "the same doubling to level 8, then a fixed increment per level, on two tables split by class.");

        if (sources.Overlays is { Problems.Count: > 0 })
            notes.Add($"{sources.Overlays.Problems.Count} file(s) in the game folder ended in .ovr but could not " +
                      "be read as overlays; they are listed at the end of this book.");

        notes.Add("Nothing from the game is redistributed. Your files are opened read-only, and the only thing " +
                  "written anywhere is this book, in the folder you chose.");

        return notes;
    }
}
