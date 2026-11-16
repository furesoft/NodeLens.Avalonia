using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The sandbox a preview applies its edit to: a copy in its own project, projected by the same session rules,
/// thrown away afterwards.
/// </summary>
/// <remarks>
/// The isolation tests are the point of the slice - after a preview the live model, its version and its journal
/// must be exactly as they were - but the drawing test matters as much: a ghost that projects differently from
/// the page it replaces would be a preview of a document the user is not looking at.
/// </remarks>
public sealed class PreviewSandboxTests
{
    [Fact]
    public void TheSandboxDrawsWhatTheLiveSessionDraws()
    {
        using var session = Live(out _);
        using var sandbox = PreviewSandbox.Open(session);

        Assert.NotNull(sandbox);
        Assert.Equal(
            LayoutSnapshot.Render(session.Layout, width: 60),
            LayoutSnapshot.Render(sandbox.Layout, width: 60));
    }

    [Fact]
    public void TheSandboxStartsWhereTheLiveCaretIs()
    {
        using var session = Live(out var rule);

        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));

        using var sandbox = PreviewSandbox.Open(session);

        Assert.Equal(session.Selection, sandbox!.Selection);
    }

    [Fact]
    public void ApplyingAnEditChangesOnlyTheCopy()
    {
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Renamed"));

        Assert.NotNull(differences);
        Assert.Equal("Renamed", sandbox.Document.GetChildren(RulesLanguage.RulesSlot)[0].GetOrNull("name"));
        Assert.Equal("Approved", rule.GetOrNull("name"));
    }

    [Fact]
    public void TheLiveSessionKeepsItsHistoryItsVersionAndItsDrawing()
    {
        using var session = Live(out var rule);
        var before = LayoutSnapshot.Render(session.Layout, width: 60);
        var version = session.Document.Model.Version;
        var revision = session.History.Revision;

        using var sandbox = PreviewSandbox.Open(session);

        sandbox!.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Renamed"));

        Assert.Equal(0, session.History.UndoDepth);
        Assert.Equal(revision, session.History.Revision);
        Assert.Equal(version, session.Document.Model.Version);
        Assert.False(session.Undo());
        Assert.Equal(before, LayoutSnapshot.Render(session.Layout, width: 60));
    }

    [Fact]
    public void TheDifferencesSayWhatTheEditWouldDo()
    {
        using var session = Live(out var rule);
        var condition = rule.GetChild("condition")!;

        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(
            new WrapInAction(condition.Id, ExpressionsConceptKeys.Parenthesized, "inner"));

        Assert.NotNull(differences);
        Assert.Equal(2, differences.Count);

        // The wrapper is new, and the condition it went around moved - which is what a ghost has to mark.
        var added = Assert.Single(differences, each => each.Kind == NodeDifferenceKind.Added);
        var moved = Assert.Single(differences, each => each.Kind == NodeDifferenceKind.Moved);

        Assert.Equal(condition.Id, moved.Node);
        Assert.NotEqual(condition.Id, added.Node);
    }

    [Fact]
    public void AnEditThatChangesNothingHasNoDifferences()
    {
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Approved"));

        Assert.NotNull(differences);
        Assert.Empty(differences);
    }

    [Fact]
    public void AnActionThatThrowsYieldsNoPreview()
    {
        using var session = Live(out _);
        using var sandbox = PreviewSandbox.Open(session);

        Assert.Null(sandbox!.Apply(new DelegateAction(_ => throw new InvalidOperationException("boom"))));
        Assert.Empty(sandbox.Differences);
    }

    [Fact]
    public void AnActionThatReachesForALiveNodeYieldsNoPreview()
    {
        // The documented failure: an action that captured nodes instead of addresses. On a copy those nodes
        // belong to another model, and that is a "cannot be previewed", not an editor that breaks.
        using var session = Live(out var rule);

        using var sandbox = PreviewSandbox.Open(session);

        Assert.Null(sandbox!.Apply(new DelegateAction(context =>
        {
            context.WrapChild(rule, ExpressionsConceptKeys.Parenthesized, "inner");

            return EditResult.Unchanged(context.Selection);
        })));
    }

    [Fact]
    public void ADocumentOverTheBudgetHasNoPreview()
    {
        using var session = Live(out _);

        Assert.Null(PreviewSandbox.Open(session, nodeBudget: 2));
        Assert.NotNull(PreviewSandbox.Open(session, nodeBudget: 100));
    }

    [Fact]
    public void ADisposedSandboxRefusesToApplyAndLeavesTheLiveSessionUsable()
    {
        var session = Live(out var rule);
        var sandbox = PreviewSandbox.Open(session);

        sandbox!.Dispose();
        sandbox.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => sandbox.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Renamed")));

        using (session)
        {
            Assert.Equal("Approved", rule.GetOrNull("name"));
        }
    }

    [Fact]
    public void ASandboxOverAnEmptyDocumentStillWorks()
    {
        var model = PayrollSample.NewEmptyModel();
        using var session = new EditorSession(model.Roots[0], RulesLanguage.CreateNotations(model.Concepts));
        using var sandbox = PreviewSandbox.Open(session);

        Assert.NotNull(sandbox);
        Assert.Empty(sandbox!.Differences);
    }

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
