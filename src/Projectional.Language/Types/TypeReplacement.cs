namespace Projectional.Language;

/// <summary>
/// One rewrite a language declares: a claim of a relation between two types is replaced by other relations.
/// </summary>
/// <remarks>
/// <para>
/// This is MPS' replacement rule, and it is what makes a synthesis rule declarable at all. "A sequence of one
/// element type is a subtype of a sequence of another" is not an edge between two types - it is an edge between
/// <em>any</em> two sequences, and what it really asks is that the element types relate. A replacement states
/// exactly that: match two patterns, bind their variables, and prove the relations the rule lists instead.
/// </para>
/// <para>
/// A replacement states <em>no</em> relations when the claim holds outright. MPS' own example is the null type,
/// which is a subtype of everything except the primitives, so the inequation between it and a classifier is
/// simply removed rather than proven. An empty <see cref="Instead"/> is that removal.
/// </para>
/// <para>
/// The patterns are <see cref="IType"/>s whose leaves may be <see cref="TypeVariable"/>s, so a pattern is written
/// in exactly the vocabulary a relation is - there is no second syntax for "a type with a hole in it". A variable
/// that the patterns bind is the rule's own: two replacements never share one, because a variable's identity is
/// the instance and each rule holds its own.
/// </para>
/// </remarks>
public sealed class TypeReplacement
{
    /// <summary>Creates a replacement for <paramref name="claim"/> between two patterns.</summary>
    /// <param name="claim">The relation whose claim this rule rewrites.</param>
    /// <param name="left">The pattern the left side of a claim has to match.</param>
    /// <param name="right">The pattern the right side has to match.</param>
    /// <param name="instead">The relations to prove in place of the claim; empty when the claim is discharged.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="claim"/> is <see cref="TypeEquationKind.Supertype"/>, which is
    /// <see cref="TypeEquationKind.Subtype"/> read the other way round and so not a second claim.
    /// </exception>
    public TypeReplacement(
        TypeEquationKind claim,
        IType left,
        IType right,
        IReadOnlyList<TypeEquation> instead)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(instead);

        if (claim == TypeEquationKind.Supertype)
        {
            throw new ArgumentException(
                "A replacement claims a subtype or an equality; ':>' is the same claim as '<:' with the sides swapped.",
                nameof(claim));
        }

        Claim = claim;
        Left = left;
        Right = right;
        Instead = instead;
    }

    /// <summary>Which relation's claim this rule rewrites.</summary>
    public TypeEquationKind Claim { get; }

    /// <summary>The pattern the left side of a claim has to match.</summary>
    public IType Left { get; }

    /// <summary>The pattern the right side of a claim has to match.</summary>
    public IType Right { get; }

    /// <summary>The relations to prove instead, in the order the rule states them.</summary>
    public IReadOnlyList<TypeEquation> Instead { get; }

    /// <summary>
    /// The relations to prove in place of a claim between <paramref name="left"/> and <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The left side of the claim being made.</param>
    /// <param name="right">The right side of the claim being made.</param>
    /// <param name="instead">The relations to prove, with the patterns' variables replaced by what they matched.</param>
    /// <returns><see langword="true"/> when both patterns matched, so the claim was rewritten.</returns>
    /// <exception cref="ArgumentNullException">Either side is <see langword="null"/>.</exception>
    public bool TryApply(IType left, IType right, out IReadOnlyList<TypeEquation> instead)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        Dictionary<TypeVariable, IType> bindings = [];

        if (!Match(Left, left, bindings) || !Match(Right, right, bindings))
        {
            instead = [];

            return false;
        }

        instead = [.. Instead.Select(each => new TypeEquation(
            each.Kind,
            Substitute(each.Left, bindings),
            Substitute(each.Right, bindings)))];

        return true;
    }

    /// <summary>Whether <paramref name="pattern"/> describes <paramref name="type"/>, binding its variables.</summary>
    /// <remarks>
    /// A shape matches its own shape and descends into the one part it names: a list into its element, an optional
    /// into its inner, a record into the fields the pattern names. Nothing else matches, which is what keeps a
    /// pattern from claiming more than its author wrote - a join of two types is "either of these", a claim no
    /// single shape can make, so it is refused where it is written rather than never matching at run time.
    /// </remarks>
    private static bool Match(IType pattern, IType type, Dictionary<TypeVariable, IType> bindings)
    {
        switch (pattern)
        {
            case TypeVariable variable when bindings.TryGetValue(variable, out var bound):
                // One variable twice in a rule means one type twice, which is the whole point of writing it
                // twice: "[?a] <: [?a]" relates a type to itself and must not match a sequence of anything else.
                return bound.IsComparableTo(type);

            case TypeVariable variable:
                bindings[variable] = type;

                return true;

            case ListType list when type is ListType actual:
                return Match(list.Element, actual.Element, bindings);

            case OptionalType optional when type is OptionalType actual:
                return Match(optional.Inner, actual.Inner, bindings);

            case RecordType record when type is RecordType actual:
                foreach (var (name, field) in record.Fields)
                {
                    var match = actual.Fields.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal));

                    if (match.Name is null || !Match(field, match.Type, bindings))
                    {
                        return false;
                    }
                }

                return true;

            case NamedType name:
                return ReferenceEquals(name, type)
                    || (type is NamedType other && string.Equals(other.Name, name.Name, StringComparison.Ordinal));

            default:
                // A pattern that is not a variable, a shape or a name is a type written out, so it matches the type
                // it is: a primitive its own name, and a join or a meet never - the lowering refuses those.
                return pattern.GetType() == type.GetType() && string.Equals(pattern.Name, type.Name, StringComparison.Ordinal);
        }
    }

    /// <summary><paramref name="type"/> with every variable the patterns bound replaced, all the way down.</summary>
    /// <remarks>
    /// Deeper than the solver's own substitution, which only ever replaces a whole side: a relation a rule states
    /// instead is a template written by an author, so <c>[?element]</c> has to become <c>[int]</c> and not stay a
    /// list of a variable nobody constrained.
    /// </remarks>
    private static IType Substitute(IType type, Dictionary<TypeVariable, IType> bindings) => type switch
    {
        TypeVariable variable when bindings.TryGetValue(variable, out var bound) => bound,
        ListType list => new ListType(Substitute(list.Element, bindings)),
        OptionalType optional => new OptionalType(Substitute(optional.Inner, bindings)),
        RecordType record => new RecordType(record.Fields.Select(field => (field.Name, Substitute(field.Type, bindings)))),
        JoinType join => new JoinType(Substitute(join.Left, bindings), Substitute(join.Right, bindings)),
        MeetType meet => new MeetType(Substitute(meet.Left, bindings), Substitute(meet.Right, bindings)),
        _ => type,
    };

    public override string ToString() =>
        $"{Claim} of {Left.Name} and {Right.Name} by {Instead.Count} relation(s)";
}
