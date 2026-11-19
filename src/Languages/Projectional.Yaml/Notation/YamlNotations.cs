using Projectional.Language;

namespace Projectional.Yaml;

/// <summary>How YAML is drawn: a block mapping as one line per member, a block sequence as one dash per item.</summary>
/// <remarks>
/// <para>
/// The two collection kinds are separate concepts, so each draws the shape it is: a block mapping as
/// <c>key: value</c> lines, a block sequence as <c>- value</c> lines, a flow collection in braces or brackets with its
/// members separated by commas. That is a decision rather than a convenience - notation is chosen per concept and not per
/// node, so a <c>style</c> slot could not have given one concept two drawings.
/// </para>
/// <para>
/// A pair's value is drawn inline, which is not always how the file writes it: a block collection has to go on the next
/// line, and <see cref="YamlLanguage.LayoutSlot"/> is what the generator reads for that. A drawing is not the file, and
/// the layout engine breaks the line by itself when the child's notation is vertical.
/// </para>
/// </remarks>
internal static class YamlNotations
{
    /// <summary>What an empty member or item list draws: one cell, so the slot stays a caret stop.</summary>
    private const string Nothing = "empty";

    internal static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(YamlConceptKeys.Document, ed => ed.Sequence(
            ed.Children(YamlLanguage.EntriesSlot, Nothing, stacked: true)));

        notations.Add(YamlConceptKeys.Mapping, ed => ed.Sequence(
            ed.Children(YamlLanguage.MembersSlot, Nothing, stacked: true)));

        notations.Add(YamlConceptKeys.Sequence, ed => ed.Sequence(
            ed.Children(YamlLanguage.ItemsSlot, Nothing, stacked: true)));

        notations.Add(YamlConceptKeys.SequenceItem, ed => ed.Line(
            ed.Punct("-"),
            ed.Space,
            ed.ChildInline(YamlLanguage.ValueSlot)));

        notations.Add(YamlConceptKeys.Pair, ed => ed.Line(
            ed.ChildInline(YamlLanguage.KeySlot),
            ed.Punct(":"),
            ed.WhenFilled(YamlLanguage.ValueSlot, ed.Space, ed.ChildInline(YamlLanguage.ValueSlot))));

        notations.Add(YamlConceptKeys.FlowMapping, ed => ed.Line(
            ed.Punct("{"),
            ed.WhenFilled(YamlLanguage.MembersSlot, ed.Space),
            ed.InlineCollection(YamlLanguage.MembersSlot, ed.Punct(","), " "),
            ed.WhenFilled(YamlLanguage.MembersSlot, ed.Space),
            ed.Punct("}")));

        notations.Add(YamlConceptKeys.FlowPair, ed => ed.Line(
            ed.ChildInline(YamlLanguage.KeySlot),
            ed.Punct(": "),
            ed.ChildInline(YamlLanguage.ValueSlot)));

        notations.Add(YamlConceptKeys.FlowSequence, ed => ed.Line(
            ed.Punct("["),
            ed.WhenFilled(YamlLanguage.ItemsSlot, ed.Space),
            ed.InlineCollection(YamlLanguage.ItemsSlot, ed.Punct(","), " "),
            ed.WhenFilled(YamlLanguage.ItemsSlot, ed.Space),
            ed.Punct("]")));

        notations.Add(YamlConceptKeys.Scalar, ed => ed.Line(ed.Property(YamlLanguage.TextSlot)));

        notations.Add(YamlConceptKeys.Alias, ed => ed.Line(
            ed.Punct("*"),
            ed.Property(YamlLanguage.NameSlot)));

        notations.Add(YamlConceptKeys.Comment, ed => ed.Line(
            ed.Punct("#"),
            ed.Property(YamlLanguage.TextSlot, emptyText: "note")));

        notations.Add(YamlConceptKeys.Raw, ed => ed.Line(ed.Property(YamlLanguage.TextSlot)));
    }
}
