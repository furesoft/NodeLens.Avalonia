using Projectional.Editing;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A fix set is the batch a check hands to the editor beside its findings: keyed by finding, ordered, and
/// strict about two repairs answering one question.
/// </summary>
/// <remarks>
/// Order is part of the contract for the same reason it is in a marker set - the menu a user opens twice
/// must not shuffle - and the duplicate rule is the one place this type is stricter than its marker
/// counterpart, because two fixes with one id for one finding is a bug with no visible symptom.
/// </remarks>
public sealed class FixSetTests
{
    [Fact]
    public void TheEmptySetHoldsNothing()
    {
        var diagnostic = Finding("bad");

        Assert.Equal(0, FixSet.Empty.Count);
        Assert.Empty(FixSet.Empty.For(diagnostic));
    }

    [Fact]
    public void FixesKeepTheOrderTheyWereBuiltWith()
    {
        var diagnostic = Finding("bad");

        var set = new FixSet(
            new DiagnosticFix(diagnostic, "second", "Second", _ => null),
            new DiagnosticFix(diagnostic, "first", "First", _ => null));

        Assert.Equal(["Second", "First"], set.For(diagnostic).Select(fix => fix.Label));
    }

    [Fact]
    public void ForAnswersAboutOneFindingOnly()
    {
        var first = Finding("first");
        var second = Finding("second");

        var set = new FixSet(
            new DiagnosticFix(first, "a", "A", _ => null),
            new DiagnosticFix(second, "b", "B", _ => null),
            new DiagnosticFix(first, "c", "C", _ => null));

        Assert.Equal(["A", "C"], set.For(first).Select(fix => fix.Label));
        Assert.Equal(["B"], set.For(second).Select(fix => fix.Label));
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void ForAFindingNobodyRepairedIsEmpty()
    {
        // A check that knows about a problem but not its cure is an ordinary case: the answer is
        // "nothing to offer", not a miss and not null.
        var rule = TestEditor.BareRule();
        var repaired = new Diagnostic(DiagnosticSeverity.Error, rule.Id, "one");
        var unrepaired = new Diagnostic(DiagnosticSeverity.Error, rule.Id, "two");

        var set = new FixSet(new DiagnosticFix(repaired, "a", "A", _ => null));

        Assert.Empty(set.For(unrepaired));
    }

    [Fact]
    public void TheFindingIsMatchedByValueNotByReference()
    {
        // A host that recomputed its checks has a new instance describing the same finding, and it has to
        // get the same repairs back - which is why the key is the record rather than object identity.
        var rule = TestEditor.BareRule();
        var reported = new Diagnostic(DiagnosticSeverity.Error, rule.Id, "bad", role: "name");
        var recomputed = new Diagnostic(DiagnosticSeverity.Error, rule.Id, "bad", role: "name");

        var set = new FixSet(new DiagnosticFix(reported, "a", "A", _ => null));

        Assert.Single(set.For(recomputed));
    }

    [Fact]
    public void ADifferentMessageIsADifferentFinding()
    {
        // Rewording a finding is rewriting the explanation its repairs were built from, so the old fixes
        // must not answer the new message.
        var rule = TestEditor.BareRule();
        var old = new Diagnostic(DiagnosticSeverity.Error, rule.Id, "bad");
        var reworded = new Diagnostic(DiagnosticSeverity.Error, rule.Id, "still bad");

        var set = new FixSet(new DiagnosticFix(old, "a", "A", _ => null));

        Assert.Empty(set.For(reworded));
    }

    [Fact]
    public void TwoFixesWithOneIdForOneFindingAreRejected()
    {
        var diagnostic = Finding("bad");

        var set = Assert.Throws<ArgumentException>(() => new FixSet(
            new DiagnosticFix(diagnostic, "rename", "Rename", _ => null),
            new DiagnosticFix(diagnostic, "rename", "Rename it", _ => null)));

        Assert.Contains("rename", set.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoFindingsMayShareOneFixId()
    {
        // The id is unique per finding, not globally: two findings on one node can each offer a "rename",
        // and they are two different repairs rather than a duplicate.
        var rule = TestEditor.BareRule();
        var first = new Diagnostic(DiagnosticSeverity.Error, rule.Id, "first");
        var second = new Diagnostic(DiagnosticSeverity.Warning, rule.Id, "second");

        var set = new FixSet(
            new DiagnosticFix(first, "rename", "Rename", _ => null),
            new DiagnosticFix(second, "rename", "Rename", _ => null));

        Assert.Equal(2, set.Count);
        Assert.Single(set.For(first));
        Assert.Single(set.For(second));
    }

    [Fact]
    public void ANullFixIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new FixSet([null!]));
    }

    [Fact]
    public void ForRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => FixSet.Empty.For(null!));
    }

    private static Diagnostic Finding(string message) =>
        new(DiagnosticSeverity.Error, TestEditor.BareRule().Id, message);
}
