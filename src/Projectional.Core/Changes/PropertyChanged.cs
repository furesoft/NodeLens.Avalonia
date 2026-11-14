namespace Projectional.Core;

/// <summary>
/// A property value was written. <see langword="null"/> means unset, which is different from
/// empty: a slot with no default has no text at all until the user types one.
/// </summary>
public sealed record PropertyChanged(NodeId Target, string Name, string? Old, string? New) : IModelChange
{
    /// <inheritdoc />
    public IModelChange Invert() => this with { Old = New, New = Old };
}
