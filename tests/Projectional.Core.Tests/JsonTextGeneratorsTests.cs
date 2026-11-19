using Projectional.Core;
using Projectional.Json;
using Projectional.Language;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>JSON out: the file, spelled by the language that owns the format.</summary>
/// <remarks>
/// The quoting and the gaps are pinned here rather than in the importer's tests, because this is the half that decides
/// what a saved file looks like - and the corpus compares against exactly this spelling.
/// </remarks>
public sealed class JsonTextGeneratorsTests
{
    [Fact]
    public void AMemberIsItsNameAndItsValue()
    {
        var model = NewModel();
        var document = model.NewNode(JsonConceptKeys.Object);
        var member = model.NewChild(document, JsonLanguage.MembersSlot, JsonConceptKeys.Property);
        member.Set(JsonLanguage.NameSlot, "a");
        var number = model.NewChild(member, JsonLanguage.ValueSlot, JsonConceptKeys.Number);
        number.Set(JsonLanguage.TextSlot, "1");

        Assert.Equal("{ \"a\": 1 }", Generate(document));
    }

    [Fact]
    public void AnEmptyObjectIsTwoBraces()
    {
        // Not "{ }": the gaps exist to separate a member from its brackets, and an empty object has none. The drawing
        // keeps one space, because a slot must stay a caret stop - a drawing is not the file.
        var model = NewModel();

        Assert.Equal("{}", Generate(model.NewNode(JsonConceptKeys.Object)));
    }

    [Fact]
    public void AnEmptyArrayIsTwoBrackets()
    {
        var model = NewModel();

        Assert.Equal("[]", Generate(model.NewNode(JsonConceptKeys.Array)));
    }

    [Fact]
    public void MembersAndItemsAreJoinedByACommaAndASpace()
    {
        var model = NewModel();
        var document = model.NewNode(JsonConceptKeys.Object);
        var first = model.NewChild(document, JsonLanguage.MembersSlot, JsonConceptKeys.Property);
        first.Set(JsonLanguage.NameSlot, "a");
        model.NewChild(first, JsonLanguage.ValueSlot, JsonConceptKeys.Null);

        var second = model.NewChild(document, JsonLanguage.MembersSlot, JsonConceptKeys.Property);
        second.Set(JsonLanguage.NameSlot, "b");
        var boolean = model.NewChild(second, JsonLanguage.ValueSlot, JsonConceptKeys.Bool);
        boolean.Set(JsonLanguage.ValueSlot, "false");

        Assert.Equal("{ \"a\": null, \"b\": false }", Generate(document));
    }

    [Fact]
    public void ValuesNestTheWayTheModelDoes()
    {
        var model = NewModel();
        var document = model.NewNode(JsonConceptKeys.Object);
        var member = model.NewChild(document, JsonLanguage.MembersSlot, JsonConceptKeys.Property);
        member.Set(JsonLanguage.NameSlot, "tags");
        var array = model.NewChild(member, JsonLanguage.ValueSlot, JsonConceptKeys.Array);

        // A string is emitted between its quotes, and its text is the spelling of the file - so there is nothing to
        // escape at this level, which is exactly the point of holding the file's spelling rather than the value.
        var item = model.NewChild(array, JsonLanguage.ItemsSlot, JsonConceptKeys.Text);
        item.Set(JsonLanguage.TextSlot, "a\\\"b");

        Assert.Equal("{ \"tags\": [ \"a\\\"b\" ] }", Generate(document));
    }

    [Fact]
    public void ACommentIsLeftOutWithoutDisturbingItsNeighbour()
    {
        // JSON has no comments, so the note the model holds is not in the file. Because the list walk drops an entry
        // that emits nothing, the member beside it keeps its own separator instead of gaining a second one.
        var model = NewModel();
        var document = model.NewNode(JsonConceptKeys.Object);

        var first = model.NewChild(document, JsonLanguage.MembersSlot, JsonConceptKeys.Property);
        first.Set(JsonLanguage.NameSlot, "a");
        model.NewChild(first, JsonLanguage.ValueSlot, JsonConceptKeys.Null);

        var note = model.NewChild(document, JsonLanguage.MembersSlot, JsonConceptKeys.Comment);
        note.Set(JsonLanguage.TextSlot, "why");

        var second = model.NewChild(document, JsonLanguage.MembersSlot, JsonConceptKeys.Property);
        second.Set(JsonLanguage.NameSlot, "b");
        model.NewChild(second, JsonLanguage.ValueSlot, JsonConceptKeys.Bool)
            .Set(JsonLanguage.ValueSlot, "true");

        Assert.Equal("{ \"a\": null, \"b\": true }", Generate(document));
    }

    [Fact]
    public void ACommentInAnObjectThatHoldsNothingStillGeneratesAJsonObject()
    {
        // The gaps inside the braces are chosen by whether the slot holds a *child*, and a comment is one - so the
        // document prints with those two spaces. It is still valid JSON, and the alternative would be a second answer
        // to "is this object empty" that no part can ask.
        var model = NewModel();
        var document = model.NewNode(JsonConceptKeys.Object);
        var note = model.NewChild(document, JsonLanguage.MembersSlot, JsonConceptKeys.Comment);
        note.Set(JsonLanguage.TextSlot, "why");

        Assert.Equal("{  }", Generate(document));
    }

    [Fact]
    public void TheGeneratorIsNamedSoAMenuCanListIt()
    {
        Assert.Equal("Json", JsonTextGenerators.Json.Name);
        Assert.Same(JsonTextGenerators.Json, Assert.Single(JsonTextGenerators.All));
    }

    private static string Generate(Node node)
    {
        var generated = new Generator().Generate(node, JsonTextGenerators.Json);

        Assert.True(generated.Succeeded, generated.ToString());
        return generated.Text;
    }

    private static Model NewModel() => JsonLanguage.NewProject().NewModel("json");
}
