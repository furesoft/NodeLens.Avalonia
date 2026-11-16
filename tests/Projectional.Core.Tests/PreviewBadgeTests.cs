using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The sentence a ghost carries in its corner, which is what keeps a preview from looking like an edited document.
/// </summary>
/// <remarks>
/// The counts are built from real diffs rather than from a fake, because the badge's whole job is to describe the
/// edit that was actually previewed - and the grammar (one kind reads as a total, several are broken down) is
/// decided by how many kinds are present, not by how many changes.
/// </remarks>
public sealed class PreviewBadgeTests
{
    [Fact]
    public void AnEditThatChangesNothingSaysSo()
    {
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Approved"));
        var badge = PreviewBadge.Of("rename", PreviewMarks.Of(differences!, sandbox.Tree));

        Assert.Equal("Preview - rename, no changes", badge);
    }

    [Fact]
    public void OneKindOfChangeReadsAsATotal()
    {
        // Four additions are "4 changes", not "4 added": with one kind there is nothing to break down, and a
        // reader wants the size of the edit rather than its taxonomy.
        using var session = Live(out _);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = new[]
        {
            Added(NodeId.New(), "condition"),
            Added(NodeId.New(), "condition"),
            Added(NodeId.New(), "condition"),
            Added(NodeId.New(), "condition"),
        };

        var badge = PreviewBadge.Of("convert to while", PreviewMarks.Of(differences, sandbox!.Tree));

        Assert.Equal("Preview - convert to while, 4 changes", badge);
    }

    [Fact]
    public void SeveralKindsAreBrokenDown()
    {
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = new[]
        {
            Added(NodeId.New(), "condition"),
            Added(NodeId.New(), "condition"),
            Removed(NodeId.New(), rule.Id, "condition"),
            Removed(NodeId.New(), rule.Id, "condition"),
            Removed(NodeId.New(), rule.Id, "condition"),
        };

        var badge = PreviewBadge.Of("convert to while", PreviewMarks.Of(differences, sandbox!.Tree));

        Assert.Equal("Preview - convert to while, 2 added, 3 removed", badge);
    }

    [Fact]
    public void OneChangeIsSingular()
    {
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = sandbox!.Apply(new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "Renamed"));
        var badge = PreviewBadge.Of("rename", PreviewMarks.Of(differences!, sandbox.Tree));

        Assert.Equal("Preview - rename, 1 change", badge);

        var added = PreviewBadge.Of("negate", PreviewMarks.Of([Added(NodeId.New(), "condition")], sandbox.Tree));

        Assert.Equal("Preview - negate, 1 change", added);
    }

    [Fact]
    public void TruncatedMarksAreNamedInTheBadge()
    {
        // The counts stay about the edit; what was left out is the marks, and saying so is the difference between
        // a ghost that is sparing and a ghost that is lying about how much it showed.
        using var session = Live(out var rule);
        using var sandbox = PreviewSandbox.Open(session);

        var differences = new[]
        {
            Added(NodeId.New(), "condition"),
            Removed(NodeId.New(), rule.Id, "condition"),
        };

        var marks = PreviewMarks.Of(differences, sandbox!.Tree, budget: 0);
        var badge = PreviewBadge.Of("convert", marks);

        Assert.StartsWith("Preview - convert, 1 added, 1 removed (", badge, StringComparison.Ordinal);
        Assert.Contains("not drawn)", badge, StringComparison.Ordinal);
    }

    [Fact]
    public void ABlankLabelIsRefused()
    {
        using var session = Live(out _);
        using var sandbox = PreviewSandbox.Open(session);
        var marks = PreviewMarks.Of([], sandbox!.Tree);

        Assert.Throws<ArgumentException>(() => PreviewBadge.Of("", marks));
        Assert.Throws<ArgumentException>(() => PreviewBadge.Of(" ", marks));
    }

    [Fact]
    public void ANullMarkListIsRefused() => Assert.Throws<ArgumentNullException>(() => PreviewBadge.Of("rename", null!));

    private static NodeDifference Added(NodeId node, string slot) =>
        new(node, NodeDifferenceKind.Added, slot, After: new NodePlace(null, null, 0));

    private static NodeDifference Removed(NodeId node, NodeId parent, string slot) =>
        new(node, NodeDifferenceKind.Removed, slot, Before: new NodePlace(parent, slot, 0), OldValue: "amount > 1000");

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
