namespace Projectional.Language;

/// <summary>The subtype edges a language has, with the primitive ones already in place and nothing behind them.</summary>
/// <remarks>
/// <para>
/// A relation rather than a rule on each type, because "which types may stand in for which" is one graph and a
/// per-type answer would have to repeat it everywhere - and a language that declares an edge should not have to
/// also edit the two types it joins.
/// </para>
/// <para>
/// Three facts are built in and cannot be removed, because a relation without them is not a subtype relation:
/// a type is a subtype of itself, a primitive type is a subtype of itself <em>by name</em> (two <c>int</c> types
/// are one type even though they are two instances), and <see cref="UnknownType"/> stands in no edge at all -
/// neither as the subtype nor as the supertype - which is what keeps "I do not know" from answering a question
/// about what the language requires. Everything else is a declared edge, and the declared edges are transitive:
/// adding <c>a -&lt;: b</c> to a relation that already held <c>b -&lt;: c</c> makes <c>a</c> a subtype of
/// <c>c</c> without a second declaration, because that is what a subtype relation means rather than a rule this
/// class chooses.
/// </para>
/// <para>
/// The registry is immutable and <see cref="Add"/> answers a new one, so a language can be composed without
/// mutating a relation another session is using - the same shape as <see cref="TypeRegistry"/>,
/// <see cref="EvaluationRegistry"/> and the notation registry.
/// </para>
/// <para>
/// The edges are held as a list and searched in order. A relation a language declares is a handful of edges, so a
/// dictionary would buy nothing and would need an equality contract on <see cref="IType"/> that this framework
/// does not have - two <c>int</c> types are equal by name and nothing else, and inventing <c>Equals</c> to make a
/// hash table work would put that decision in the wrong place.
/// </para>
/// </remarks>
public sealed class SubtypeRelation : ISubtype
{
    private readonly IReadOnlyList<(IType Subtype, IType Supertype)> _edges;

    private SubtypeRelation(IReadOnlyList<(IType Subtype, IType Supertype)> edges) => _edges = edges;

    /// <summary>
    /// A relation with the primitive edges and no declared one: a type is a subtype of itself, and by nothing else.
    /// </summary>
    /// <remarks>
    /// This is what a language that declares no subtyping gets, and it is also what
    /// <see cref="IType.Accepts"/>'s default implementation asks: a type that names itself is the whole of what
    /// the framework can know without a language saying more.
    /// </remarks>
    public static SubtypeRelation Primitives { get; } = new([]);

    /// <summary>How many edges were declared, which is zero for <see cref="Primitives"/>.</summary>
    public int Count => _edges.Count;

    /// <summary>The same relation with one more edge declared: every <paramref name="subtype"/> may stand as a <paramref name="supertype"/>.</summary>
    /// <param name="subtype">The type that may stand in place of the other.</param>
    /// <param name="supertype">The type it may stand in place of.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The edge is made of types the framework cannot relate, or it would make a type its own supertype only.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Declaring the same edge twice answers a relation that holds it once, so a language that states a fact in two
    /// places does not have to know it did.
    /// </para>
    /// <para>
    /// An edge that mentions <see cref="UnknownType"/> is refused rather than ignored: "unknown may stand in for
    /// <c>int</c>" is exactly the claim that makes a guess look like a proof, and it is the one claim this
    /// relation exists to keep out of a language's declarations.
    /// </para>
    /// </remarks>
    public SubtypeRelation Add(IType subtype, IType supertype)
    {
        ArgumentNullException.ThrowIfNull(subtype);
        ArgumentNullException.ThrowIfNull(supertype);

        if (subtype is UnknownType || supertype is UnknownType)
        {
            throw new ArgumentException(
                "'unknown' stands in no subtype edge: an unknown type is what a rule says it does not know, and letting it satisfy a supertype is how a guess becomes a proof.",
                nameof(subtype));
        }

        return Holds(subtype, supertype) ? this : new SubtypeRelation([.. _edges, (subtype, supertype)]);
    }

    /// <summary>Whether a value of the type <paramref name="actual"/> may stand where <paramref name="expected"/> is required.</summary>
    /// <param name="actual">The type the node produces.</param>
    /// <param name="expected">The type the language requires.</param>
    /// <remarks>
    /// A type is a subtype of itself, a primitive is a subtype of a primitive with the same name, and a declared
    /// edge (or a chain of them) makes one type a subtype of another. An <see cref="UnknownType"/> on either side
    /// answers <see langword="false"/> unless the two sides are the same type, which is the strictness
    /// <see cref="ISubtype"/> exists for.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public bool IsSubtype(IType actual, IType expected)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expected);

        if (Same(actual, expected))
        {
            return true;
        }

        // No edge touches "unknown" in either direction, so there is nothing left to ask: the two types are not the
        // same one, and a relation may not invent the claim that they relate.
        if (actual is UnknownType || expected is UnknownType)
        {
            return false;
        }

        return _edges.Any(edge => Same(edge.Subtype, actual) && Reaches(edge.Supertype, expected, []));
    }

    /// <summary>Whether the chain from <paramref name="from"/> arrives at <paramref name="expected"/>.</summary>
    private bool Reaches(IType from, IType expected, HashSet<IType> seen)
    {
        if (Same(from, expected))
        {
            return true;
        }

        // Reference identity is the visited set: a relation is a graph over a handful of instances, and a cycle it
        // accidentally holds must answer "no" rather than recur forever.
        if (!seen.Add(from))
        {
            return false;
        }

        return _edges.Any(edge => Same(edge.Subtype, from) && Reaches(edge.Supertype, expected, seen));
    }

    /// <summary>Whether the relation already holds "every <paramref name="subtype"/> may stand as a <paramref name="supertype"/>".</summary>
    private bool Holds(IType subtype, IType supertype) => IsSubtype(subtype, supertype);

    /// <summary>Whether two types are the same one, as far as the relation is concerned.</summary>
    /// <remarks>
    /// Identity first, then the one value equality the framework has: two <see cref="PrimitiveType"/>s with one name
    /// are one type, because a declaration writes the name and the instances are made where they are needed.
    /// </remarks>
    private static bool Same(IType left, IType right) =>
        ReferenceEquals(left, right)
        || (left is PrimitiveType a
            && right is PrimitiveType b
            && string.Equals(a.Name, b.Name, StringComparison.Ordinal));

    public override string ToString() => $"{_edges.Count} edge(s)";
}
