namespace Projectional.Language;

/// <summary>One relation a type rule states between two types, which may be <see cref="TypeVariable"/>s.</summary>
/// <remarks>
/// <para>
/// This is what replaces MPS' statement list: a rule does not assign a type to a variable, it states that a
/// relation holds, and the solver finds an assignment that makes all of them hold at once. That is why the two
/// sides are <see cref="IType"/> and not "a variable and a type" - the interesting equations relate two things
/// the rule cannot name yet.
/// </para>
/// <para>
/// A value, not a node: an equation is produced per <see cref="ITypeRule"/> evaluation and lives only as long as
/// one solve, so nothing here is stored, journaled or persisted - the same reason a type itself is computed.
/// </para>
/// </remarks>
/// <param name="Kind">Which relation the two sides stand in.</param>
/// <param name="Left">The left side.</param>
/// <param name="Right">The right side.</param>
public readonly record struct TypeEquation(TypeEquationKind Kind, IType Left, IType Right)
{
    /// <summary>The two sides are the same type.</summary>
    /// <param name="left">One side.</param>
    /// <param name="right">The other side.</param>
    public static TypeEquation Equal(IType left, IType right) => new(TypeEquationKind.Equal, left, right);

    /// <summary>The left side may stand where the right side is required.</summary>
    /// <param name="subtype">The type that may stand in place of the other.</param>
    /// <param name="supertype">The type it may stand in place of.</param>
    public static TypeEquation Subtype(IType subtype, IType supertype) =>
        new(TypeEquationKind.Subtype, subtype, supertype);

    /// <summary>The right side may stand where the left side is required.</summary>
    /// <param name="supertype">The type it may stand in place of.</param>
    /// <param name="subtype">The type that may stand in place of the other.</param>
    /// <remarks>
    /// The cookbook writes this direction as <c>:&gt;=:</c>. It is stated rather than left to the caller as
    /// <c>Subtype(subtype, supertype)</c> because which side carries the constraint is what a reader of a rule
    /// follows, and reversing arguments at the call site is how a rule comes to mean the opposite of what it says.
    /// </remarks>
    public static TypeEquation Supertype(IType supertype, IType subtype) =>
        new(TypeEquationKind.Supertype, supertype, subtype);

    public override string ToString() => Kind switch
    {
        TypeEquationKind.Equal => $"{Left.Name} == {Right.Name}",
        TypeEquationKind.Subtype => $"{Left.Name} <: {Right.Name}",
        _ => $"{Left.Name} :> {Right.Name}",
    };
}
