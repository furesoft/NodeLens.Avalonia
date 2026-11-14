namespace Projectional.Core;

/// <summary>
/// Stable identity of a node. It is assigned at creation and never changes, including
/// across renames and moves between models.
/// </summary>
/// <remarks>
/// Identity is what the rest of the system is built on: references are stored as target
/// ids, caret positions and undo entries address nodes by id, and diffing and merging
/// compare ids rather than paths.
/// </remarks>
public readonly record struct NodeId : IComparable<NodeId>
{
    private NodeId(Guid value) => Value = value;

    /// <summary>The underlying guid. <see cref="Guid.Empty"/> for the default value.</summary>
    public Guid Value { get; }

    /// <summary>The value that no node ever has.</summary>
    public static NodeId Empty => default;

    public bool IsEmpty => Value == Guid.Empty;

    public static NodeId New() => new(Guid.NewGuid());

    public static NodeId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("A node id must not be empty.", nameof(value))
        : new NodeId(value);

    public static NodeId Parse(string text) => TryParse(text, out var id)
        ? id
        : throw new FormatException($"'{text}' is not a valid node id.");

    /// <summary>
    /// Parses a persisted node id. Only the hyphenated 36-character layout is accepted, so
    /// that a hand-edited model file cannot introduce a second layout for the same id.
    /// </summary>
    public static bool TryParse(string? text, out NodeId id)
    {
        if (GuidText.TryParse(text, out var value))
        {
            id = new NodeId(value);
            return true;
        }

        id = default;
        return false;
    }

    public int CompareTo(NodeId other) => Value.CompareTo(other.Value);

    public static bool operator <(NodeId left, NodeId right) => left.CompareTo(right) < 0;

    public static bool operator <=(NodeId left, NodeId right) => left.CompareTo(right) <= 0;

    public static bool operator >(NodeId left, NodeId right) => left.CompareTo(right) > 0;

    public static bool operator >=(NodeId left, NodeId right) => left.CompareTo(right) >= 0;

    public override string ToString() => GuidText.Format(Value);
}
