using Projectional.Core;
using Projectional.Language;

namespace Projectional.TextGen;

/// <summary>
/// How a generator is drawn, in the shape MPS writes one: a header per generator, then one mapping per concept with
/// its template indented under it.
/// </summary>
/// <remarks>
/// A template part is drawn as the glyph that says what it does rather than as a nested block of prose - <c>"text"</c>
/// for a literal, <c>$slot</c> for a property, <c>(slot)</c> for a child, <c>[slot]</c> for a list - because it is
/// short enough to be read in place, and a generator that needed a line per part would hide its own shape.
/// <c>&lt;&lt; ... &gt;&gt;</c> is the empty-section placeholder, the same glyph the other definition languages use.
/// </remarks>
public static class TextGenNotations
{
    private const string NoEntries = "<< ... >>";

    /// <summary>The notation registry of the generator language.</summary>
    public static NotationRegistry Registry { get; } = Create(TextGenLanguage.Concepts);

    /// <summary>The notation registry of <paramref name="concepts"/>.</summary>
    public static NotationRegistry Create(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);

        AddTo(notations);

        return notations;
    }

    /// <summary>Registers the notations into <paramref name="notations"/>.</summary>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(TextGenConceptKeys.Generator, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("generate", TextEmphasis.Bold),
                ed.Space,
                ed.Property(TextGenLanguage.NameSlot)),
            ed.Indent(ed.Children(TextGenLanguage.MappingsSlot, NoEntries))));

        notations.Add(TextGenConceptKeys.Mapping, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("map"),
                ed.Space,
                ed.Property(TextGenLanguage.ConceptKeySlot),
                ed.Space,
                ed.Keyword("to")),
            ed.Indent(ed.Line(ed.ChildInline(TextGenLanguage.TemplateSlot)))));

        notations.Add(TextGenConceptKeys.Template, ed => ed.Sequence(
            ed.Line(ed.Keyword("template")),
            ed.Indent(ed.Children(TextGenLanguage.PartsSlot, NoEntries))));

        notations.Add(TextGenConceptKeys.Text, ed => ed.Line(
            ed.Keyword("\""),
            ed.Property(TextGenLanguage.TextSlot),
            ed.Keyword("\"")));

        notations.Add(TextGenConceptKeys.Property, ed => ed.Line(
            ed.Keyword("$"),
            ed.Property(TextGenLanguage.ConceptKeySlot)));

        // `<slot>` says "what this points at", which is the one thing a reference part emits. The two stand-in
        // texts are deliberately not drawn: a conditional cell can say "when this value holds", not "when it
        // differs from the default", and an always-visible `<slot> or <unset>` would bury the part's meaning.
        // They are still editable through the property tip.
        notations.Add(TextGenConceptKeys.Reference, ed => ed.Line(
            ed.Keyword("<"),
            ed.Property(TextGenLanguage.ConceptKeySlot),
            ed.Keyword(">")));

        notations.Add(TextGenConceptKeys.Child, ed => ed.Line(
            ed.Keyword("("),
            ed.Property(TextGenLanguage.ConceptKeySlot),
            ed.Keyword(")")));

        notations.Add(TextGenConceptKeys.Children, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("["),
                ed.Property(TextGenLanguage.ConceptKeySlot),
                ed.Keyword("]"),
                ed.Keyword(" separated by")),
            ed.Indent(ed.Line(ed.ChildInline(TextGenLanguage.SeparatorSlot)))));

        notations.Add(TextGenConceptKeys.Loop, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("for each"),
                ed.Space,
                ed.Property(TextGenLanguage.ConceptKeySlot),
                ed.Keyword(":"),
                ed.Keyword("separated by")),
            ed.Indent(ed.Sequence(
                ed.Line(ed.ChildInline(TextGenLanguage.SeparatorSlot)),
                ed.Line(ed.Keyword("emit")),
                ed.Indent(ed.Line(ed.ChildInline(TextGenLanguage.BodySlot)))))));

        notations.Add(TextGenConceptKeys.Indent, ed => ed.Sequence(
            ed.Line(ed.Keyword("indent")),
            ed.Indent(ed.Line(ed.ChildInline(TextGenLanguage.BodySlot)))));

        notations.Add(TextGenConceptKeys.Line, ed => ed.Line(ed.Keyword("newline")));

        notations.Add(TextGenConceptKeys.Conditional, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("when"),
                ed.Space,
                ed.Property(TextGenLanguage.ConceptKeySlot),
                ed.Space,
                ed.Keyword("is"),
                ed.Space,
                ed.Property(TextGenLanguage.ValueSlot),
                ed.Keyword(":")),
            ed.Indent(ed.Sequence(
                ed.Line(ed.ChildInline(TextGenLanguage.BodySlot)),
                ed.Line(ed.Keyword("otherwise:")),
                ed.Indent(ed.Line(ed.ChildInline(TextGenLanguage.OtherwiseSlot)))))));

        notations.Add(TextGenConceptKeys.WhenFilled, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("when"),
                ed.Space,
                ed.Property(TextGenLanguage.ConceptKeySlot),
                ed.Space,
                ed.Keyword("is filled:")),
            ed.Indent(ed.Sequence(
                ed.Line(ed.ChildInline(TextGenLanguage.BodySlot)),
                ed.Line(ed.Keyword("otherwise:")),
                ed.Indent(ed.Line(ed.ChildInline(TextGenLanguage.OtherwiseSlot)))))));

        notations.Add(TextGenConceptKeys.Self, ed => ed.Line(ed.Keyword("self")));
    }
}
