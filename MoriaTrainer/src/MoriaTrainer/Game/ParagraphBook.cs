namespace MoriaTrainer.Game;

/// <summary>
/// Renders the in-game "monster memory" recall paragraph for a creature, mirroring what UMoria's
/// <c>recall[]</c> engine prints when you press <c>/</c> or <c>l</c> on a creature you've seen.
///
/// The live <c>c_recall[]</c> array (one <c>recall_type</c> per creature, 279 of them) accumulates
/// observed attacks/spells/breaths/flags as you fight. Reading it back requires locating the COFF
/// image base — a Candidate for a future revision (see <c>.docs/ReverseEngineering.md</c> §7).
/// Until then, this book renders the **static** recall text from the shipped roster
/// (<see cref="MonsterBook"/>), so the Paragraphs tab shows what each creature *can* do — the same
/// information a fully-explored monster memory would show.
/// </summary>
public static class ParagraphBook
{
    /// <summary>
    /// Returns the recall paragraph for the given creature id, or null if the id is unknown to the
    /// shipped roster. The paragraph mirrors the in-game recall format: a header line, an attacks
    /// line, a defenses/resistances line, and a tactics note.
    /// </summary>
    public static string? Render(int creatureId)
    {
        var c = MonsterBook.ById(creatureId);
        return c == null ? null : Render(c);
    }

    /// <summary>Renders the recall paragraph for a known creature.</summary>
    public static string Render(CreatureInfo c)
    {
        var lines = new List<string>
        {
            $"{c.Name} (depth {c.Level}, AC {c.ArmorClass}, HD {c.HitDice}, XP {c.Exp}, speed {c.Speed})",
            $"Attacks: {c.Attacks}",
        };

        if (!string.IsNullOrEmpty(c.Flags))
            lines.Add($"Attributes: {c.Flags}");

        if (!string.IsNullOrEmpty(c.Recall))
            lines.Add(c.Recall);

        if (c.IsBalrog)
            lines.Add("WIN CONDITION: killing this creature wins the game and retires the character.");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Searches the roster by name (case-insensitive substring).</summary>
    public static IEnumerable<CreatureInfo> Search(string query) =>
        string.IsNullOrWhiteSpace(query)
            ? MonsterBook.Creatures
            : MonsterBook.Creatures.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                             || c.Flags.Contains(query, StringComparison.OrdinalIgnoreCase));
}
