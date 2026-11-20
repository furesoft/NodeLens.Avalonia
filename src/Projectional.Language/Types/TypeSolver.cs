namespace Projectional.Language;

/// <summary>Finds the types that satisfy a set of <see cref="TypeEquation"/>s, or says why none does.</summary>
/// <remarks>
/// <para>
/// The solve is a fixpoint over the equations: each equation turns into a constraint on the variables in it, the
/// constraints are re-read until nothing narrows any more, and whatever is left is the answer. Two things make it
/// terminate. A step budget, because a language's rule table is data and a mistake in it must cost a message
/// rather than a frozen window - the same reason <c>Interpreter</c> has one; it is a guard rather than a verdict,
/// and one pass short is exactly what it reports. And monotonicity: a pinned variable stays pinned and a bound is
/// only ever added, so a pass that changed nothing is a fixed point and the loop ends.
/// </para>
/// <para>
/// It is a bounds solver, not a unification engine. <c>?a == int</c> is a fact, <c>?a &lt;: number</c> is a bound,
/// and a variable is given a type only when its lower bound names one and its upper bounds all accept it. That
/// covers the cookbook's rule shapes; what it deliberately does not do is invent the missing side of an equation -
/// <c>?a == ?b</c> relates two unknowns and stays a relation, which is honest rather than solved by guessing.
/// </para>
/// <para>
/// A claim about a compound type is rewritten before it is judged, by a <see cref="TypeReplacement"/>: a list
/// against a list becomes a claim about their elements, and an optional against an optional a claim about their
/// inners, because those are the two shapes whose part is a single named type. The rules are the framework's own
/// (<see cref="TypeReplacements.Covariance"/>) plus whatever the language declares, so what the solver knows about
/// a list is stated in the same vocabulary a language uses to add a rule of its own. A record is not descended
/// into: its fields are compared by name, and decomposing two records would first need the answer to "do these two
/// shapes correspond", which is the same question the solve is running.
/// </para>
/// </remarks>
public sealed class TypeSolver
{
    /// <summary>How many narrowing steps one solve may spend unless the caller says otherwise.</summary>
    /// <remarks>
    /// Generous for a real rule table and small enough that a cyclical declaration surfaces as a failure in a
    /// moment. A rule table whose solve needs more than this is a rule table with a loop in it.
    /// </remarks>
    public const int DefaultBudget = 1_000;

    private readonly ISubtype _subtypes;
    private readonly ComparisonRelation _comparisons;
    private readonly IReadOnlyList<TypeReplacement> _replacements;

