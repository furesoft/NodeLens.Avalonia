using Projectional.Core;
using Projectional.Editing;
using Projectional.Toml;
using Projectional.Toml.Checks;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The rules TOML states about itself, and the repair each finding carries.</summary>
public sealed class TomlChecksTests
{
    [Fact]
    public void ADuplicateKeyIsRenamedToTheFirstFreeSuffix()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var document = model.NewNode(TomlConceptKeys.Document);
        Pair(model, document, "a");
        var second = Pair(model, document, "a");

        var finding = Assert.Single(new TomlDuplicateKeyCheck().Find(model));

        Assert.Equal(TomlDuplicateKeyCheck.Code, finding.Diagnostic.Code);

        using var session = new EditorSession(document, TomlLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal("a2", second.GetOrNull(TomlLanguage.KeySlot));
        Assert.Empty(new TomlDuplicateKeyCheck().Find(model));
    }

    [Fact]
    public void AnArrayOfTablesRepeatingANameIsNotADuplicate()
    {
        // [[a]] twice is two elements of one array, which is exactly what the double bracket means.
        var model = TomlLanguage.NewProject().NewModel("settings");
        var document = model.NewNode(TomlConceptKeys.Document);

        foreach (var _ in new[] { 1, 2 })
        {
            model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.ArrayOfTables)
                .Set(TomlLanguage.KeySlot, "a");
        }

        Assert.Empty(new TomlDuplicateKeyCheck().Find(model));
    }

    [Fact]
    public void ANumberCellHoldingTextIsResetToItsDefault()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var pair = Pair(model, model.NewNode(TomlConceptKeys.Document), "a");
        var number = model.NewChild(pair, TomlLanguage.ValueSlot, TomlConceptKeys.Integer);
        number.Set(TomlLanguage.TextSlot, "12,5");

        var finding = Assert.Single(new TomlNumberCheck().Find(model));

        Assert.Equal("Replace with \"0\"", finding.Label);

        using var session = new EditorSession(number, TomlLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal("0", number.GetOrNull(TomlLanguage.TextSlot));
    }

    [Fact]
    public void ALegalNumberLiteralIsLeftAlone()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var pair = Pair(model, model.NewNode(TomlConceptKeys.Document), "a");
        model.NewChild(pair, TomlLanguage.ValueSlot, TomlConceptKeys.Float)
            .Set(TomlLanguage.TextSlot, "1e3");

        Assert.Empty(new TomlNumberCheck().Find(model));
    }

    [Fact]
    public void ADateThatNoLongerParsesIsResetWithItsKind()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var pair = Pair(model, model.NewNode(TomlConceptKeys.Document), "a");
        var date = model.NewChild(pair, TomlLanguage.ValueSlot, TomlConceptKeys.DateTime);
        date.Set(TomlLanguage.TextSlot, "yesterday");
        date.Set(TomlLanguage.KindSlot, TomlLanguage.LocalDate);

        var finding = Assert.Single(new TomlDateTimeCheck().Find(model));

        using var session = new EditorSession(date, TomlLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal("1979-05-27T07:32:00Z", date.GetOrNull(TomlLanguage.TextSlot));
        Assert.Equal(TomlLanguage.OffsetDateTime, date.GetOrNull(TomlLanguage.KindSlot));
        Assert.Empty(new TomlDateTimeCheck().Find(model));
    }

    [Fact]
    public void AStringWithARawQuoteIsReportedAndEscaped()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var text = model.NewNode(TomlConceptKeys.String);
        text.Set(TomlLanguage.TextSlot, "say \"hi\"");
        text.Set(TomlLanguage.KindSlot, TomlLanguage.BasicString);

        var finding = Assert.Single(new TomlStringCheck().Find(model));

        Assert.Equal(TomlStringCheck.Code, finding.Diagnostic.Code);

        using var session = new EditorSession(text, TomlLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal("say \\\"hi\\\"", text.GetOrNull(TomlLanguage.TextSlot));
        Assert.Empty(new TomlStringCheck().Find(model));
    }

    [Fact]
    public void ALiteralStringMayHoldABackslashItDoesNotEscape()
    {
        // The four kinds are different syntaxes, so what is illegal in a basic string is legal in a literal one.
        var model = TomlLanguage.NewProject().NewModel("settings");
        var text = model.NewNode(TomlConceptKeys.String);
        text.Set(TomlLanguage.TextSlot, "C:\\temp");
        text.Set(TomlLanguage.KindSlot, TomlLanguage.LiteralString);

        Assert.Empty(new TomlStringCheck().Find(model));
    }

    [Fact]
    public void AWellFormedDocumentIsClean()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var document = model.NewNode(TomlConceptKeys.Document);
        var pair = Pair(model, document, "a");
        var text = model.NewChild(pair, TomlLanguage.ValueSlot, TomlConceptKeys.String);
        text.Set(TomlLanguage.TextSlot, "demo");

        Assert.Empty(TomlChecks.All.Find(model));
    }

    private static Node Pair(Model model, Node container, string key)
    {
        var pair = model.NewChild(container, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
        pair.Set(TomlLanguage.KeySlot, key);
        return pair;
    }
}
