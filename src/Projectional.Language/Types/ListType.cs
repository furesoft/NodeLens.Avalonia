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

    public override string ToString() => Name;
}
