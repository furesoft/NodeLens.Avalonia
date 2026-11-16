using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Which repairs a document offers: the line the caret is on, worst finding first, nothing without a
/// repair to offer.
/// </summary>
/// <remarks>
/// The line is resolved geometrically rather than by counting texts, so the tests that matter are the two
/// a line number would get wrong: a finding on a wrapped continuation line belongs to the line the user is
/// looking at, and a finding on the line above does not.
/// </remarks>
public sealed class QuickFixesTests
{
    [Fact]
    public void TheWorstFindingOnTheLineLeadsAndTheRestFollowInSetOrder()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        var info = Value(rule, DiagnosticSeverity.Info, "info");
        var error = Value(rule, DiagnosticSeverity.Error, "error");
        var warning = Value(rule, DiagnosticSeverity.Warning, "warning");

        Publish(session, [info, error, warning], [info, error, warning]);

        // The error leads because severity decides first; the two that remain keep the order the marker
        // set reported them in, which is document order and not severity order.
        Assert.Equal(["Fix for error", "Fix for info", "Fix for warning"], LabelsOf(session));
    }

    [Fact]
    public void AFindingWithoutARepairIsNotOfferedAndDoesNotLead()
    {
        // A check that reports a problem it cannot cure must not drag the affordance onto the line and
        // then have nothing in it.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        var uncured = Value(rule, DiagnosticSeverity.Error, "uncured");
        var cured = Value(rule, DiagnosticSeverity.Warning, "cured");

        Publish(session, [cured], [uncured, cured]);

        Assert.Equal(["Fix for cured"], LabelsOf(session));
    }

    [Fact]
    public void ALineWhoseOnlyFindingHasNoRepairOffersNothing()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        var uncured = Value(rule, DiagnosticSeverity.Error, "uncured");

        session.Markers = new MarkerSet(uncured);

        Assert.Empty(QuickFixes.At(session));
    }

    [Fact]
    public void AMarkerOnAWrappedContinuationLineBelongsToThatLine()
    {
        // "abcdefgh ijkl" overflows a twelve-column page, so the value continues on a second line. A
        // finding on the last four characters is on the continuation line, and the caret there sees it -
        // which is the case a line number counted per cell would get wrong.
        var rule = RuleNamed("abcdefgh ijkl");
        using var session = TestEditor.Open(rule);
        session.WrapWidth = 12;
        var name = CellAddress.OfSlot(rule.Id, "name");

        var wrapped = new Diagnostic(
            DiagnosticSeverity.Error,
            rule.Id,
            "bad tail",
            role: "name",
            range: CharacterRange.Of(9, 12));

        Publish(session, [wrapped], [wrapped]);

        session.Select(Selection.At(CaretPosition.At(name, 10)));
        Assert.Equal(["Fix for bad tail"], LabelsOf(session));

        // The same finding is not offered two lines up, where the value starts.
        session.Select(Selection.At(CaretPosition.At(name, 2)));
        Assert.Empty(QuickFixes.At(session));
    }

    [Fact]
    public void AFindingOnTheLineAboveIsNotOffered()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);

        var inBody = new Diagnostic(DiagnosticSeverity.Error, step.Id, "step", role: "text");

        Publish(session, [inBody], [inBody]);

        // The session opens with the caret on the rule's own name, which is the line above the body.
        Assert.Empty(QuickFixes.At(session));
    }

    [Fact]
    public void ADisabledFixIsStillOffered()
    {
        // "This language knows about the problem and here is why it cannot act" is the useful half of
        // the message, so a disabled fix is listed rather than dropped.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var diagnostic = Value(rule, DiagnosticSeverity.Error, "bad");

        session.Markers = new MarkerSet(diagnostic);
        session.Fixes = new FixSet(new DiagnosticFix(
            diagnostic,
            "rename",
            "Rename",
            _ => null,
            availability: FixAvailability.Disabled("the model is read-only")));

        var fix = Assert.Single(QuickFixes.At(session));

        Assert.True(fix.IsDisabled);
        Assert.Equal("the model is read-only", fix.Availability.Reason);
    }

    [Fact]
    public void OfAnswersForAFindingInHandByValue()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var diagnostic = Value(rule, DiagnosticSeverity.Error, "bad");

        Publish(session, [diagnostic], [diagnostic]);

        // A host that kept its own copy of the finding gets the same repairs: the set is keyed by value.
        var recomputed = new Diagnostic(
            DiagnosticSeverity.Error,
            rule.Id,
            "bad",
            role: "name",
            range: CharacterRange.Of(0, 4));

        Assert.Equal(["Fix for bad"], QuickFixes.Of(session, recomputed).Select(fix => fix.Label));
    }

    [Fact]
    public void OfAnswersEmptyForAFindingNobodyRepaired()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var repaired = Value(rule, DiagnosticSeverity.Error, "repaired");
        var other = Value(rule, DiagnosticSeverity.Error, "other");

        Publish(session, [repaired], [repaired]);

        Assert.Empty(QuickFixes.Of(session, other));
    }

    [Fact]
    public void ADocumentWithNoFindingsOffersNothing()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        Assert.Empty(QuickFixes.At(session));
    }

    [Fact]
    public void NullsAreRejected()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var diagnostic = Value(rule, DiagnosticSeverity.Error, "bad");

        Assert.Throws<ArgumentNullException>(() => QuickFixes.At(null!));
        Assert.Throws<ArgumentNullException>(() => QuickFixes.Of(null!, diagnostic));
        Assert.Throws<ArgumentNullException>(() => QuickFixes.Of(session, null!));
    }

    private static IReadOnlyList<string> LabelsOf(EditorSession session) =>
        [.. QuickFixes.At(session).Select(fix => fix.Label)];

    /// <summary>Reports <paramref name="withFixes"/> to the session, one repair each.</summary>
    /// <param name="session">The document.</param>
    /// <param name="withFixes">The findings that carry a repair, in the order the repairs are offered.</param>
    /// <param name="all">Every finding the marker set reports, in the order it reports them.</param>
    private static void Publish(
        EditorSession session,
        IReadOnlyList<Diagnostic> withFixes,
        IReadOnlyList<Diagnostic> all)
    {
        session.Markers = new MarkerSet([.. all]);
        session.Fixes = new FixSet([.. withFixes.Select(diagnostic =>
            new DiagnosticFix(diagnostic, diagnostic.Message, $"Fix for {diagnostic.Message}", _ => null))]);
    }

    private static Diagnostic Value(
        Node rule,
        DiagnosticSeverity severity,
        string message) =>
        new(severity, rule.Id, message, role: "name", range: CharacterRange.Of(0, 4));

    private static Node RuleNamed(string name)
    {
        var rule = TestEditor.BareRule();
        rule.Set("name", name);

        return rule;
    }
}
