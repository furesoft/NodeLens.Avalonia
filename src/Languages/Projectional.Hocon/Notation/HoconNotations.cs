using Projectional.Language;

namespace Projectional.Hocon;

/// <summary>How HOCON is drawn: the file's own lines, with the separator a property was written with as its own cell.</summary>
/// <remarks>
/// <para>
/// A HOCON document is drawn as the object it is, without the braces the file also omits: <c>key = value</c> per line,
/// an object between braces with its members indented, an array in brackets. The point of this language is that a file
/// reads as the thing a user typed rather than as the tree it resolves to, so the separator is visible as written -
/// <c>=</c>, <c>:</c>, <c>+=</c> or the whitespace form - and a substitution and an include are the one line they are.
/// </para>
/// <para>
/// The four separators are one property and four conditional cells, exactly as TOML's four string kinds are: a cell
/// behind a conditional is drawn only while its value holds, so one concept spells four syntaxes without a second
/// concept. The alternative - four concepts - would make "change the separator" a delete and an insert.
/// </para>
/// </remarks>
internal static class HoconNotations
{
    /// <summary>What an empty member list draws: one cell, so the slot stays a caret stop.</summary>
    private const string Nothing = "empty";

    internal static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(HoconConceptKeys.Document, ed => ed.Sequence(
            ed.Children(HoconLanguage.MembersSlot, Nothing, stacked: true)));

        notations.Add(HoconConceptKeys.Object, ed => ed.Sequence(
            ed.Line(ed.Punct("{")),
            ed.Indent(ed.Children(HoconLanguage.MembersSlot, Nothing, stacked: true)),
            ed.Line(ed.Punct("}"))));

        notations.Add(HoconConceptKeys.Property, ed => ed.Line(
            ed.Property(HoconLanguage.KeySlot),
            ed.Conditional(HoconLanguage.OperatorSlot, HoconLanguage.EqualsOperator, ed.Punct(" = ")),
            ed.Conditional(HoconLanguage.OperatorSlot, HoconLanguage.ColonOperator, ed.Punct(": ")),
            ed.Conditional(HoconLanguage.OperatorSlot, HoconLanguage.AppendOperator, ed.Punct(" += ")),
            ed.Conditional(HoconLanguage.OperatorSlot, HoconLanguage.SpaceOperator, ed.Space),
            ed.ChildInline(HoconLanguage.ValueSlot)));

        notations.Add(HoconConceptKeys.Array, ed => ed.Line(
            ed.Punct("["),
            ed.WhenFilled(HoconLanguage.ItemsSlot, ed.Space),
            ed.InlineCollection(HoconLanguage.ItemsSlot, ed.Punct(","), " "),
            ed.WhenFilled(HoconLanguage.ItemsSlot, ed.Space),
            ed.Punct("]")));

        notations.Add(HoconConceptKeys.String, ed => ed.Line(
            ed.Conditional(HoconLanguage.QuotedSlot, HoconLanguage.Quoted, ed.Punct("\"")),
            ed.Conditional(HoconLanguage.QuotedSlot, HoconLanguage.TripleQuoted, ed.Punct("\"\"\"")),
            ed.Property(HoconLanguage.TextSlot, emptyText: "text"),
            ed.Conditional(HoconLanguage.QuotedSlot, HoconLanguage.Quoted, ed.Punct("\"")),
            ed.Conditional(HoconLanguage.QuotedSlot, HoconLanguage.TripleQuoted, ed.Punct("\"\"\""))));

        notations.Add(HoconConceptKeys.Number, ed => ed.Line(ed.Property(HoconLanguage.LiteralSlot)));

        notations.Add(HoconConceptKeys.Bool, ed => ed.Line(ed.Property(HoconLanguage.ValueSlot)));

        notations.Add(HoconConceptKeys.Null, ed => ed.Line(ed.Keyword("null")));

        // Juxtaposition is the separator, so the list is drawn with a space and no gap of its own - two spaces would be
        // a different concatenation to a reader who knows the format.
        notations.Add(HoconConceptKeys.Concat, ed => ed.Line(
            ed.InlineCollection(HoconLanguage.PartsSlot, ed.Space, emptyText: "value", spacing: 0)));

        notations.Add(HoconConceptKeys.Substitution, ed => ed.Line(
            ed.Punct("${"),
            ed.Conditional(HoconLanguage.OptionalSlot, HoconLanguage.Optional, ed.Punct("?")),
            ed.Property(HoconLanguage.PathSlot, emptyText: "name"),
            ed.Punct("}")));

        notations.Add(HoconConceptKeys.Include, ed => ed.Line(
            ed.Keyword("include"),
            ed.Space,
            ed.Property(HoconLanguage.TargetSlot)));

        notations.Add(HoconConceptKeys.Comment, ed => ed.Line(
            ed.Conditional(HoconLanguage.MarkerSlot, "#", ed.Punct("#")),
            ed.Conditional(HoconLanguage.MarkerSlot, "//", ed.Punct("//")),
            ed.Property(HoconLanguage.TextSlot, emptyText: "note")));
    }
}
