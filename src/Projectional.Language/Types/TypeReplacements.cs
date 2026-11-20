namespace Projectional.Language;

/// <summary>
/// The replacements the framework itself states, which every language gets before it declares its own.
/// </summary>
/// <remarks>
/// <para>
/// Two shapes have a part that is a type - a list has an element and an optional has an inner - and for both, a
/// claim about the whole is a claim about the part. That is covariance, and it used to be two <c>case</c> labels
/// inside <see cref="TypeSolver"/>. Stating it as replacements instead keeps one mechanism rather than two: what
/// the solver knows about a list is what these rules say, and a language that declares a replacement is using the
/// same vocabulary the framework used to hard-code.
/// </para>
/// <para>
/// They are additive and not overridable: a language adds replacements, and the framework's own stay, because a
/// language that could suppress covariance would be a language whose lists stop relating to their elements -
/// which is not a decision a type rule should be able to make by omission. A record is deliberately absent: its
/// fields are compared by name, so "do these two shapes correspond" is the question the solve is already running.
/// </para>
/// </remarks>
public static class TypeReplacements
{
    /// <summary>The replacements every language has, whether or not it declares any.</summary>
    public static IReadOnlyList<TypeReplacement> Covariance { get; } =
    [
        // A sequence of one element type is a sequence of another exactly when the elements relate, and the same
        // holds of an optional and its inner. Each rule holds its own variables, so the four never interfere.
        EqualList(),
        EqualOptional(),
        SubtypeList(),
        SubtypeOptional(),
    ];

    private static TypeReplacement EqualList()
    {
        TypeVariable left = new("element");
        TypeVariable right = new("otherElement");

        return new(
            TypeEquationKind.Equal,
            new ListType(left),
            new ListType(right),
            [TypeEquation.Equal(left, right)]);
    }

    private static TypeReplacement EqualOptional()
    {
        TypeVariable left = new("inner");
        TypeVariable right = new("otherInner");

        return new(
            TypeEquationKind.Equal,
            new OptionalType(left),
            new OptionalType(right),
            [TypeEquation.Equal(left, right)]);
    }

    private static TypeReplacement SubtypeList()
    {
        TypeVariable left = new("element");
        TypeVariable right = new("otherElement");

        return new(
            TypeEquationKind.Subtype,
            new ListType(left),
            new ListType(right),
            [TypeEquation.Subtype(left, right)]);
    }

    private static TypeReplacement SubtypeOptional()
    {
        TypeVariable left = new("inner");
        TypeVariable right = new("otherInner");

        return new(
            TypeEquationKind.Subtype,
            new OptionalType(left),
            new OptionalType(right),
            [TypeEquation.Subtype(left, right)]);
    }
}
