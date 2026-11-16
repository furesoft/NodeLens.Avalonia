using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The frame a ghost paints: the copy's text, which cells the edit touched, and the badge that says so.
/// </summary>
/// <remarks>
/// The ghost is drawn instead of the page rather than over it, so what these tests prove is that it draws the
/// <em>copy</em> - the new text in the new places - and that "unchanged" is expressed as dimming rather than as a
/// mark of its own.
/// </remarks>
public sealed class GhostFrameTests
{
    [Fact]
    public void TheGhostDrawsTheCopysTextAndNotThePages()
    {
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Renamed"));

        var ghost = Ghost(sandbox, differences!, "rename");

        Assert.Contains(ghost.TextRuns, run => run.Text == "Renamed");
        Assert.DoesNotContain(ghost.TextRuns, run => run.Text == "Approved");

        // The page is untouched, which is the whole point of previewing a copy.
        Assert.Equal("Approved", rule.GetOrNull("name"));
    }

    [Fact]
    public void ACellTheEditTouchedIsNotDimmedAndEveryOtherCellIs()
    {
        // The rule the marks encode: what the edit did reads first, so everything else is dimmed by one step.
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Renamed"));

        var ghost = Ghost(sandbox, differences!, "rename");
        var changed = Assert.Single(ghost.TextRuns, run => run.Text == "Renamed");

        Assert.False(ghost.IsDimmed(changed));
        Assert.Equal(CellChangeKind.Changed, ghost.ChangeOf(changed)!.Value.Kind);
        Assert.Equal("Approved", ghost.ChangeOf(changed)!.Value.OldValue);

        Assert.All(
            ghost.TextRuns.Where(run => !ReferenceEquals(run.Cell, changed.Cell)),
            run => Assert.True(ghost.IsDimmed(run), $"'{run.Text}' should be dimmed"));
    }

    [Fact]
    public void AWrappedNodeMarksTheWrapperAndNotTheNodesAroundIt()
    {
        using var session = Live(out var rule);
        var condition = rule.GetChild("condition")!;

        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(
            new WrapInAction(condition.Id, ExpressionsConceptKeys.Parenthesized, "inner"));

        var ghost = Ghost(sandbox, differences!, "wrap");

        // The parenthesis the wrapper added is new text, and the rule's own name is not.
        Assert.Contains(
            ghost.TextRuns,
            run => run.Text == "(" && ghost.ChangeOf(run)?.Kind == CellChangeKind.Added);

        Assert.Contains(
            ghost.TextRuns,
            run => run.Text == "Approved" && ghost.IsDimmed(run));
    }

    [Fact]
    public void TheBadgeSaysWhatTheEditDid()
    {
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Renamed"));

        Assert.Equal("Preview - rename, 1 change", Ghost(sandbox, differences!, "rename").Badge);
    }

    [Fact]
    public void TheGhostKeepsThePagesOffsetSoThePreviewStaysAnchored()
    {
        // The ghost replaces the page under the user's eyes: it must not jump to the top of the document when it
        // appears, or the region the preview is about scrolls away.
        using var session = Live(out _);
        using var sandbox = PreviewSandbox.Open(session);

        var copy = sandbox!;
        var ghost = GhostFrame.Build(
            copy,
            PreviewMarks.Of([], copy.Tree),
            "nothing",
            new LayoutSize(40, 3),
            offsetX: 0,
            offsetY: 2);

        // The same offset the page would clamp to, not an absolute number: a short document has nothing to scroll,
        // and the rule under test is "the ghost does not choose its own view", not "two means two".
        var (_, expected) = LayoutViewport.Clamp(copy.Layout, new LayoutSize(40, 3), 0, 2);

        Assert.Equal(expected, ghost.Viewport.OffsetY);
    }

    [Fact]
    public void AnEditThatChangesNothingDimsEverything()
    {
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Approved"));
        var ghost = Ghost(sandbox, differences!, "rename");

        Assert.NotEmpty(ghost.TextRuns);
        Assert.All(ghost.TextRuns, run => Assert.True(ghost.IsDimmed(run)));
        Assert.Equal("Preview - rename, no changes", ghost.Badge);
    }

    private static GhostFrame Ghost(PreviewSandbox sandbox, IReadOnlyList<NodeDifference> differences, string label) =>
        GhostFrame.Build(
            sandbox,
            PreviewMarks.Of(differences, sandbox.Tree),
            label,
            new LayoutSize(60, 20),
            offsetX: 0,
            offsetY: 0);

    /// <summary>The payroll sample opened the way the app opens it, with its notation and its rules.</summary>
    private static EditorSession Live(out Node rule)
    {
        var model = PayrollSample.NewSampleModel();

        rule = model.Roots[0].GetChildren(RulesLanguage.RulesSlot)[0];

        return new EditorSession(
            model.Roots[0],
            RulesLanguage.CreateNotations(model.Concepts),
            emphasis: RulesLanguage.Emphasis,
            typing: RulesLanguage.TypingRules);
    }
}
