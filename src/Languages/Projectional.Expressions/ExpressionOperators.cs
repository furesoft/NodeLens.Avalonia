using Projectional.Core;

namespace Projectional.Expressions;

/// <summary>
/// The operator vocabulary of <see cref="ExpressionsLanguage"/>: which symbols a binary and a unary
/// expression accept, and the property types that declare them.
/// </summary>
/// <remarks>
/// <para>
/// An operator is a property rather than a node, because it is a choice out of a fixed set: its
/// spelling is also its stored value, so nothing has to map a symbol to an enum and back, and a
/// user typing <c>+</c> into the cell is writing the model.
/// </para>
/// <para>
/// Precedence is not here because it is not part of the projection. The tree says what the
/// expression means and <see cref="ExpressionsConceptKeys.Parenthesized"/> says how a user overrides
/// the reading; an unparenthesized tree renders in its own structure whatever the operator symbols
/// are. Associativity and precedence belong to whoever analyses the tree, and a language that wants
/// them can wrap or replace these types.
/// </para>
/// </remarks>
public static class ExpressionOperators
{
    private static readonly string[] BinarySymbols = ["+", "-", "*", "/", "^", "==", "!=", "<", ">", "<=", ">=", "&&", "||"];
    private static readonly string[] UnarySymbols = ["-", "!"];

    /// <summary>The symbols a <see cref="ExpressionsConceptKeys.Binary"/> accepts.</summary>
    public static IReadOnlyList<string> Binary => BinarySymbols;

    /// <summary>The symbols a <see cref="ExpressionsConceptKeys.Unary"/> accepts.</summary>
    public static IReadOnlyList<string> Unary => UnarySymbols;

    /// <summary>The type of the <c>operator</c> property of a binary expression.</summary>
    public static PropertyType BinaryType { get; } = PropertyType.Enum(BinarySymbols);

    /// <summary>The type of the <c>operator</c> property of a unary expression.</summary>
    public static PropertyType UnaryType { get; } = PropertyType.Enum(UnarySymbols);
}
