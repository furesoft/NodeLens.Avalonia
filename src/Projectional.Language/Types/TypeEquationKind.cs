namespace Projectional.Language;

/// <summary>Which relation an equation states between its two sides.</summary>
/// <remarks>
/// Three kinds and not two, because the cookbook writes both directions of the subtype relation
/// (<c>:&lt;=:</c> and <c>:&gt;=:</c>) and a rule that had to reverse its own arguments to say one of them
/// would be a rule whose reading depends on which side the author happened to put first. The third kind is
/// therefore a spelling of the second with the sides swapped, and the solver treats it that way - there is no
/// third relation behind it.
/// </remarks>
public enum TypeEquationKind
{
    /// <summary>The two sides are the same type.</summary>
    Equal,

    /// <summary>The left side may stand where the right side is required.</summary>
    Subtype,

    /// <summary>The right side may stand where the left side is required - the same relation, read the other way.</summary>
    Supertype,
}
