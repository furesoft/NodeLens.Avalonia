using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// <c>STD040</c>: a binary whose drawn form does not say how it is grouped.
/// </summary>
/// <remarks>
/// The tree is the meaning and the notation draws no parentheses of its own, so the check reports the trees
/// a reader would get wrong: a looser operand, and an equally tight one on the right, where left-associativity
/// silently changes the shape.
/// </remarks>
public sealed class BinaryGroupingCheckTests
{
    private static BinaryGroupingCheck Check => new();

    [Fact]
    public void ALooserLeftOperandIsReported()
    {
        var (model, rule) = Document();
        var product = Binary(model, rule, "condition", "*");
        var sum = Binary(model, product, "left", "+");
        Int(model, product, "right", 3);

        var fix = Assert.Single(Check.Find(model));

        Assert.Equal(BinaryGroupingCheck.Code, fix.Diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Warning, fix.Diagnostic.Severity);
        Assert.Equal(sum.Id, fix.Diagnostic.Target);
        Assert.Equal("Wrap in parentheses", fix.Label);
        Assert.Equal(EditSafety.Plain, fix.Safety);
    }

    [Fact]
    public void ATighterOperandIsNotReported()
    {
        var (model, rule) = Document();
        var sum = Binary(model, rule, "condition", "+");
        Binary(model, sum, "left", "*");
        Int(model, sum, "right", 1);

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void AnEquallyTightLeftOperandIsNotReported()
    {
        // a - b - c already means (a - b) - c, so the left side needs nothing said about it.
        var (model, rule) = Document();
        var outer = Binary(model, rule, "condition", "-");
        Binary(model, outer, "left", "-");
        Int(model, outer, "right", 3);

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void AnEquallyTightRightOperandIsReported()
    {
        // a - (b - c) draws as a - b - c, which reads as (a - b) - c - so the right side needs the mark.
        var (model, rule) = Document();
        var outer = Binary(model, rule, "condition", "-");
        Int(model, outer, "left", 1);
        var inner = Binary(model, outer, "right", "-");

        var fix = Assert.Single(Check.Find(model));

        Assert.Equal(inner.Id, fix.Diagnostic.Target);
    }

    [Fact]
    public void ABinaryWithoutAnOperatorIsLeftAlone()
    {
        var (model, rule) = Document();
        var binary = Binary(model, rule, "condition", null);
        Binary(model, binary, "left", "+");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void TheRepairWrapsTheOperandTheFindingNames()
    {
        var (model, rule) = Document();
        var product = Binary(model, rule, "condition", "*");
        var sum = Binary(model, product, "left", "+");
        Int(model, product, "right", 3);

        var fix = Assert.Single(Check.Find(model));

        using var session = new EditorSession(model.Roots[0], RulesLanguage.CreateNotations(RulesLanguage.Concepts));
        session.Markers = new MarkerSet(fix.Diagnostic);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);

        var operand = Assert.Single(product.GetChildren("left"));

        Assert.Equal(ExpressionsConceptKeys.Parenthesized, operand.Concept.Key);
        Assert.Same(sum, operand.GetChild("inner"));

        // The parentheses went where the misreading was, so the finding is gone rather than moved.
        Assert.Empty(Check.Find(model));

        Assert.True(session.Undo());
        Assert.Same(sum, Assert.Single(product.GetChildren("left")));
    }

    [Fact]
    public void AModelThatIsNullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Check.Find(null!));
    }

    /// <summary>A document with one named rule whose condition slot is still free.</summary>
    private static (Model Model, Node Rule) Document()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", "Approved");

        return (model, rule);
    }

    private static Node Binary(Model model, Node parent, string slot, string? @operator)
    {
        var binary = model.NewChild(parent, slot, ExpressionsConceptKeys.Binary);

        if (@operator is not null)
        {
            binary.Set("operator", @operator);
        }

        return binary;
    }

    private static void Int(Model model, Node parent, string slot, int value) =>
        model.NewChild(parent, slot, ExpressionsConceptKeys.IntLiteral).Set("value", value);
}
