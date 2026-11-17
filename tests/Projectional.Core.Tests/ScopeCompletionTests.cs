using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Completion of a reference: the list is the scope, and accepting one links the slot.
/// </summary>
/// <remarks>
/// The design says the scope chain is one implementation with two consumers - the list and a typed name.
/// These tests hold the list half: what it offers, that it offers only what the slot accepts, and that
/// accepting re-resolves the name instead of trusting the list.
/// </remarks>
public sealed class ScopeCompletionTests
{
    [Fact]
    public void AReferenceOffersTheNamesItsScopeHolds()
    {
        var (session, rule, _) = RuleWithTag("urgent");

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(rule.Id, "tag"), 0)));
        session.Apply(new OpenCompletionAction());

        Assert.NotNull(session.Completion);
        Assert.Contains(session.Completion.Items, item => item.ConceptKey == "urgent" && item.Kind == CompletionKind.Reference);
    }

    [Fact]
    public void AcceptingAReferenceLinksTheSlotAndNamesTheTarget()
    {
        var (session, rule, tag) = RuleWithTag("urgent");

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(rule.Id, "tag"), 0)));
        session.Apply(new OpenCompletionAction());
        session.Apply(new AcceptCompletionAction());

        Assert.Equal(tag.Id, rule.GetReferenceTarget("tag")?.Id);

        // The name travels with the link as its fallback, so a reference whose target is removed still
        // says what it meant.
        Assert.Equal("urgent", rule.GetReference("tag")?.ResolveInfo);
    }

    [Fact]
    public void AcceptingAReferenceIsOneUndoStep()
    {
        var (session, rule, _) = RuleWithTag("urgent");

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(rule.Id, "tag"), 0)));
        session.Apply(new OpenCompletionAction());
        session.Apply(new AcceptCompletionAction());

        Assert.True(session.Undo());
        Assert.Null(rule.GetReferenceTarget("tag"));
    }

    [Fact]
    public void ANameThatIsNoLongerInTheScopeIsRefused()
    {
        var (session, rule, tag) = RuleWithTag("urgent");

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(rule.Id, "tag"), 0)));
        session.Apply(new OpenCompletionAction());

        // The list survives between keystrokes; the document does not. Renaming the target after the list
        // opened must not link to a name the scope no longer answers.
        tag.Set("label", "renamed");

        var result = session.Apply(new AcceptCompletionAction());

        Assert.False(result.Changed);
        Assert.Null(rule.GetReferenceTarget("tag"));
    }

    [Fact]
    public void AReferenceWithNothingInScopeOpensNoList()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);

        using var session = new EditorSession(rule, TestNotations.Registry);

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(rule.Id, "tag"), 0)));
        session.Apply(new OpenCompletionAction());

        Assert.Null(session.Completion);
    }

    private static (EditorSession Session, Node Rule, Node Tag) RuleWithTag(string label)
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var tag = model.NewNode(TestLanguage.Tag);

        tag.Set("label", label);

        return (new EditorSession(rule, TestNotations.Registry), rule, tag);
    }
}
