using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Semantics;

namespace Projectional.Checks;

/// <summary>A binary expression whose grouping a reader would only get right from operator precedence.</summary>
/// <remarks>
/// <para>
/// The projection draws a binary as <c>left operator right</c> and says nothing about grouping, so the tree
/// is the meaning and the text is only an approximation of it. That is fine while the tree agrees with what
/// the symbols imply, and it is a trap when it does not: <c>1 + 2 * 3</c> drawn for <c>1 + (2 * 3)</c>
/// reads correctly by luck, while <c>(1 + 2) * 3</c> drawn without parentheses reads as something else
/// entirely. Parentheses are the language's own concept for saying the grouping outright, so this check
/// reports the operands that need them and offers exactly that repair.
/// </para>
/// <para>
/// The finding is about the <em>operand</em>, not about its parent. The parent is what makes the grouping
/// wrong, but the parentheses belong around the child - and a finding whose repair does not remove it is a
/// repair the user applies and sees again immediately. Wrapping the child also keeps one rule for every
/// offer in the layer: the node the finding names is the node the repair acts on.
/// </para>
/// <para>
/// Precedence lives here and not in the language: <see cref="ExpressionOperators"/> says explicitly that
/// associativity and precedence belong to whoever analyses the tree, because the projection never needs
/// them. This check is that analyser, so the table is its policy rather than the language's data.
/// </para>
/// <para>
/// A child needs parentheses when it binds looser than its parent, and on the right also when it binds
/// equally: <c>a - (b - c)</c> is a different tree from <c>a - b - c</c>, while <c>(a - b) - c</c> is what
/// the left side already means. An operator the table does not know is left alone: that is a language
/// extending the vocabulary, which is <c>STD042</c>'s question rather than this one's.
/// </para>
/// </remarks>
public sealed class BinaryGroupingCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "STD040";

    string ICheck.Code => Code;

    string ICheck.Title => "A binary expression whose grouping a reader would only get right from operator precedence.";

    /// <summary>The stable identifier of the repair that adds the parentheses.</summary>
    public const string FixId = "std.binary-grouping.wrap";

    private const string OperatorSlot = "operator";
    private const string LeftSlot = "left";
    private const string RightSlot = "right";

    /// <summary>The slot of <see cref="ExpressionsConceptKeys.Parenthesized"/> that holds the grouped expression.</summary>
    private const string WrapperSlot = "inner";

    /// <summary>Every operand in <paramref name="model"/> that is drawn without the parentheses it needs.</summary>
    /// <param name="model">The document to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One fix per operand, in document order. The diagnostic targets that operand, so the marker lands on
    /// the operator that decides the grouping and the repair surrounds the node the finding names.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> fixes = [];

        foreach (var node in NodeWalk.Of(model))
        {
            CollectFrom(node, LeftSlot, left: true, fixes);
            CollectFrom(node, RightSlot, left: false, fixes);
        }

        return fixes;
    }

    /// <summary>Reports <paramref name="slot"/>'s child of <paramref name="parent"/> when it needs brackets.</summary>
    /// <param name="parent">The binary whose operator decides the grouping.</param>
    /// <param name="slot">Which side to ask about.</param>
    /// <param name="left">
    /// Whether this is the left operand. An equally tight child needs brackets only on the right, where
    /// left-associativity makes the drawing read as a different tree.
    /// </param>
    /// <param name="fixes">The list findings are added to.</param>
    private static void CollectFrom(Node parent, string slot, bool left, List<DiagnosticFix> fixes)
    {
        if (RankOf(parent) is not { } rank || RankOf(parent.GetChild(slot)) is not { } child)
        {
            return;
        }

        if (left ? child >= rank : child > rank)
        {
            return;
        }

        var operand = parent.GetChild(slot)!;

        fixes.Add(new DiagnosticFix(
            new Diagnostic(
                DiagnosticSeverity.Warning,
                operand.Id,
                "this operand is grouped by operator precedence rather than by the drawing; parentheses would say it outright.",
                code: Code),
            FixId,
            "Wrap in parentheses",
            context => context.Node is null
                ? null
                : new WrapInAction(context.Node.Id, ExpressionsConceptKeys.Parenthesized, WrapperSlot),
            "Surrounds the operand with parentheses, the concept the language has for grouping."));
    }

    /// <summary>How tightly <paramref name="node"/>'s operator binds, or <see langword="null"/>.</summary>
    /// <remarks>
    /// A node that is not a binary, or whose operator cell is empty, has no rank. That is the honest answer:
    /// the check compares operators it knows, and the fact that an expression is not grouped by this table is
    /// not a finding about the user's document.
    /// </remarks>
    private static int? RankOf(Node? node)
    {
        if (node is null || !node.Concept.IsKindOf(ExpressionsConceptKeys.Binary))
        {
            return null;
        }

        return node.GetOrNull(OperatorSlot) is { } symbol ? Rank(symbol) : null;
    }

    /// <summary>Where an operator sits in the expression grammar: the larger, the tighter it binds.</summary>
    /// <remarks>
    /// The four levels are the ones the language's own symbol list implies, and they are written as one
    /// table rather than derived from anything, because this is a policy the analyser owns. Equality sits
    /// between the comparisons and the logical operators, and the two logical operators sit below them, so
    /// <c>a == b &amp;&amp; c</c> means <c>(a == b) &amp;&amp; c</c> - which is what a reader of the drawn text
    /// expects.
    /// </remarks>
    private static int? Rank(string symbol) => symbol switch
    {
        "||" => 1,
        "&&" => 2,
        "==" or "!=" => 3,
        "<" or ">" or "<=" or ">=" => 4,
        "+" or "-" => 5,
        "*" or "/" => 6,
        "^" => 7,
        _ => null,
    };
}
