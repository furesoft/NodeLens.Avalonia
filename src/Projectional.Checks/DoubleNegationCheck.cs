using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Checks;

/// <summary>A negation whose operand is negated again by the same operator, so the two cancel out.</summary>
/// <remarks>
/// <para>
/// <c>!!x</c> and <c>--x</c> both mean <c>x</c> - which is why the operator has to be <em>the same</em> in both
/// nodes: <c>!-x</c> is not a double negation, and removing both would change what the expression says. The check
/// therefore asks two questions of the declaration, not of a symbol table: is the node a <c>Expressions.Unary</c>,
/// and is its <c>operand</c> one whose operator cell holds the same text.
/// </para>
/// <para>
/// The finding names the <em>outer</em> node, at its operator cell, because that is the word the reader sees twice.
/// The repair is one program of two unwraps - the inner negation takes the outer one's place and is then unwrapped
/// again - so the result is the operand itself, in one undo step. A pair at the very top of a document is not
/// reported: unwrapping a model root would delete the node the editor draws, which is why that repair cannot run.
/// </para>
/// <para>
/// Two unwraps rather than one: unwrapping the outer alone leaves <c>!x</c>, which removes one operator and not the
/// pair the finding is about. That the program is the honest shape is also why the model needed `Unwrap` at all -
/// <c>Model.Wrap</c> goes the other direction, and no seam hoisted a child into its parent's place.
/// </para>
/// </remarks>
public sealed class DoubleNegationCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "STD041";

    string ICheck.Code => Code;

    string ICheck.Title => "A negation whose operand is negated again by the same operator, so the two cancel out.";

    /// <summary>The stable identifier of the repair that removes both negations.</summary>
    public const string FixId = "std.double-negation.remove";

    private const string OperatorSlot = "operator";
    private const string OperandSlot = "operand";

    /// <summary>Every negation of a negation in <paramref name="model"/>, in document order.</summary>
    /// <param name="model">The document to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One fix per pair, targeting the outer node's operator so the marker lands under the word the user reads
    /// twice.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> fixes = [];

        foreach (var node in NodeWalk.Of(model))
        {
            // A finding's repair has to be able to run, and unwrapping a model root cannot: the document a session
            // draws *is* a root, so deleting it would take away the node the editor is built from - the same reason
            // "replace" refuses one. A double negation at the very top of a document is therefore not reported.
            if (node.Parent is null
                || !node.Concept.IsKindOf(ExpressionsConceptKeys.Unary)
                || node.GetOrNull(OperatorSlot) is not { Length: > 0 } outer
                || node.GetChild(OperandSlot) is not { } inner
                || !inner.Concept.IsKindOf(ExpressionsConceptKeys.Unary)
                || !string.Equals(outer, inner.GetOrNull(OperatorSlot), StringComparison.Ordinal))
            {
                continue;
            }

            var outerId = node.Id;
            var innerId = inner.Id;

            fixes.Add(new DiagnosticFix(
                new Diagnostic(
                    DiagnosticSeverity.Warning,
                    outerId,
                    $"\"{outer}\" negates a negation with the same operator, so the two cancel each other out.",
                    role: OperatorSlot,
                    code: Code),
                FixId,
                "Remove both",
                context => context.Node is null
                    ? null
                    : EditProgram.Of(
                        new UnwrapAction(outerId, OperandSlot),
                        new UnwrapAction(innerId, OperandSlot)),
                "Removes both operators and keeps the operand, which is what the two of them say."));
        }

        return fixes;
    }
}