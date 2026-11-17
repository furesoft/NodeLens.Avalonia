using Projectional.Core;

namespace Projectional.Language;

/// <summary>
/// A type a property declaration already names: <c>bool</c>, <c>int</c>, <c>string</c>, a date, or one of
/// a language's enumerations.
/// </summary>
/// <remarks>
/// <para>
/// It wraps the <see cref="PropertyType"/> rather than replacing it, because the declaration is what the
/// model stores and the type is what a node evaluates to - the same word, two jobs. Wrapping also means a
/// language declares its primitive types once, in the place it already declares its properties.
/// </para>
/// <para>
/// Two primitive types are the same when their names are, compared ordinally. The name is the only public
/// discriminator a <see cref="PropertyType"/> has (the concrete types behind it are internal), and it is
/// what the language wrote, so it is what a diagnostic can quote.
/// </para>
/// </remarks>
public sealed class PrimitiveType : IType
{
    private PrimitiveType(PropertyType declaration) => Declaration = declaration;

    /// <summary>The declaration this type was made from.</summary>
    public PropertyType Declaration { get; }

    /// <inheritdoc />
    public string Name => Declaration.Name;

    /// <summary>The type of <paramref name="declaration"/>.</summary>
    public static PrimitiveType Of(PropertyType declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        return new PrimitiveType(declaration);
    }

    /// <inheritdoc />
    /// <remarks>
    /// An <see cref="UnknownType"/> is accepted, because refusing it would turn one mistake into a finding
    /// on every expression that reads it.
    /// </remarks>
    /// <remarks>
    /// A declared name is looked through first: an <c>Amount</c> that means <c>int</c> is an <c>int</c>, so a
    /// primitive that refused it would make every alias a nominal type by accident.
    /// </remarks>
    public bool Accepts(IType actual)
    {
        ArgumentNullException.ThrowIfNull(actual);

        return NamedType.Unwrap(actual) switch
        {
            UnknownType => true,
            PrimitiveType primitive => string.Equals(primitive.Name, Name, StringComparison.Ordinal),
            _ => false,
        };
    }

    public override string ToString() => Name;
}
