namespace Projectional.Language;

/// <summary>
/// The single child of a <see cref="Core.Cardinality.One"/> slot, drawn inline where it sits.
/// </summary>
/// <remarks>
/// This is what makes <c>if condition then</c> one line instead of three. A slot that holds many
/// children cannot be drawn inline — use <see cref="ChildNotationCell"/> instead.
/// </remarks>
public sealed class SingleChildNotationCell : InlineNotationCell
{
    public SingleChildNotationCell(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        Slot = slot;
    }

    public string Slot { get; }

    /// <summary>What the slot looks like while it is empty, so the caret has somewhere to go.</summary>
    public string EmptyText { get; init; } = "<empty>";

    public override string ToString() => $"child {Slot}";
}
