using Projectional.Core;
using Projectional.Editing;
using Projectional.Hocon;
using Projectional.Hocon.Checks;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The rules HOCON states about itself, and the repair each finding carries.</summary>
public sealed class HoconChecksTests
{
    [Fact]
    public void AnUnquotedValueHoldingASpaceIsReportedAndQuoted()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var text = model.NewNode(HoconConceptKeys.String);
        text.Set(HoconLanguage.TextSlot, "two words");

        var finding = Assert.Single(new HoconUnquotedCheck().Find(model));

        Assert.Equal(HoconUnquotedCheck.Code, finding.Diagnostic.Code);

        using var session = new EditorSession(text, HoconLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal(HoconLanguage.Quoted, text.GetOrNull(HoconLanguage.QuotedSlot));
        Assert.Empty(new HoconUnquotedCheck().Find(model));
    }

    [Fact]
    public void ALegalRunIsLeftAlone()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        model.NewNode(HoconConceptKeys.String).Set(HoconLanguage.TextSlot, "a.b-c_d");

        Assert.Empty(new HoconUnquotedCheck().Find(model));
    }

    [Fact]
    public void AQuotedValueIsNotAskedToBeARun()
    {
        // A quoted string may hold anything at all, which is exactly why it is the repair.
        var model = HoconLanguage.NewProject().NewModel("application");
        var text = model.NewNode(HoconConceptKeys.String);
        text.Set(HoconLanguage.QuotedSlot, HoconLanguage.Quoted);
        text.Set(HoconLanguage.TextSlot, "two words, { and }");

        Assert.Empty(new HoconUnquotedCheck().Find(model));
    }

    [Fact]
    public void ANumberCellHoldingTextIsResetToItsDefault()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var number = model.NewNode(HoconConceptKeys.Number);
        number.Set(HoconLanguage.LiteralSlot, "12,5");

        var finding = Assert.Single(new HoconNumberCheck().Find(model));

        Assert.Equal("Replace with \"0\"", finding.Label);

        using var session = new EditorSession(number, HoconLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal("0", number.GetOrNull(HoconLanguage.LiteralSlot));
    }

    [Fact]
    public void AWellFormedDocumentIsClean()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var document = model.NewNode(HoconConceptKeys.Document);
        var property = model.NewChild(document, HoconLanguage.MembersSlot, HoconConceptKeys.Property);
        property.Set(HoconLanguage.KeySlot, "a");
        model.NewChild(property, HoconLanguage.ValueSlot, HoconConceptKeys.Number)
            .Set(HoconLanguage.LiteralSlot, "1.5");

        Assert.Empty(HoconChecks.All.Find(model));
    }
}
