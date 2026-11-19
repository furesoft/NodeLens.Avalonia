using Projectional.Language;

namespace Projectional.Json;

/// <summary>How JSON is drawn: the file's own punctuation, with the structure the cells carry.</summary>
/// <remarks>
/// <para>
/// Every concept here is one <see cref="LineNotationCell"/>, so a document draws as the file reads - an object as
/// <c>{ "a": 1, "b": 2 }</c>, an array as <c>[1, 2, 3]</c>. That is a decision with a cost and a reason: the layout engine
/// does not wrap, so a deeply nested document is one long line. A reference language's job is to show what the file says,
/// and a projection that re-broke the lines would show a shape the file does not have.
/// </para>
/// <para>
/// The gap inside a bracket is a <see cref="WhenFilledNotationCell"/>, which is the only way to draw
/// <c>{ "a": 1 }</c> - one space on each side - while an object that holds nothing still draws the one cell that says
/// it can be filled. Without it the drawing would differ from the text the same language generates.
/// </para>
/// <para>
/// The quotes around a name and around a string are constants of the notation, not part of the value: the slot holds the
/// text between them, so the caret never sits inside a character the user cannot delete.
/// </para>
/// </remarks>
internal static class JsonNotations
{
    /// <summary>What an empty member list or item list draws: one space, so the slot stays a caret stop.</summary>
    private const string Empty = " ";

    internal static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(JsonConceptKeys.Object, ed => ed.Line(
            ed.Punct("{"),
            ed.WhenFilled(JsonLanguage.MembersSlot, ed.Space),
            ed.InlineCollection(JsonLanguage.MembersSlot, ed.Punct(","), Empty),
            ed.WhenFilled(JsonLanguage.MembersSlot, ed.Space),
            ed.Punct("}")));

        notations.Add(JsonConceptKeys.Property, ed => ed.Line(
            ed.Punct("\""),
            ed.Property(JsonLanguage.NameSlot, emptyText: "name"),
            ed.Punct("\":"),
            ed.Space,
            ed.ChildInline(JsonLanguage.ValueSlot)));

        notations.Add(JsonConceptKeys.Array, ed => ed.Line(
            ed.Punct("["),
            ed.WhenFilled(JsonLanguage.ItemsSlot, ed.Space),
            ed.InlineCollection(JsonLanguage.ItemsSlot, ed.Punct(","), Empty),
            ed.WhenFilled(JsonLanguage.ItemsSlot, ed.Space),
            ed.Punct("]")));

        notations.Add(JsonConceptKeys.Text, ed => ed.Line(
            ed.Punct("\""),
            ed.Property(JsonLanguage.TextSlot, emptyText: "text"),
            ed.Punct("\"")));

        // No quotes and no emphasis: a number is the one leaf whose text is the whole of what it means.
        notations.Add(JsonConceptKeys.Number, ed => ed.Line(ed.Property(JsonLanguage.TextSlot)));

        notations.Add(JsonConceptKeys.Bool, ed => ed.Line(ed.Property(JsonLanguage.ValueSlot)));

        notations.Add(JsonConceptKeys.Null, ed => ed.Line(ed.Keyword("null")));
    }
}
