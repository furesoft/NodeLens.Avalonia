using Projectional.Core;
using Projectional.Editing;
using Projectional.Json;
using Projectional.Json.Checks;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The rules JSON states about itself, and the repair each finding carries.</summary>
public sealed class JsonChecksTests
{
    [Fact]
    public void AStringWithARawQuoteIsReportedAndEscaped()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        var text = model.NewNode(JsonConceptKeys.Text);
        text.Set(JsonLanguage.TextSlot, "say \"hi\"");

        var finding = Assert.Single(new JsonSpellingCheck().Find(model));

        Assert.Equal(JsonSpellingCheck.Code, finding.Diagnostic.Code);

        using var session = new EditorSession(text, JsonLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal("say \\\"hi\\\"", text.GetOrNull(JsonLanguage.TextSlot));
        Assert.Empty(new JsonSpellingCheck().Find(model));
    }

    [Fact]
    public void ALegalSpellingIsLeftAlone()
    {
        // "\u0061" is a legal spelling of "a": this language keeps spellings rather than normalising them, so a value
        // that could have come from a file gets no finding.
        var model = JsonLanguage.NewProject().NewModel("json");
        model.NewNode(JsonConceptKeys.Text).Set(JsonLanguage.TextSlot, "a\\u0061\\n");

        Assert.Empty(new JsonSpellingCheck().Find(model));
    }

    [Fact]
    public void ANumberCellHoldingTextIsResetToItsDefault()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        var number = model.NewNode(JsonConceptKeys.Number);
        number.Set(JsonLanguage.TextSlot, "12,5");

        var finding = Assert.Single(new JsonNumberCheck().Find(model));

        Assert.Equal("Replace with \"0\"", finding.Label);

        using var session = new EditorSession(number, JsonLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal("0", number.GetOrNull(JsonLanguage.TextSlot));
    }

    [Fact]
    public void ADuplicateMemberIsRenamedToTheFirstFreeSuffix()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        var container = model.NewNode(JsonConceptKeys.Object);
        Member(model, container, "a");
        var second = Member(model, container, "a");

        var finding = Assert.Single(new JsonDuplicateMemberCheck().Find(model));

        Assert.Equal(JsonDuplicateMemberCheck.Code, finding.Diagnostic.Code);

        using var session = new EditorSession(container, JsonLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal("a2", second.GetOrNull(JsonLanguage.NameSlot));
        Assert.Empty(new JsonDuplicateMemberCheck().Find(model));
    }

    [Fact]
    public void AWellFormedDocumentIsClean()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        var container = model.NewNode(JsonConceptKeys.Object);
        var member = Member(model, container, "a");
        model.NewChild(member, JsonLanguage.ValueSlot, JsonConceptKeys.Number)
            .Set(JsonLanguage.TextSlot, "1.5");

        Assert.Empty(JsonChecks.All.Find(model));
    }

    [Fact]
    public void TwoCommentsWithTheSameWordsAreNotADuplicateMember()
    {
        // A note has no name to collide with: reading its text as one would report two notes that say the same thing
        // as a duplicated key.
        var model = JsonLanguage.NewProject().NewModel("json");
        var container = model.NewNode(JsonConceptKeys.Object);

        foreach (var text in new[] { "same", "same" })
        {
            var note = model.NewChild(container, JsonLanguage.MembersSlot, JsonConceptKeys.Comment);
            note.Set(JsonLanguage.TextSlot, text);
        }

        Assert.Empty(new JsonDuplicateMemberCheck().Find(model));
    }

    private static Node Member(Model model, Node container, string name)
    {
        var member = model.NewChild(container, JsonLanguage.MembersSlot, JsonConceptKeys.Property);
        member.Set(JsonLanguage.NameSlot, name);
        return member;
    }
}