    /// <summary>Creates a solver that decides concrete edges with <paramref name="subtypes"/>.</summary>
    /// <param name="subtypes">
    /// The subtype relation the language declared, or <see langword="null"/> for
    /// <see cref="SubtypeRelation.Primitives"/> - which answers "a type is itself" and nothing more.
    /// </param>
    /// <param name="comparisons">
    /// The types the language declared interchangeable, or <see langword="null"/> for
    /// <see cref="ComparisonRelation.Empty"/> - which relates a type to itself only. They decide whether two types
    /// may be one type; they deliberately do not widen <see cref="ISubtype"/>, so declaring two types
    /// interchangeable does not also put either below the other.
    /// </param>
    /// <param name="replacements">
    /// The replacements the language declared, which are tried after the framework's own
    /// (<see cref="TypeReplacements.Covariance"/>) - or <see langword="null"/> for just those.
    /// </param>
    /// <param name="budget">How many narrowing steps one solve may spend.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="budget"/> is not positive.</exception>
    public TypeSolver(
        ISubtype? subtypes = null,
        ComparisonRelation? comparisons = null,
        IReadOnlyList<TypeReplacement>? replacements = null,
        int budget = DefaultBudget)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budget);

        _subtypes = subtypes ?? SubtypeRelation.Primitives;
        _comparisons = comparisons ?? ComparisonRelation.Empty;
        _replacements = replacements is null or { Count: 0 }
            ? TypeReplacements.Covariance
            : [.. TypeReplacements.Covariance, .. replacements];
        Budget = budget;
    }

    /// <summary>How many narrowing steps one solve may spend.</summary>
    public int Budget { get; }

    /// <summary>The types that satisfy <paramref name="equations"/>, or why no attempt did.</summary>
    /// <param name="equations">The relations the rules stated.</param>
    /// <remarks>
    /// An empty set of equations solves trivially: nothing was asked, so nothing is unsatisfied, and every
    /// variable stays a variable.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="equations"/> is <see langword="null"/>.</exception>
    public TypeSolution Solve(IEnumerable<TypeEquation> equations)
    {
        ArgumentNullException.ThrowIfNull(equations);

        var pending = new List<TypeEquation>(equations);
        var must = new Dictionary<TypeVariable, IType>();
        var lower = new Dictionary<TypeVariable, List<IType>>();
        var upper = new Dictionary<TypeVariable, List<IType>>();
        var steps = 0;

        // The fixpoint. A pass that changes nothing is the answer; a pass that changes something is re-run,
        // because a variable solved in this pass may satisfy the equation the previous pass could not.
        while (pending.Count > 0)
        {
            if (++steps > Budget)
            {
                return new TypeSolution(types: new Dictionary<TypeVariable, IType>(), $"The equations did not settle within {Budget} step(s); a rule table that needs more passes than this has a loop in it, or one deeper than the budget is worth.", steps);
            }

            var changed = false;
            List<TypeEquation> next = [];

            foreach (var equation in pending)
            {
                if (Narrow(equation, must, lower, upper, next, out var failure))
                {
                    changed = true;

                    continue;
                }

                if (failure is not null)
                {
                    return new TypeSolution(types: new Dictionary<TypeVariable, IType>(), failure, steps);
                }

                next.Add(equation);
            }

            pending = next;

            if (!changed)
            {
                break;
            }
        }

        // An equality pins a type, so it is an answer already; a bound only narrows, and a variable whose lower
        // bound names one type is that type once its upper bounds accept it. What is left over stays a variable:
        // assigning a type nothing demanded is the guess this layer exists to refuse.
        Dictionary<TypeVariable, IType> solved = new(must);

        foreach (var (variable, candidates) in lower)
        {
            if (solved.ContainsKey(variable) || candidates.Count != 1)
            {
                continue;
            }

            var candidate = candidates[0];

            if (upper.TryGetValue(variable, out var bounds) && bounds.Any(bound => !_subtypes.IsSubtype(candidate, bound)))
            {
                return new TypeSolution(
                    types: new Dictionary<TypeVariable, IType>(),
                    $"{variable.Name} can only be {candidate.Name}, but the rules require it to be a {string.Join(" and a ", bounds.Select(bound => bound.Name))}.",
                    steps);
            }

            solved[variable] = candidate;
        }

        return new TypeSolution(solved, null, steps);
    }

    /// <summary>Applies one equation, and answers whether it changed what is known.</summary>
    /// <remarks>
    /// The three shapes a side can arrive in decide everything: a solved or concrete type is a fact, a variable is
    /// something to constrain, and a list of a variable is a structure whose equality splits into its parts. The
    /// equations that cannot be applied yet are handed to <paramref name="next"/> rather than dropped, because the
    /// pass that binds their variable comes later.
    /// </remarks>
    private bool Narrow(
        TypeEquation equation,
        Dictionary<TypeVariable, IType> must,
        Dictionary<TypeVariable, List<IType>> lower,
        Dictionary<TypeVariable, List<IType>> upper,
        List<TypeEquation> next,
        out string? failure)
    {
        failure = null;

        var left = Substitute(equation.Left, must);
        var right = Substitute(equation.Right, must);

        if (equation.Kind == TypeEquationKind.Supertype)
        {
            (left, right) = (right, left);
        }

        if (equation.Kind == TypeEquationKind.Equal)
        {
            return Equal(left, right, must, lower, upper, next, out failure);
        }

        return Subtype(left, right, lower, upper, next, out failure);
    }

    /// <summary>One equality: bind, decompose, or keep for a later pass.</summary>
    private bool Equal(
        IType left,
        IType right,
        Dictionary<TypeVariable, IType> must,
        Dictionary<TypeVariable, List<IType>> lower,
        Dictionary<TypeVariable, List<IType>> upper,
        List<TypeEquation> next,
        out string? failure)
    {
        failure = null;

        if (ReferenceEquals(left, right))
        {
            return false;
        }

        switch (left, right)
        {
            case (TypeVariable a, TypeVariable b):
                // Two unknowns stated equal is a relation to carry, not something to invent: giving either one the
                // other's name would make "?a == ?b" look solved.
                next.Add(TypeEquation.Equal(a, b));

                return false;

            case (TypeVariable variable, var type):
                return Bind(variable, type, must);

            case (var type, TypeVariable variable):
                return Bind(variable, type, must);

            case var _ when Replace(TypeEquationKind.Equal, left, right, next):
                return true;

            default:
                // Two concrete types are a fact the relation settles; a disagreement is the one thing a solve can
                // report as a contradiction rather than as an unfinished job. Comparison is asked as well as
                // subtyping, because a language may say two types are interchangeable without putting either below
                // the other.
                if (!_subtypes.IsComparableTo(left, right) && !_comparisons.IsComparableTo(left, right))
                {
                    failure = $"{left.Name} and {right.Name} cannot be one type.";
                }

                return false;
        }
    }

    /// <summary>Records that <paramref name="variable"/> is exactly <paramref name="type"/>.</summary>
    /// <remarks>
    /// Two equalities that disagree do not meet here: <see cref="Substitute"/> replaces a pinned variable before the
    /// next equation is read, so the second equality arrives as two concrete sides and the contradiction is reported
    /// by the branch that compares them - which names both types instead of naming a variable and one of them.
    /// </remarks>
    private static bool Bind(TypeVariable variable, IType type, Dictionary<TypeVariable, IType> must)
    {
        if (must.TryGetValue(variable, out var previous))
        {
            return !ReferenceEquals(previous, type);
        }

        must[variable] = type;

        return true;
    }

    /// <summary>One subtyping: a bound on a variable, or a fact the relation settles.</summary>
    private bool Subtype(
        IType left,
        IType right,
        Dictionary<TypeVariable, List<IType>> lower,
        Dictionary<TypeVariable, List<IType>> upper,
        List<TypeEquation> next,
        out string? failure)
    {
        failure = null;

        switch (left, right)
        {
            case (TypeVariable variable, var supertype):
                // "something <: the variable", so whatever it turns out to be must be at least this.
                return Add(upper, variable, supertype);

            case (var subtype, TypeVariable variable):
                // "the variable <: something", so whatever it turns out to be must be at most this.
                return Add(lower, variable, subtype);

            case var _ when Replace(TypeEquationKind.Subtype, left, right, next):
                return true;

            default:
                if (!_subtypes.IsSubtype(left, right))
                {
                    failure = $"{left.Name} may not stand where {right.Name} is required.";
                }

                return false;
        }
    }

    /// <summary>Rewrites a claim the language has a replacement for, stating what to prove instead.</summary>
    /// <remarks>
    /// Consulted between the cases that <em>constrain</em> a side and the one that decides a fact, which is the only
    /// place it can stand: a variable has to be bound before a pattern could match it, and a claim is a fact only
    /// once no rule says what it really means. The first match wins, in the order the language declared, so a
    /// rule table that says two things about one pair means the one written first.
    /// </remarks>
    private bool Replace(TypeEquationKind claim, IType left, IType right, List<TypeEquation> next)
    {
        foreach (var replacement in _replacements)
        {
            if (replacement.Claim != claim || !replacement.TryApply(left, right, out var instead))
            {
                continue;
            }

            next.AddRange(instead);

            return true;
        }

        return false;
    }

    /// <summary>Adds <paramref name="type"/> to one side of <paramref name="variable"/>'s bounds.</summary>
    /// <remarks>
    /// Bounds only grow, which is what makes the fixpoint terminate: a pass that added nothing has nothing left to
    /// do. The comparison is by reference because a bound is a type instance a rule produced; two structurally
    /// equal lists built on two passes are two bounds, and the extra one only costs a step.
    /// </remarks>
    private static bool Add(Dictionary<TypeVariable, List<IType>> bounds, TypeVariable variable, IType type)
    {
        if (!bounds.TryGetValue(variable, out var types))
        {
            bounds[variable] = [type];

            return true;
        }

        if (types.Any(each => ReferenceEquals(each, type)))
        {
            return false;
        }

        types.Add(type);

        return true;
    }

    /// <summary><paramref name="type"/> with every variable that is already pinned replaced.</summary>
    private static IType Substitute(IType type, Dictionary<TypeVariable, IType> must) => type switch
    {
        TypeVariable variable when must.TryGetValue(variable, out var replacement) => replacement,
        _ => type,
    };

    public override string ToString() => $"budget {Budget}";
}
