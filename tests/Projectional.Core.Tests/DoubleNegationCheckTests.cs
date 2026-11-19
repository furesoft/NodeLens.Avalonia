using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// <c>STD041</c>: a negation whose operand is negated again by the same operator - the pair a reader has to cancel
/// out in their head.
/// </summary>
/// <remarks>
/// The operator has to be the same in both nodes: <c>!-x</c> is not a double negation, and removing both would change
/// what the expression says. The repair is one program of two unwraps, so the result is the operand itself and undo
/// takes it back in one step. The chain sits inside a binary's left operand because a model root cannot be unwrapped,
/// and a finding whose repair cannot run is not reported.
/// </remarks>
public sealed class DoubleNegationCheckTests
{
    private static DoubleNegationCheck Check => new();

    [Fact]
    public void ANegationOfTheSameNegationIsReported()
    {
        var (model, _, outer, _) = Chain("!", "!", "amount");

        var fix = Assert.Single(Check.Find(model));

        Assert.Equal(DoubleNegationCheck.Code, fix.Diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Warning, fix.Diagnostic.Severity);
        Assert.Equal("operator", fix.Diagnostic.Role);
        Assert.Equal(outer.Id, fix.Diagnostic.Target);
        Assert.Equal("Remove both", fix.Label);
        Assert.Equal(EditSafety.Plain, fix.Safety);
        Assert.Contains("!", fix.Diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASingleNegationIsQuiet()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var root = Binary(model);
        var negation = model.NewChild(root, "left", ExpressionsConceptKeys.Unary);

        negation.Set("operator", "!");
        model.NewChild(negation, "operand", ExpressionsConceptKeys.Name).Set("name", "amount");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void TwoDifferentOperatorsAreNotADoubleNegation()
    {
        // `!-x` is not `x`; the operators cancel only when they are the same one.
        var (model, _, _, _) = Chain("!", "-", "amount");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void AParenthesisedOperandIsNotTheSameNegation()
    {
        // `!(!x)` groups the inner negation, and the outer operand is the parenthesis - a node the reader wrote on
        // purpose.
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var root = Binary(model);
        var outer = model.NewChild(root, "left", ExpressionsConceptKeys.Unary);

        outer.Set("operator", "!");
        var parenthesized = model.NewChild(outer, "operand", ExpressionsConceptKeys.Parenthesized);
        var inner = model.NewChild(parenthesized, "inner", ExpressionsConceptKeys.Unary);

        inner.Set("operator", "!");
        model.NewChild(inner, "operand", ExpressionsConceptKeys.Name).Set("name", "amount");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void ADoubleNegationAtTheTopOfTheDocumentIsNotReported()
    {
        // Its repair would have to delete the model root, and the document a session draws *is* a root - so the
        // finding is withheld rather than offered without a cure.
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var outer = model.NewNode(ExpressionsConceptKeys.Unary);

        outer.Set("operator", "!");
        model.AddRoot(outer);

        var inner = model.NewChild(outer, "operand", ExpressionsConceptKeys.Unary);

        inner.Set("operator", "!");
        model.NewChild(inner, "operand", ExpressionsConceptKeys.Name).Set("name", "amount");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void TheRepairRemovesBothOperatorsAndUndoTakesItBack()
    {
        var (model, root, outer, inner) = Chain("-", "-", "amount");
        var name = inner.GetChild("operand")!;
        var fix = Assert.Single(Check.Find(model));

        using var session = new EditorSession(
            root,
            ExpressionsLanguage.CreateNotations(ExpressionsLanguage.Concepts),
            typing: ExpressionsLanguage.TypingRules);

        session.Markers = new MarkerSet(fix.Diagnostic);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Same(name, root.GetChild("left"));
        Assert.Empty(Check.Find(model));

        Assert.True(session.Undo());
        Assert.Same(outer, root.GetChild("left"));
        Assert.Same(inner, outer.GetChild("operand"));
        Assert.Single(Check.Find(model));
    }

    [Fact]
    public void AModelThatIsNullIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => Check.Find(null!));

    /// <summary>A document holding a binary whose left operand is <c>outer inner name</c>.</summary>
    private static (Model Model, Node Root, Node Outer, Node Inner) Chain(
        string outerOperator,
        string innerOperator,
        string name)
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var root = Binary(model);
        var outer = model.NewChild(root, "left", ExpressionsConceptKeys.Unary);

        outer.Set("operator", outerOperator);

        var inner = model.NewChild(outer, "operand", ExpressionsConceptKeys.Unary);

        inner.Set("operator", innerOperator);
        model.NewChild(inner, "operand", ExpressionsConceptKeys.Name).Set("name", name);

        return (model, root, outer, inner);
    }

    /// <summary>A binary root, so an unwrap below it has a place to give up.</summary>
    private static Node Binary(Model model)
    {
        var root = model.NewNode(ExpressionsConceptKeys.Binary);

        root.Set("operator", "+");
        model.AddRoot(root);

        return root;
    }
}