namespace Projectional.Language;

/// <summary>A type a rule cannot name yet: the solver's unknown, written with a <c>?</c>.</summary>
/// <remarks>
/// <para>
/// MPS writes these as <c>var elementType</c> and ties them between two statements with anti-quotation. The
/// variable is the same idea without the syntax: a rule declares one, states equations over it, and the solver
/// replaces it with whatever satisfies them. Anti-quotation is unnecessary because a variable is a value the
/// rule holds rather than a fragment it splices into a statement.
/// </para>
/// <para>
/// It is an <see cref="IType"/> so that an equation can relate a variable to a type without a second kind of
/// operand - and it is a distinct class rather than an <see cref="UnknownType"/>, because the two answer opposite
/// questions. <c>unknown</c> means "nothing is known and nothing more will be"; a variable means "this will be
/// known once the solve runs". Collapsing them would let an unsolved variable pass for an answer.
/// </para>
/// <para>
/// The name is for people - it is what a diagnostic and a failed equation quote - and is not an identity: two
/// variables with one name are two variables, because a rule that meant one variable holds one instance. The
/// declared form of the aspect validates uniqueness when a language names them in a model.
/// </para>
/// </remarks>
public sealed class TypeVariable : IType
{
    /// <summary>Creates a variable called <paramref name="name"/>.</summary>
    /// <param name="name">The name the variable is read and reported under.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public TypeVariable(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Identifier = name;
    }

    /// <summary>The name the variable is read and reported under.</summary>
    public string Identifier { get; }

    /// <inheritdoc />
    /// <remarks>The <c>?</c> is what makes a half-solved answer recognisable in a message, so it is part of the
    /// spelling rather than decoration.</remarks>
    public string Name => "?" + Identifier;

    /// <inheritdoc />
    /// <remarks>
    /// A variable conforms to nothing on its own: whether it does is what the solve decides, and an answer of
    /// "yes" before then would be the guess this whole layer refuses. So the default implementation, which asks
    /// <see cref="SubtypeRelation.Primitives"/>, would answer "only itself" - which is exactly right, since the
    /// relation cannot know a variable either.
    /// </remarks>
    public override string ToString() => Name;
}
