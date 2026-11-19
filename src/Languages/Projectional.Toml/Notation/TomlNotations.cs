using Projectional.Language;

namespace Projectional.Toml;

/// <summary>How TOML is drawn: the file's own lines, with the structure the cells carry.</summary>
/// <remarks>
/// <para>
/// TOML's structure *is* its layout - a header opens a table and every following pair belongs to it - so the drawing
/// keeps that shape: a pair as <c>key = value</c>, a table as <c>[key]</c> with its entries indented under it, an array
/// of tables as <c>[[key]]</c>, and an inline table as <c>{ a = 1 }</c> in the line where it stands.
/// </para>
/// <para>
/// The delimiters of a string are drawn from its <c>kind</c> slot rather than stored in the value, exactly as JSON's
/// quotes are: the slot holds what sits between them, so the caret never lands inside a character the user cannot
/// delete. A <see cref="ConditionalNotationCell"/> is what lets four spellings share one concept - it draws nothing
/// when the condition does not hold, which is why a <c>basic</c> string shows one quote and not four.
/// </para>
/// <para>
/// The <c>kind</c> of a date and time and the key of a header are not drawn as cells of their own where the notation
/// already says them; the kind is still a slot, so a check and a reader can ask it.
/// </para>
/// </remarks>
internal static class TomlNotations
{
    /// <summary>What an empty entry list draws: one cell, so the slot stays a caret stop.</summary>
    private const string NoEntries = "empty";

    internal static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(TomlConceptKeys.Document, ed => ed.Sequence(
            ed.Children(TomlLanguage.EntriesSlot, NoEntries, stacked: true)));

        notations.Add(TomlConceptKeys.KeyValue, ed => ed.Line(
            ed.Property(TomlLanguage.KeySlot),
            ed.Punct(" = "),
            ed.ChildInline(TomlLanguage.ValueSlot)));

        notations.Add(TomlConceptKeys.Table, ed => ed.Sequence(
            ed.Line(ed.Punct("["), ed.Property(TomlLanguage.KeySlot), ed.Punct("]")),
            ed.Indent(ed.Children(TomlLanguage.EntriesSlot, NoEntries, stacked: true))));

        notations.Add(TomlConceptKeys.ArrayOfTables, ed => ed.Sequence(
            ed.Line(ed.Punct("[["), ed.Property(TomlLanguage.KeySlot), ed.Punct("]]")),
            ed.Indent(ed.Children(TomlLanguage.EntriesSlot, NoEntries, stacked: true))));

        notations.Add(TomlConceptKeys.Comment, ed => ed.Line(
            ed.Punct("#"),
            ed.Property(TomlLanguage.TextSlot, emptyText: "note")));

        notations.Add(TomlConceptKeys.String, ed => ed.Line(
            ed.Conditional(TomlLanguage.KindSlot, TomlLanguage.BasicString, ed.Punct("\"")),
            ed.Conditional(TomlLanguage.KindSlot, TomlLanguage.MultilineBasicString, ed.Punct("\"\"\"")),
            ed.Conditional(TomlLanguage.KindSlot, TomlLanguage.LiteralString, ed.Punct("'")),
            ed.Conditional(TomlLanguage.KindSlot, TomlLanguage.MultilineLiteralString, ed.Punct("'''")),
            ed.Property(TomlLanguage.TextSlot, emptyText: "text"),
            ed.Conditional(TomlLanguage.KindSlot, TomlLanguage.BasicString, ed.Punct("\"")),
            ed.Conditional(TomlLanguage.KindSlot, TomlLanguage.MultilineBasicString, ed.Punct("\"\"\"")),
            ed.Conditional(TomlLanguage.KindSlot, TomlLanguage.LiteralString, ed.Punct("'")),
            ed.Conditional(TomlLanguage.KindSlot, TomlLanguage.MultilineLiteralString, ed.Punct("'''"))));

        notations.Add(TomlConceptKeys.Integer, ed => ed.Line(ed.Property(TomlLanguage.TextSlot)));

        notations.Add(TomlConceptKeys.Float, ed => ed.Line(ed.Property(TomlLanguage.TextSlot)));

        // The kind is not drawn: the text already says which of the four a value is, and a second cell repeating it
        // would be a value the user could type into and get wrong.
        notations.Add(TomlConceptKeys.DateTime, ed => ed.Line(ed.Property(TomlLanguage.TextSlot)));

        notations.Add(TomlConceptKeys.Bool, ed => ed.Line(ed.Property(TomlLanguage.ValueSlot)));

        notations.Add(TomlConceptKeys.Array, ed => ed.Line(
            ed.Punct("["),
            ed.WhenFilled(TomlLanguage.ItemsSlot, ed.Space),
            ed.InlineCollection(TomlLanguage.ItemsSlot, ed.Punct(","), " "),
            ed.WhenFilled(TomlLanguage.ItemsSlot, ed.Space),
            ed.Punct("]")));

        notations.Add(TomlConceptKeys.InlineTable, ed => ed.Line(
            ed.Punct("{"),
            ed.WhenFilled(TomlLanguage.MembersSlot, ed.Space),
            ed.InlineCollection(TomlLanguage.MembersSlot, ed.Punct(","), " "),
            ed.WhenFilled(TomlLanguage.MembersSlot, ed.Space),
            ed.Punct("}")));
    }
}
