using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// One offer: an identity, the text a menu shows, the sentence that explains it, and the edit it produces.
/// </summary>
/// <remarks>
/// The two rules that carry the design are that the description is required (an intention has no finding to
/// fall back on) and that a per-caret answer is a copy rather than a mutation, so a declared catalogue can be
/// asked about any caret without one answer changing the next.
/// </remarks>
public sealed class IntentionTests
{
    [Fact]
    public void AnIntentionCarriesWhatAMenuNeeds()
    {
        var intention = Rename();

        Assert.Equal("test.rule.rename", intention.Id);
        Assert.Equal("Rename", intention.Label);
        Assert.Equal("Renames the rule.", intention.Description);
        Assert.Null(intention.Gesture);
        Assert.True(intention.Availability.IsApplicable);
        Assert.Equal(EditSafety.Plain, intention.Safety);
        Assert.False(intention.IsDestructive);
        Assert.Null(intention.Fix);
    }

    [Fact]
    public void TheDescriptionIsRequired()
    {
        // A fix falls back to the finding's message; an intention has nothing behind it, so a label without a
        // sentence would be a riddle nothing downstream could solve.
        Assert.Throws<ArgumentException>(() => new Intention("id", "label", string.Empty, _ => null));
        Assert.Throws<ArgumentException>(() => new Intention("id", "label", "   ", _ => null));
        Assert.Throws<ArgumentException>(() => new Intention(string.Empty, "label", "does it", _ => null));
        Assert.Throws<ArgumentException>(() => new Intention("id", string.Empty, "does it", _ => null));
        Assert.Throws<ArgumentNullException>(() => new Intention("id", "label", "does it", null!));
    }

    [Fact]
    public void AGestureMustNotBeEmptyWhenItIsGiven()
    {
        Assert.Throws<ArgumentException>(() => new Intention("id", "label", "does it", _ => null, string.Empty));
        Assert.Equal("Ctrl+K", new Intention("id", "label", "does it", _ => null, "Ctrl+K").Gesture);
    }

    [Fact]
    public void WithAnswersACopyForOneCaret()
    {
        var declared = Rename();

        var absent = declared.With(IntentionAvailability.Absent);

        Assert.True(absent.IsAbsent);
        Assert.True(declared.Availability.IsApplicable);
        Assert.Equal(declared.Id, absent.Id);
        Assert.Same(declared.Create, absent.Create);
    }

    [Fact]
    public void ADestructiveIntentionSaysSo()
    {
        var intention = new Intention(
            "test.rule.delete",
            "Delete",
            "Removes the rule.",
            _ => null,
            safety: EditSafety.Destructive);

        Assert.True(intention.IsDestructive);
        Assert.Equal(EditSafety.Destructive, intention.Safety);
    }

    [Fact]
    public void AFixBecomesAnEntryThatKeepsTheFix()
    {
        var rule = TestEditor.BareRule();
        var finding = new Diagnostic(
            DiagnosticSeverity.Warning,
            rule.Id,
            "the name says nothing",
            role: "name",
            code: "TEST001");
        var fix = new DiagnosticFix(
            finding,
            "std.trim",
            "Trim to \"walk\"",
            _ => new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "walk"),
            "Removes the whitespace.",
            safety: EditSafety.Destructive);

        var intention = Intention.FromFix(fix);

        Assert.Same(fix, intention.Fix);
        Assert.Equal(fix.Id, intention.Id);
        Assert.Equal(fix.Label, intention.Label);
        Assert.Equal(fix.Description, intention.Description);
        Assert.Equal(EditSafety.Destructive, intention.Safety);
        Assert.True(intention.Availability.IsApplicable);
    }

    [Fact]
    public void ADisabledFixBecomesADisabledEntry()
    {
        var rule = TestEditor.BareRule();
        var finding = new Diagnostic(DiagnosticSeverity.Warning, rule.Id, "bad");
        var fix = new DiagnosticFix(
            finding,
            "std.rename",
            "Rename",
            _ => null,
            availability: FixAvailability.Disabled("the model is read-only"));

        var intention = Intention.FromFix(fix);

        Assert.True(intention.IsDisabled);
        Assert.False(intention.IsAbsent);
        Assert.Equal("the model is read-only", intention.Availability.Reason);
    }

    [Fact]
    public void AnEntryFromAFixRepairsTheFixesNodeAndNotTheCarets()
    {
        // A repair repairs the node it was built for; the caret may well be somewhere else in the same line.
        var rule = TestEditor.BareRule();
        var other = rule.Model.NewNode(TestLanguage.Rule);
        rule.Model.AddRoot(other);

        var finding = new Diagnostic(DiagnosticSeverity.Warning, other.Id, "bad", role: "name");
        var fix = new DiagnosticFix(
            finding,
            "std.rename",
            "Rename",
            context => context.Node is { } node
                ? new SetPropertyAction(CellAddress.OfSlot(node.Id, "name"), "named")
                : null);

        using var session = TestEditor.Open(rule);
        var intention = Intention.FromFix(fix);
        var context = session.CreateIntentionContext();

        var action = Assert.IsType<SetPropertyAction>(intention.Create(context));

        Assert.Equal(CellAddress.OfSlot(other.Id, "name"), action.Address);
    }

    [Fact]
    public void AnEntryFromAFixDeclinesOnceItsNodeIsGone()
    {
        // The finding can outlive its node - a check ran, then the user deleted what it pointed at - and a
        // repair must then decline rather than throw inside a menu build.
        var gone = NodeId.New();
        var fix = new DiagnosticFix(
            new Diagnostic(DiagnosticSeverity.Warning, gone, "bad"),
            "std.rename",
            "Rename",
            context => context.Node is null
                ? null
                : new SetPropertyAction(CellAddress.OfSlot(context.Node.Id, "name"), "renamed"));

        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        Assert.Null(Intention.FromFix(fix).Create(session.CreateIntentionContext()));
    }

    private static Intention Rename() =>
        new("test.rule.rename", "Rename", "Renames the rule.", _ => null);
}
