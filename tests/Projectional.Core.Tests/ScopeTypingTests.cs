using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Typing a name into a reference cell: the second consumer of the scope chain.
/// </summary>
/// <remarks>
/// A reference cell holds a name, not text, so what is typed is a name to resolve rather than characters
/// to store. The run accumulates across keystrokes - a name is spelt over several - and nothing is
/// written until the name means exactly one thing.
/// </remarks>
public sealed class ScopeTypingTests
{
    [Fact]
    public void TypingANameThatResolvesLinksTheSlot()
    {
        var (session, rule, tag) = RuleWithTag("urgent");

        var result = Type(session, rule, "urgent");

        Assert.True(result.Changed);
        Assert.Equal(tag.Id, rule.GetReferenceTarget("tag")?.Id);
    }

    [Fact]
    public void ANameIsSpeltOverSeveralKeystrokes()
    {
        var (session, rule, tag) = RuleWithTag("urgent");

        // Select once: Select clears the pending run, which is what makes a caret move end a word.
        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(rule.Id, "tag"), 0)));

        Assert.False(session.Apply(new TypeTextAction("ur")).Changed);
        Assert.False(session.Apply(new TypeTextAction("ge")).Changed);
        Assert.True(session.Apply(new TypeTextAction("nt")).Changed);

        Assert.Equal(tag.Id, rule.GetReferenceTarget("tag")?.Id);
    }

    [Fact]
    public void ANameThatMeansNothingYetWritesNothing()
    {
        var (session, rule, _) = RuleWithTag("urgent");

        var result = Type(session, rule, "nobody");

        Assert.False(result.Changed);
        Assert.Null(rule.GetReferenceTarget("tag"));
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void ANameThatMeansTwoThingsIsRefusedWithAReason()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);

        foreach (var label in new[] { "urgent", "urgent" })
        {
            model.NewNode(TestLanguage.Tag).Set("label", label);
        }

        using var session = new EditorSession(rule, TestNotations.Registry);

        var result = Type(session, rule, "urgent");

        // The scope model says ambiguity is not a silent pick, and a gesture that chose would be making
        // the user's decision for them.
        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("2 things", result.Reason, StringComparison.Ordinal);
        Assert.Null(rule.GetReferenceTarget("tag"));
    }

    [Fact]
    public void LinkingByTypingIsOneUndoStep()
    {
        var (session, rule, _) = RuleWithTag("urgent");

        Type(session, rule, "urgent");

        Assert.True(session.Undo());
        Assert.Null(rule.GetReferenceTarget("tag"));
    }

    private static EditResult Type(EditorSession session, Node rule, string text)
    {
        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(rule.Id, "tag"), 0)));

        return session.Apply(new TypeTextAction(text));
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
