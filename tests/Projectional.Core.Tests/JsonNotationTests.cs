using Projectional.Core;
using Projectional.Json;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>How a JSON document is drawn: the file's own punctuation, with the value in a cell of its own.</summary>
/// <remarks>
/// The drawing is asserted as text because that is what a reader sees, and the interesting cases are the two the cell
/// model had to reach for a new part to express: an object that holds a member draws a space around it, and an object
/// that holds none still shows the slot that can be filled.
/// </remarks>
public class JsonNotationTests
{
    [Fact]
    public void AnObjectIsDrawnAsTheFileReads()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        var document = model.NewNode(JsonConceptKeys.Object);

        var first = model.NewChild(document, JsonLanguage.MembersSlot, JsonConceptKeys.Property);
        first.Set(JsonLanguage.NameSlot, "a");
        model.NewChild(first, JsonLanguage.ValueSlot, JsonConceptKeys.Number)
            .Set(JsonLanguage.TextSlot, "1");

        var second = model.NewChild(document, JsonLanguage.MembersSlot, JsonConceptKeys.Property);
        second.Set(JsonLanguage.NameSlot, "b");
        var array = model.NewChild(second, JsonLanguage.ValueSlot, JsonConceptKeys.Array);
        model.NewChild(array, JsonLanguage.ItemsSlot, JsonConceptKeys.Bool)
            .Set(JsonLanguage.ValueSlot, "true");
        model.NewChild(array, JsonLanguage.ItemsSlot, JsonConceptKeys.Null);

        Assert.Equal("{ \"a\": 1, \"b\": [ true, null ] }", Render(document));
    }

    [Fact]
    public void AnEmptyObjectKeepsItsSlotVisible()
    {
        // One cell, not none: an empty object is where the first member is typed, so hiding the placeholder would make
        // the only reachable gesture disappear.
        var model = JsonLanguage.NewProject().NewModel("json");

        Assert.Equal("{ }", Render(model.NewNode(JsonConceptKeys.Object)));
    }

    [Fact]
    public void AnEmptyArrayKeepsItsSlotVisible()
    {
        var model = JsonLanguage.NewProject().NewModel("json");

        Assert.Equal("[ ]", Render(model.NewNode(JsonConceptKeys.Array)));
    }

    [Fact]
    public void AStringIsDrawnBetweenItsQuotes()
    {
        // The quotes are constants of the notation, so the caret never lands inside one: the slot holds the text
        // between them and that is all the user can edit.
        var model = JsonLanguage.NewProject().NewModel("json");
        var text = model.NewNode(JsonConceptKeys.Text);
        text.Set(JsonLanguage.TextSlot, "a\\\"b");

        Assert.Equal("\"a\\\"b\"", Render(text));
    }

    [Fact]
    public void ACommentIsDrawnAsTheNoteItIs()
    {
        // The drawing says what the model holds; the generated file says what JSON can carry. A note is the one place
        // those two answers differ on purpose.
        var model = JsonLanguage.NewProject().NewModel("json");
        var comment = model.NewNode(JsonConceptKeys.Comment);
        comment.Set(JsonLanguage.TextSlot, "why");

        Assert.Equal("// why", Render(comment));
    }

    private static string Render(Node node) =>
        LayoutSnapshot.Render(node, JsonLanguage.Notations, width: 120).Trim();
}
