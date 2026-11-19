using Projectional.Core;
using Projectional.Projection;
using Projectional.Hocon;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>How a HOCON document is drawn: the file's own lines, with the separator the file had.</summary>
/// <remarks>
/// The separator is the interesting case: four spellings of one property, drawn by four conditional cells - so the
/// drawing shows what the user typed rather than a normalised form.
/// </remarks>
public class HoconNotationTests
{
    [Theory]
    [InlineData(HoconLanguage.EqualsOperator, "a = 1")]
    [InlineData(HoconLanguage.ColonOperator, "a: 1")]
    [InlineData(HoconLanguage.AppendOperator, "a += 1")]
    [InlineData(HoconLanguage.SpaceOperator, "a 1")]
    public void APropertyDrawsTheSeparatorItHas(string @operator, string expected)
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var property = model.NewNode(HoconConceptKeys.Property);
        property.Set(HoconLanguage.KeySlot, "a");
        property.Set(HoconLanguage.OperatorSlot, @operator);
        model.NewChild(property, HoconLanguage.ValueSlot, HoconConceptKeys.Number)
            .Set(HoconLanguage.LiteralSlot, "1");

        Assert.Equal(expected, Render(property));
    }

    [Fact]
    public void AnObjectDrawsItsBracesWithItsMembersIndented()
    {
        // The drawing hangs a block that starts inside a line: the object's own indent is measured from where its cell
        // begins, which is the documented consequence of drawing a vertical notation inside an inline child. The file
        // that is generated is indented from the line start, because a template's indent is a property of the template.
        var model = HoconLanguage.NewProject().NewModel("application");
        var document = model.NewNode(HoconConceptKeys.Document);
        var property = model.NewChild(document, HoconLanguage.MembersSlot, HoconConceptKeys.Property);
        property.Set(HoconLanguage.KeySlot, "server");
        property.Set(HoconLanguage.OperatorSlot, HoconLanguage.SpaceOperator);

        var server = model.NewChild(property, HoconLanguage.ValueSlot, HoconConceptKeys.Object);
        var port = model.NewChild(server, HoconLanguage.MembersSlot, HoconConceptKeys.Property);
        port.Set(HoconLanguage.KeySlot, "port");
        port.Set(HoconLanguage.OperatorSlot, HoconLanguage.EqualsOperator);
        model.NewChild(port, HoconLanguage.ValueSlot, HoconConceptKeys.Number)
            .Set(HoconLanguage.LiteralSlot, "8080");

        Assert.Equal("server {\n         port = 8080\n       }", Render(document));
    }

    [Fact]
    public void AConcatenationDrawsItsPartsBesideEachOther()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var concat = model.NewNode(HoconConceptKeys.Concat);

        var greeting = model.NewChild(concat, HoconLanguage.PartsSlot, HoconConceptKeys.String);
        greeting.Set(HoconLanguage.TextSlot, "hello");
        greeting.Set(HoconLanguage.QuotedSlot, HoconLanguage.Unquoted);

        var name = model.NewChild(concat, HoconLanguage.PartsSlot, HoconConceptKeys.Substitution);
        name.Set(HoconLanguage.PathSlot, "user");

        Assert.Equal("hello ${user}", Render(concat));
    }

    [Fact]
    public void AnIncludeAndASubstitutionAreTheOneLineTheyAre()
    {
        var model = HoconLanguage.NewProject().NewModel("application");

        var include = model.NewNode(HoconConceptKeys.Include);
        include.Set(HoconLanguage.TargetSlot, "\"other.conf\"");

        var substitution = model.NewNode(HoconConceptKeys.Substitution);
        substitution.Set(HoconLanguage.PathSlot, "JAVA_HOME");
        substitution.Set(HoconLanguage.OptionalSlot, HoconLanguage.Optional);

        Assert.Equal("include \"other.conf\"", Render(include));
        Assert.Equal("${?JAVA_HOME}", Render(substitution));
    }

    [Fact]
    public void ACommentDrawsItsMarkerAndItsText()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var comment = model.NewNode(HoconConceptKeys.Comment);
        comment.Set(HoconLanguage.MarkerSlot, "//");
        comment.Set(HoconLanguage.TextSlot, " note");

        Assert.Equal("// note", Render(comment));
    }

    [Fact]
    public void AnEmptyDocumentKeepsItsSlotVisible()
    {
        var model = HoconLanguage.NewProject().NewModel("application");

        Assert.Equal("empty", Render(model.NewNode(HoconConceptKeys.Document)));
    }

    private static string Render(Node node) =>
        LayoutSnapshot.Render(node, HoconLanguage.Notations, width: 120).Trim();
}
