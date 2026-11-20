namespace Projectional.Language;

/// <summary>
/// A list: any number of values of one element type.
/// </summary>
/// <remarks>
/// The element type is the whole of it, because a list's length is a fact about a value and not about its
/// type. A language that needs a fixed length says so with a <see cref="RecordType"/> or a constraint.
/// </remarks>
public sealed class ListType : IType
{
    /// <summary>Creates a list of <paramref name="element"/>.</summary>
    /// <param name="element">What every item of the list evaluates to.</param>
    public ListType(IType element)
    {
        ArgumentNullException.ThrowIfNull(element);

        Element = element;
    }

    /// <summary>What every item of the list evaluates to.</summary>
    public IType Element { get; }

    /// <inheritdoc />
    public string Name => $"[{Element.Name}]";

    /// <inheritdoc />
    /// <remarks>
    /// An <see cref="UnknownType"/> is accepted, and so is a list of unknown elements: refusing either
    /// would turn one mistake into a finding on every expression that reads the list.
    /// </remarks>
    public bool Accepts(IType actual)
    {
        ArgumentNullException.ThrowIfNull(actual);

        return NamedType.Unwrap(actual) switch
        {
            UnknownType => true,
            ListType list => Element.Accepts(list.Element),
            _ => false,
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// A list is comparable with another list when its elements are, so a list's comparability is as structural as
    /// its acceptance - and an unknown type is comparable with anything, for the same reason it is accepted.
    /// </remarks>
    public bool IsComparableTo(IType other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return NamedType.Unwrap(other) switch
        {
            UnknownType => true,
            ListType list => Element.IsComparableTo(list.Element),
            _ => false,
        };
    }

    public override string ToString() => Name;
}
