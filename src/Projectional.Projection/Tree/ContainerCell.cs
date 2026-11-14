namespace Projectional.Projection;

/// <summary>
/// A cell that arranges other cells in one direction, with no meaning of its own beyond the
/// arrangement.
/// </summary>
/// <remarks>
/// Containers are what the notation's layout cells become: a line is a horizontal container, a
/// sequence a vertical one, an indent a vertical one that is pushed in. They keep the nesting the
/// notation declared instead of being flattened into their parent, so that an invalidation, a
/// diagnostic or a future table column can still be pointed at the region the author wrote.
/// </remarks>
public sealed class ContainerCell : StructureCell
{
    public ContainerCell(
        CellAddress address,
        CellOrientation orientation,
        params ReadOnlySpan<EditorCell> content)
        : this(address, orientation, isIndented: false, content)
    {
    }

    public ContainerCell(
        CellAddress address,
        CellOrientation orientation,
        bool isIndented,
        params ReadOnlySpan<EditorCell> content)
        : base(address, content)
    {
        Orientation = orientation;
        IsIndented = isIndented;
    }

    public CellOrientation Orientation { get; }

    /// <summary>Whether the content is pushed one indentation level in, as <c>ed.Indent</c> asks.</summary>
    public bool IsIndented { get; }

    internal override string Describe() =>
        (Orientation, IsIndented) switch
        {
            (CellOrientation.Horizontal, _) => "line",
            (CellOrientation.Vertical, true) => "indent",
            _ => "sequence",
        };
}