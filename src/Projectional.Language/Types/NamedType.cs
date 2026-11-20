namespace Projectional.Language;

/// <summary>
/// A type a language declared under a name, standing for the type that name was given.
/// </summary>
/// <remarks>
/// <para>
/// The <em>name</em> is what the type is called and the <em>expansion</em> is what it means, and the two are
/// kept apart on purpose. A diagnostic that quoted the expansion of a name the author wrote would make the
/// source and the message describe the same thing in two vocabularies; a caller that had to unwrap an alias
/// to compare two types would be doing the language's job.
/// </para>
/// <para>
/// It is therefore still structural: <see cref="Accepts"/> asks the expansion, unwrapping a
/// <see cref="NamedType"/> on both sides, so a value that is structurally the expansion satisfies the alias
/// and two aliases of one type accept each other. A name is a spelling, not a distinct kind of value -
/// nothing here makes <c>Amount</c> and <c>int</c> different types, only different words for one.
/// </para>
/// </remarks>
public sealed class NamedType : IType
{
    /// <summary>Creates the type <paramref name="name"/> stands for.</summary>
    /// <param name="name">What the language calls it.</param>
    /// <param name="expansion">What it means.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public NamedType(string name, IType expansion)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(expansion);

        Name = name;
        Expansion = expansion;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>What the name means.</summary>
    public IType Expansion { get; }

    /// <summary>
    /// <paramref name="type"/> with every declared name removed, so a caller sees what it means.
    /// </summary>
    public static IType Unwrap(IType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type is NamedType named ? Unwrap(named.Expansion) : type;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Both sides are unwrapped first, so an alias accepts its own expansion, the expansion accepts the
    /// alias, and two names for one type accept each other.
    /// </remarks>
    public bool Accepts(IType actual)
    {
        ArgumentNullException.ThrowIfNull(actual);

        return Expansion.Accepts(actual is NamedType named ? named.Expansion : actual);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A declared name is comparable with what its expansion is comparable with, so the name is transparent for
    /// comparison exactly as it is for acceptance. Unwrapping the other side is the same rule read the other way.
    /// </remarks>
    public bool IsComparableTo(IType other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Expansion.IsComparableTo(other is NamedType named ? named.Expansion : other);
    }

    public override string ToString() => Name;
}
