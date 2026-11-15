using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A diagnostic is about a range in a value; a page draws rectangles on lines. This is the conversion
/// between the two.
/// </summary>
/// <remarks>
/// The assertions are about geometry, so they use the monospace measure: one character is one unit wide
/// and one line is one unit tall, which lets an expectation be counted off the text instead of guessed at.
/// The interesting cases are the ones where a value and a drawn line disagree - a wrap, a hard break, a
/// range that no longer fits the text - because those are where a marker would otherwise land on the wrong
/// characters or vanish.
/// </remarks>
public sealed class MarkerGeometryTests
{
    [Fact]
    public void AMarkerCoversTheCharactersItNames()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        var marker = Assert.Single(MarkersOf(session, Value(rule, "name", 1, 3)));

        var bounds = BoundsOf(session, name);
        Assert.Equal(name, marker.Cell);
        Assert.Equal(0, marker.Line);
        Assert.Equal(bounds.X + 1, marker.X);
        Assert.Equal(bounds.Y, marker.Y);
        Assert.Equal(2, marker.Width);
        Assert.Equal(session.Measure.LineHeight, marker.Height);
    }

    [Fact]
    public void AMarkerFollowsAValueAcrossAWrap()
    {
        // The value is drawn on two lines, so a range that straddles the wrap is two rectangles on two
        // lines - one squiggle per line is the only thing a page can draw.
        var rule = RuleNamed("abcdefghij");
        using var session = TestEditor.Open(rule);
        session.WrapWidth = 12;
        var name = CellAddress.OfSlot(rule.Id, "name");

        var markers = MarkersOf(session, Value(rule, "name", 6, 9));

        var bounds = BoundsOf(session, name);
        Assert.Equal(2, markers.Count);

        // The first line is seven characters wide, so offset 6 is its last character.
        Assert.Equal((bounds.X + 6, bounds.Y, 1.0, 0), (markers[0].X, markers[0].Y, markers[0].Width, markers[0].Line));

        // A continuation line starts at the cell's own left edge, not at the page's.
        Assert.Equal((bounds.X, bounds.Y + 1, 2.0, 1), (markers[1].X, markers[1].Y, markers[1].Width, markers[1].Line));
    }

    [Fact]
    public void AMarkerAcrossAHardBreakCoversBothLines()
    {
        var rule = RuleNamed("ab\ncd");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        var markers = MarkersOf(session, Value(rule, "name", 0, 5));

        var bounds = BoundsOf(session, name);
        Assert.Equal(2, markers.Count);
        Assert.Equal((bounds.X, bounds.Y, 2.0, 0), (markers[0].X, markers[0].Y, markers[0].Width, markers[0].Line));
        Assert.Equal((bounds.X, bounds.Y + 1, 2.0, 1), (markers[1].X, markers[1].Y, markers[1].Width, markers[1].Line));
    }

    [Fact]
    public void TheLinesBetweenTheEndsOfARangeAreMarkedWhole()
    {
        // A range that starts on the first line and ends on the last marks the middle ones entirely, which
        // is what "the problem is this whole passage" looks like.
        var rule = RuleNamed("ab\ncd\nef");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        var markers = MarkersOf(session, Value(rule, "name", 1, 7));

        var bounds = BoundsOf(session, name);
        Assert.Equal(3, markers.Count);
        Assert.Equal((bounds.X + 1, 1.0), (markers[0].X, markers[0].Width));
        Assert.Equal((bounds.X, 2.0), (markers[1].X, markers[1].Width));
        Assert.Equal((bounds.X, 1.0), (markers[2].X, markers[2].Width));
        Assert.Equal([0, 1, 2], markers.Select(marker => marker.Line));
    }

    [Fact]
    public void ARangeThatOverhangsTheEndIsClipped()
    {
        // A check measured a longer value than the one on screen. Marking what is left keeps the finding
        // visible without pointing past the text.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        var marker = Assert.Single(MarkersOf(session, Value(rule, "name", 2, 50)));

        var bounds = BoundsOf(session, name);
        Assert.Equal(bounds.X + 2, marker.X);
        Assert.Equal(2, marker.Width);
    }

    [Fact]
    public void ARangeThatIsEntirelyPastTheEndMarksTheCell()
    {
        // Dropping the marker would make a stale diagnostic disappear silently, and the model may still be
        // wrong - the text simply got shorter.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        var marker = Assert.Single(MarkersOf(session, Value(rule, "name", 50, 60)));

        var bounds = BoundsOf(session, name);
        Assert.Equal(bounds.X, marker.X);
        Assert.Equal(4, marker.Width);
    }

    [Fact]
    public void AnEmptyRangeMarksOneCharacter()
    {
        // A range of no length points between two characters. One character of marker is what makes
        // "this position is wrong" visible; nothing at all would be invisible.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        var marker = Assert.Single(MarkersOf(session, Value(rule, "name", 1, 1)));

        var bounds = BoundsOf(session, name);
        Assert.Equal(bounds.X + 1, marker.X);
        Assert.Equal(1, marker.Width);
    }

    [Fact]
    public void AValueWithoutARangeIsMarkedOnEveryLineItOccupies()
    {
        var rule = RuleNamed("ab\ncd");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        var markers = MarkersOf(session, new Diagnostic(DiagnosticSeverity.Warning, rule.Id, "bad name", role: "name"));

        Assert.Equal(2, markers.Count);
        Assert.All(markers, marker => Assert.Equal(2, marker.Width));
        Assert.Equal([0, 1], markers.Select(marker => marker.Line));
    }

    [Fact]
    public void ACellWithNoTextStillGetsAVisibleMarker()
    {
        // A concept whose notation is only a child slot draws no characters of its own, so there is nothing
        // to underline - but a zero-width squiggle is not a marker.
        var notations = new NotationRegistry(TestLanguage.Registry);
        notations.Add(TestLanguage.Rule, ed => ed.Sequence(ed.Line(ed.ChildInline("body"))));

        var (rule, _) = TestEditor.RuleWithBody();
        using var session = new EditorSession(rule, notations);

        var marker = Assert.Single(MarkersOf(session, new Diagnostic(DiagnosticSeverity.Error, rule.Id, "broken")));

        Assert.Equal(1, marker.Width);
    }

    [Fact]
    public void TwoFindingsOnTheSameTextBothSurvive()
    {
        // Which one is drawn is a painting decision, and the other has to stay reachable - so geometry
        // reports both rather than quietly keeping the worst.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        var markers = MarkersOf(
            session,
            new Diagnostic(DiagnosticSeverity.Error, rule.Id, "first", role: "name", range: CharacterRange.Of(1, 3)),
            new Diagnostic(DiagnosticSeverity.Hint, rule.Id, "second", role: "name", range: CharacterRange.Of(1, 3)));

        Assert.Equal(2, markers.Count);
        Assert.Contains(markers, marker => marker.Diagnostic.Message == "first");
        Assert.Contains(markers, marker => marker.Diagnostic.Message == "second");
        Assert.Equal(markers[0].X, markers[1].X);
        Assert.Equal(markers[0].Width, markers[1].Width);
    }

    [Fact]
    public void MarkersComeBackInReadingOrder()
    {
        // A page walks the list to draw, and a keyboard user walks it to jump between problems, so it has to
        // be ordered the way the eye reads rather than the order the checks happened to run in.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        var markers = MarkersOf(
            session,
            new Diagnostic(DiagnosticSeverity.Error, rule.Id, "late", role: "body"),
            new Diagnostic(DiagnosticSeverity.Error, rule.Id, "early", role: "name"));

        Assert.Equal(2, markers.Count);
        Assert.Equal("early", markers[0].Diagnostic.Message);
        Assert.True(markers[0].Y < markers[1].Y);
    }

    [Fact]
    public void AFindingsAboutANodeThatIsNotShownMarksNothing()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        var markers = MarkersOf(session, new Diagnostic(DiagnosticSeverity.Error, TestEditor.BareStep().Id, "stale"));

        Assert.Empty(markers);
    }

    [Fact]
    public void NoMarkersIsAnEmptyAnswer()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        Assert.Empty(MarkerGeometry.Of(session.Layout, MarkerSet.Empty, session.Measure));
    }

    [Fact]
    public void ALayoutAMarkerSetAndAMeasureAreRequired()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var layout = session.Layout;

        Assert.Throws<ArgumentNullException>(() => MarkerGeometry.Of(null!, MarkerSet.Empty, session.Measure));
        Assert.Throws<ArgumentNullException>(() => MarkerGeometry.Of(layout, null!, session.Measure));
        Assert.Throws<ArgumentNullException>(() => MarkerGeometry.Of(layout, MarkerSet.Empty, null!));
    }

    private static IReadOnlyList<MarkerLine> MarkersOf(EditorSession session, params Diagnostic[] diagnostics) =>
        MarkerGeometry.Of(session.Layout, new MarkerSet(diagnostics), session.Measure);

    private static Diagnostic Value(Node rule, string role, int start, int end) =>
        new(DiagnosticSeverity.Error, rule.Id, "bad value", role: role, range: CharacterRange.Of(start, end));

    private static LayoutRect BoundsOf(EditorSession session, CellAddress address) =>
        session.Layout.BoundsOf(address)!.Value;

    private static Node RuleNamed(string name)
    {
        var rule = TestEditor.BareRule();
        rule.Set("name", name);

        return rule;
    }
}
