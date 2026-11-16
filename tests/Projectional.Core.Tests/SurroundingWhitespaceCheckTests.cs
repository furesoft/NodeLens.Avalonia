using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// <c>STD003</c>: a name with whitespace in front of or behind it, which the editor cannot show.
/// </summary>
/// <remarks>
/// A name that is nothing but whitespace is deliberately left alone: trimming it would produce the empty
/// name, which is a different finding - and one this check has no repair for.
/// </remarks>
public sealed class SurroundingWhitespaceCheckTests
{
    private static SurroundingWhitespaceCheck Check => new();

    [Fact]
    public void ANameWithSurroundingSpaceIsReported()
    {
        var model = Document(" Check ");

        var fix = Assert.Single(Check.Find(model));
        var finding = fix.Diagnostic;

        Assert.Equal(SurroundingWhitespaceCheck.Code, finding.Code);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
        Assert.Equal("name", finding.Role);
        Assert.Equal("Trim to \"Check\"", fix.Label);
    }

    [Fact]
    public void ACleanNameIsNotReported()
    {
        Assert.Empty(Check.Find(Document("Check")));
    }

    [Fact]
    public void ANameThatIsOnlyWhitespaceIsLeftAlone()
    {
        Assert.Empty(Check.Find(Document("   ")));
    }

    [Fact]
    public void AConceptWithoutANameSlotIsNotConsidered()
    {
        // An integer literal has a value, not a name, so whitespace in one is the type's business.
        var model = RulesLanguage.NewProject().NewModel("expressions");
        var literal = model.NewNode(ExpressionsConceptKeys.IntLiteral);
        model.AddRoot(literal);

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void TheRepairTrimsTheNameAndUndoTakesItBack()
    {
        var model = Document(" Check ");
        var rule = model.Roots[0].GetChildren(RulesLanguage.RulesSlot)[0];
        var fix = Assert.Single(Check.Find(model));

        using var session = new EditorSession(model.Roots[0], RulesLanguage.CreateNotations(RulesLanguage.Concepts));
        session.Markers = new MarkerSet(fix.Diagnostic);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Equal("Check", rule.GetOrNull("name"));
        Assert.Empty(Check.Find(model));

        Assert.True(session.Undo());
        Assert.Equal(" Check ", rule.GetOrNull("name"));
    }

    [Fact]
    public void AModelThatIsNullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Check.Find(null!));
    }

    private static Model Document(string name)
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        ruleset.Set("name", "Payroll");
        model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule).Set("name", name);

        return model;
    }
}
