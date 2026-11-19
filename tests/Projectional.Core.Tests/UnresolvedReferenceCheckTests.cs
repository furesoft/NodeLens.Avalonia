using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// <c>STD020</c>/<c>STD021</c>: a reference whose target is gone, repaired by pointing it at a name the scope offers
/// - or, when nothing is close, by clearing it.
/// </summary>
/// <remarks>
/// The fixture's <c>Test.Rule.tag</c> slot is a reference to <c>Test.Tag</c>, and the tags are roots beside the rule,
/// so the default scope offers them by their <c>label</c>. A dangling reference is written with
/// <c>SetReference(slot, NodeId.New(), text)</c> - the same shape a loaded document has when its target is missing.
/// </remarks>
public sealed class UnresolvedReferenceCheckTests
{
    private static UnresolvedReferenceCheck Check => new();

    [Fact]
    public void AReferenceThatResolvesIsSilent()
    {
        var (model, rule, tags) = Document(text: null, "urgent");
        rule.SetReference("tag", tags[0]);

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void AReferenceNobodySetIsSilent()
    {
        var (model, _, _) = Document(text: null, "urgent");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void ADanglingReferenceWithACloseNameOffersIt()
    {
        var (model, rule, _) = Document("urgent", "urgent");

        var fix = Assert.Single(Check.Find(model));

        Assert.Equal(UnresolvedReferenceCheck.Code, fix.Diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Warning, fix.Diagnostic.Severity);
        Assert.Equal("tag", fix.Diagnostic.Role);
        Assert.Equal(rule.Id, fix.Diagnostic.Target);
        Assert.Equal("Point to \"urgent\"", fix.Label);
        Assert.Equal(EditSafety.Plain, fix.Safety);
        Assert.Contains("urgent", fix.Diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingCloseLeavesTheClearingRepairOnly()
    {
        var (model, _, _) = Document("zzzz", "urgent");

        var fix = Assert.Single(Check.Find(model));

        Assert.Equal(UnresolvedReferenceCheck.ClearCode, fix.Diagnostic.Code);
        Assert.Equal("Clear the reference", fix.Label);
        Assert.True(fix.IsDestructive);
        Assert.Contains("nothing visible here is close", fix.Diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AReferenceThatKeptNoTextHasNothingToBeCloseTo()
    {
        // A reference can be set by id alone, and a finding whose only offer is "delete it" is the honest answer:
        // this check may not guess a target from an empty string.
        var (model, rule, _) = Document(text: null, "urgent");
        rule.SetReference("tag", NodeId.New());

        var fix = Assert.Single(Check.Find(model));

        Assert.Equal(UnresolvedReferenceCheck.ClearCode, fix.Diagnostic.Code);
        Assert.Contains("does not resolve.", fix.Diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOffersAreOrderedByClosenessAndTheScopesOwnOrderBreaksTies()
    {
        var (model, _, _) = Document("urgent", "urgent2", "urgent", "urgen");

        Assert.Equal(
            ["Point to \"urgent\"", "Point to \"urgent2\"", "Point to \"urgen\""],
            Check.Find(model).Select(fix => fix.Label));
    }

    [Fact]
    public void EveryOfferCarriesItsOwnIdAndTheyAllDescribeOneFinding()
    {
        // FixSet keys on the finding *and* the id, so three candidates are one finding with three repairs - the
        // shape STD020 was written for, and the reason the id names the target.
        var (model, _, _) = Document("urgent", "urgent", "urgent2");

        var fixes = Check.Find(model);

        Assert.Equal(2, fixes.Count);
        Assert.Same(fixes[0].Diagnostic, fixes[1].Diagnostic);
        Assert.NotEqual(fixes[0].Id, fixes[1].Id);
    }

    [Fact]
    public void ANameThatIsNotOfTheSlotsConceptIsNotOffered()
    {
        // A rule called `urgent` is visible in the scope, but the `tag` slot holds tags - offering it would be a
        // repair the model refuses.
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        model.AddRoot(rule);

        var other = model.NewNode(TestLanguage.Rule);
        other.Set("name", "urgent");
        model.AddRoot(other);

        rule.SetReference("tag", NodeId.New(), "urgent");

        Assert.Equal(UnresolvedReferenceCheck.ClearCode, Assert.Single(Check.Find(model)).Diagnostic.Code);
    }

    [Fact]
    public void ATargetInAnotherDocumentMakesTheReferenceResolved()
    {
        // The model's rule is "across a model boundary, not across a project" - and this check must not be a second
        // opinion about it.
        var (model, rule, _) = Document("urgent");
        var other = model.Project.NewModel("other");
        var tag = other.NewNode(TestLanguage.Tag);
        tag.Set("label", "urgent");
        other.AddRoot(tag);

        rule.SetReference("tag", tag.Id, "urgent");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void TheRepairPointsTheReferenceAndUndoTakesItBack()
    {
        var (model, rule, tags) = Document("urgent", "urgent");
        var fix = Assert.Single(Check.Find(model));

        using var session = TestEditor.Open(rule);
        session.Markers = new MarkerSet(fix.Diagnostic);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Equal(tags[0].Id, rule.GetReferenceTarget("tag")?.Id);
        Assert.Empty(Check.Find(model));

        Assert.True(session.Undo());
        Assert.NotNull(rule.GetReference("tag"));
        Assert.Single(Check.Find(model));
    }

    [Fact]
    public void TheClearingRepairEmptiesTheSlotAndUndoTakesItBack()
    {
        var (model, rule, _) = Document("zzzz", "urgent");
        var fix = Assert.Single(Check.Find(model));

        using var session = TestEditor.Open(rule);
        session.Markers = new MarkerSet(fix.Diagnostic);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Null(rule.GetReference("tag"));
        Assert.Empty(Check.Find(model));

        Assert.True(session.Undo());
        Assert.Equal("zzzz", rule.GetReference("tag")?.ResolveInfo);
    }

    [Fact]
    public void AModelThatIsNullIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => Check.Find(null!));

    /// <summary>A model holding one rule, one tag per label, and optionally a dangling <c>tag</c> reference.</summary>
    private static (Model Model, Node Rule, IReadOnlyList<Node> Tags) Document(string? text, params string[] labels)
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        model.AddRoot(rule);

        List<Node> tags = [];

        foreach (var label in labels)
        {
            var tag = model.NewNode(TestLanguage.Tag);
            tag.Set("label", label);
            model.AddRoot(tag);
            tags.Add(tag);
        }

        if (text is not null)
        {
            rule.SetReference("tag", NodeId.New(), text);
        }

        return (model, rule, tags);
    }
}