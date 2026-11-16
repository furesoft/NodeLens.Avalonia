namespace Projectional.Expressions;

/// <summary>
/// The keys of the concepts <see cref="ExpressionsLanguage"/> declares.
/// </summary>
/// <remarks>
/// A host that puts its own concepts next to these refers to them by key, so the spelling lives in
/// one place: a slot declared over <c>Expressions.Expression</c> and a notation registered for
/// <c>Expressions.Binary</c> both have to mean the same concept, and a typo would otherwise show up as
/// a missing notation or a rejected declaration rather than as a compile error.
/// </remarks>
public static class ExpressionsConceptKeys
{
    /// <summary>The abstract base of every expression, which is what a host declares its slots over.</summary>
    public const string Expression = "Expressions.Expression";

    /// <summary>A whole number, in its <c>value</c> property.</summary>
    public const string IntLiteral = "Expressions.IntLiteral";

    /// <summary>A truth value, in its <c>value</c> property.</summary>
    public const string BoolLiteral = "Expressions.BoolLiteral";

    /// <summary>A quoted piece of text, in its <c>value</c> property.</summary>
    public const string StringLiteral = "Expressions.StringLiteral";

    /// <summary>A name, in its <c>name</c> property, which is also its name slot.</summary>
    public const string Name = "Expressions.Name";

    /// <summary>Two operands around an operator, in <c>left</c> and <c>right</c>.</summary>
    public const string Binary = "Expressions.Binary";

    /// <summary>One operator in front of its <c>operand</c>.</summary>
    public const string Unary = "Expressions.Unary";

    /// <summary>An <c>inner</c> expression between parentheses, which is how a user says how to read a tree.</summary>
    public const string Parenthesized = "Expressions.Parenthesized";

    /// <summary>A choice: a <c>condition</c>, the <c>then</c> expression and the <c>else</c> expression.</summary>
    public const string If = "Expressions.If";
}
