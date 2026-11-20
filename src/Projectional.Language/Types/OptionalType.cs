namespace Projectional.Language;

/// <summary>
/// A value that may be absent: either the inner type or nothing.
/// </summary>
/// <remarks>
/// <para>
/// An optional accepts the inner type as well as another optional, because "may be absent" is a promise
/// about what a reader has to handle, not a different kind of value. The other direction does not hold: a
/// plain <c>int</c> does not accept an optional <c>int</c>, because a slot that promised a value would be
/// getting one that might not be there.
/// </para>
/// <para>
/// Nesting is not collapsed. <c>int??</c> is a different type from <c>int?</c> - it says the absence may
/// itself be absent - and a language that does not want that should not declare it.
/// </para>
/// </remarks>
public sealed class OptionalType : IType
{
    /// <summary>Creates an optional of <paramref name="inner"/>.</summary>
    /// <param name="inner">What the value is when it is there.</param>
    public OptionalType(IType inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        Inner = inner;
    }

    /// <summary>What the value is when it is there.</summary>
    public IType Inner { get; }

    /// <inheritdoc />
    public string Name => $"{Inner.Name}?";

    /// <inheritdoc />
    /// <remarks>
    /// An <see cref="UnknownType"/> is accepted, and so is the inner type itself: an optional is a promise
    /// about absence, so a value that is always there satisfies it.
    /// </remarks>
    public bool Accepts(IType actual)
    {
        ArgumentNullException.ThrowIfNull(actual);

        return NamedType.Unwrap(actual) switch
        {
            UnknownType => true,
            OptionalType optional => Inner.Accepts(optional.Inner),
            var unwrapped => Inner.Accepts(unwrapped),
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// An optional is comparable with another optional when its inners are, and with the inner type itself when the
    /// value is always there - the same shapes its acceptance admits, so the two questions stay in step.
    /// </remarks>
    public bool IsComparableTo(IType other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return NamedType.Unwrap(other) switch
        {
            UnknownType => true,
            OptionalType optional => Inner.IsComparableTo(optional.Inner),
            var unwrapped => Inner.IsComparableTo(unwrapped),
        };
    }

    public override string ToString() => Name;
}
