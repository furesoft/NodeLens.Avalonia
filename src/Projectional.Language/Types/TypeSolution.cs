namespace Projectional.Language;

/// <summary>What one solve produced: the type of every variable, or why it stopped.</summary>
/// <remarks>
/// <para>
/// It answers three questions a caller needs and keeps them apart: whether the equations were satisfied
/// (<see cref="Succeeded"/>), what each variable turned out to be, and how much it cost (<see cref="Steps"/>).
/// A solution that failed still carries the assignments it found, because "it could not settle here" is more
/// useful with the part that was known than without it - the same choice <c>GeneratedText</c> makes about half a
/// file.
/// </para>
/// <para>
/// A variable the equations left undetermined is <em>not</em> removed from the answer: <see cref="Resolve"/>
/// hands it back as the variable it is, so a consumer sees `?elementType` rather than a type nobody proved. That
/// is the difference between this layer and a guess, and it is why the answer is a value rather than a
/// dictionary a caller has to probe.
/// </para>
/// </remarks>
public sealed class TypeSolution
{
    private readonly IReadOnlyDictionary<TypeVariable, IType> _types;

    /// <summary>Creates a solution, which only the solver does.</summary>
    /// <param name="types">What each variable turned out to be, in the order they were solved.</param>
    /// <param name="failure">Why the solve stopped without satisfying the equations, or <see langword="null"/>.</param>
    /// <param name="steps">How many narrowing steps the solve spent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="types"/> is <see langword="null"/>.</exception>
    internal TypeSolution(IReadOnlyDictionary<TypeVariable, IType> types, string? failure, int steps)
    {
        ArgumentNullException.ThrowIfNull(types);

        _types = types;
        Failure = failure;
        Steps = steps;
    }

    /// <summary>Whether every equation was satisfied.</summary>
    public bool Succeeded => Failure is null;

    /// <summary>Why the solve stopped without satisfying the equations, or <see langword="null"/> when it did.</summary>
    /// <remarks>
    /// A solve stops for one of two reasons: the equations contradict each other, or the step budget ran out first.
    /// The second is not a failure of the rules but of the budget, which is why it is a sentence rather than an
    /// exception - a runaway rule table must not be able to hang the window (<c>Interpreter</c>'s budget, and its
    /// reasoning, exactly).
    /// </remarks>
    public string? Failure { get; }

    /// <summary>How many narrowing steps the solve spent.</summary>
    public int Steps { get; }

    /// <summary>How many variables the equations gave a type.</summary>
    public int Solved => _types.Count;

    /// <summary>What <paramref name="variable"/> turned out to be, or the variable itself when nothing said.</summary>
    /// <param name="variable">The variable to ask about.</param>
    /// <exception cref="ArgumentNullException"><paramref name="variable"/> is <see langword="null"/>.</exception>
    public IType Of(TypeVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);

        return _types.TryGetValue(variable, out var type) ? type : variable;
    }

    /// <summary>Whether <paramref name="variable"/> got a type.</summary>
    /// <param name="variable">The variable to ask about.</param>
    /// <exception cref="ArgumentNullException"><paramref name="variable"/> is <see langword="null"/>.</exception>
    public bool IsSolved(TypeVariable variable) => !ReferenceEquals(Of(variable), variable);

    /// <summary><paramref name="type"/> with every solved variable replaced by its type.</summary>
    /// <param name="type">The type to substitute through.</param>
    /// <remarks>
    /// A structure is rebuilt rather than returned when one of its parts changed, because the caller is about to
    /// conformance-test it: a list of `?elementType` is not a list of <c>int</c> until somebody says so, and
    /// handing back the unresolved list would make every answer after a solve depend on which type the rule
    /// happened to build first. A record is not descended into - see <see cref="TypeSolver"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    public IType Resolve(IType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type switch
        {
            TypeVariable variable => Of(variable),
            ListType list when !ReferenceEquals(Resolve(list.Element), list.Element) => new ListType(Resolve(list.Element)),
            OptionalType optional when !ReferenceEquals(Resolve(optional.Inner), optional.Inner) =>
                new OptionalType(Resolve(optional.Inner)),
            _ => type,
        };
    }

    public override string ToString() =>
        Succeeded ? $"{Solved} variable(s) in {Steps} step(s)" : Failure!;
}
