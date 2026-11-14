namespace Projectional.Core;

/// <summary>
/// Stable identity of a model inside a project. Models are the unit of saving and of opening
/// in an editor tab, so the id survives renames and moves between folders.
/// </summary>
public readonly record struct ModelId : IComparable<ModelId>
{
    private ModelId(Guid value) => Value = value;

    /// <summary>The underlying guid. <see cref="Guid.Empty"/> for the default value.</summary>
    public Guid Value { get; }

    /// <summary>The value that no model ever has.</summary>
    public static ModelId Empty => default;

    public bool IsEmpty => Value == Guid.Empty;

    public static ModelId New() => new(Guid.NewGuid());

    public static ModelId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("A model id must not be empty.", nameof(value))
        : new ModelId(value);

    public static ModelId Parse(string text) => TryParse(text, out var id)
        ? id
        : throw new FormatException($"'{text}' is not a valid model id.");

    public static bool TryParse(string? text, out ModelId id)
    {
        if (GuidText.TryParse(text, out var value))
        {
            id = new ModelId(value);
            return true;
        }

        id = default;
        return false;
    }

    public int CompareTo(ModelId other) => Value.CompareTo(other.Value);

    public static bool operator <(ModelId left, ModelId right) => left.CompareTo(right) < 0;

    public static bool operator <=(ModelId left, ModelId right) => left.CompareTo(right) <= 0;

    public static bool operator >(ModelId left, ModelId right) => left.CompareTo(right) > 0;

    public static bool operator >=(ModelId left, ModelId right) => left.CompareTo(right) >= 0;

    public override string ToString() => GuidText.Format(Value);
}
