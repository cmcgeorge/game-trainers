using TheQuestTrainer.Game;

namespace TheQuestTrainer.ViewModels;

/// <summary>
/// Shared behaviour for an editable row: a backing value the refresh updates, and a way to put it
/// back when a write is refused.
///
/// <b>A refused edit reverts.</b> Every editable setter routes failures through
/// <see cref="Reject"/>, including the case where the write was attempted and came back incomplete
/// — not only the "not attached" case. Otherwise the box keeps showing a number the game never
/// took, and the next refresh silently contradicts it.
///
/// <b>A refresh may not overwrite a value being typed into, and a new row must still get one.</b>
/// <see cref="Update"/> takes an <c>initial</c> flag: a row built while an editor has focus must
/// still take the game's number, or it shows zero and any action taken on it works from zero.
/// </summary>
public abstract class GameRowViewModel : ObservableObject
{
    /// <summary>The session this row writes through.</summary>
    protected IGameHost Host { get; }

    /// <summary>Label shown to the left of the editor.</summary>
    public string Label { get; }

    /// <summary>Longer text for the tooltip; may be empty.</summary>
    public string Description { get; }

    /// <summary>Binds the row to a host.</summary>
    protected GameRowViewModel(IGameHost host, string label, string description = "")
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
        Label = label;
        Description = description;
    }

    /// <summary>
    /// Puts <paramref name="field"/> back to <paramref name="previous"/> and re-raises
    /// <paramref name="property"/> so the editor snaps back to what the game actually holds.
    /// </summary>
    protected void Reject<T>(ref T field, T previous, string property, ActionResult result)
    {
        field = previous;
        OnPropertyChanged(property);
        if (!string.IsNullOrEmpty(result.Message)) Host.Report(result.Message);
    }

    /// <summary>
    /// Puts <paramref name="field"/> in step with the value the game actually took.
    ///
    /// A successful write is not proof the number on screen is the number in the game: every write
    /// clamps to the field it is going into, so asking for 9,999 in a skill sets 250. Without this,
    /// the box keeps showing 9,999 until a refresh happens to run while no editor anywhere in the
    /// window has focus — which can be a long time.
    /// </summary>
    protected void Settle(ref int field, ActionResult result, string property)
    {
        if (result.Written is { } written && field != (int)written)
        {
            field = (int)written;
            OnPropertyChanged(property);
        }
        if (!string.IsNullOrEmpty(result.Message)) Host.Report(result.Message);
    }
}

/// <summary>One of the five base attributes.</summary>
public sealed class AttributeRowViewModel : GameRowViewModel
{
    private int _value;

    /// <summary>The game's attribute id, 1..5.</summary>
    public int Id { get; }

    /// <summary>Binds the row to an attribute.</summary>
    public AttributeRowViewModel(IGameHost host, AttributeInfo info)
        : base(host, info.Name, info.Effect)
    {
        Id = info.Id;
    }

    /// <summary>Base value. Setting it writes through, and reverts if the write is refused.</summary>
    public int Value
    {
        get => _value;
        set
        {
            int previous = _value;
            if (!SetField(ref _value, value)) return;
            var result = Host.WriteAttribute(Id, value);
            if (!result.Ok) Reject(ref _value, previous, nameof(Value), result);
            else Settle(ref _value, result, nameof(Value));
        }
    }

    /// <summary>Takes the game's value without writing it back.</summary>
    public void Update(int value, bool initial)
    {
        if (!initial && Host.EditorHasFocus) return;
        SetField(ref _value, value, nameof(Value));
    }
}

/// <summary>One of the twenty skills.</summary>
public sealed class SkillRowViewModel : GameRowViewModel
{
    private int _value;
    private int _starting;
    private string _note = "";

    /// <summary>The game's skill id, 1..20.</summary>
    public int Id { get; }

    /// <summary>Attribute id that caps this skill at twice its base value.</summary>
    public int GoverningAttribute { get; }

    /// <summary>Name of that attribute, for the "governed by" column.</summary>
    public string GoverningAttributeName { get; }

    /// <summary>Binds the row to a skill.</summary>
    public SkillRowViewModel(IGameHost host, SkillInfo info)
        : base(host, info.Name, info.Effect)
    {
        Id = info.Id;
        GoverningAttribute = info.GoverningAttribute;
        GoverningAttributeName = GameTables.Attribute(info.GoverningAttribute)?.Name ?? "?";
    }

    /// <summary>Base value. What the game's skills screen shows is this plus race and item bonuses.</summary>
    public int Value
    {
        get => _value;
        set
        {
            int previous = _value;
            if (!SetField(ref _value, value)) return;
            var result = Host.WriteSkill(Id, value);
            if (!result.Ok) Reject(ref _value, previous, nameof(Value), result);
            else Settle(ref _value, result, nameof(Value));
        }
    }

    /// <summary>The value the character was created with; never written.</summary>
    public int Starting
    {
        get => _starting;
        private set => SetField(ref _starting, value);
    }

    /// <summary>Short annotation, e.g. the game's own cap for this skill.</summary>
    public string Note
    {
        get => _note;
        private set => SetField(ref _note, value);
    }

    /// <summary>Takes the game's values without writing them back.</summary>
    public void Update(int value, int starting, int cap, bool available, bool initial)
    {
        Starting = starting;
        Note = available ? $"cap {cap}" : "race-locked";
        if (!initial && Host.EditorHasFocus) return;
        SetField(ref _value, value, nameof(Value));
    }
}

/// <summary>A read-only label/value pair for the Reference tab.</summary>
/// <param name="Name">Left column.</param>
/// <param name="Value">Middle column.</param>
/// <param name="Detail">Right column; may be empty.</param>
public readonly record struct ReferenceRow(string Name, string Value, string Detail);
