using Projectional.Language;

namespace Projectional.Samples.FeatureModel;

/// <summary>How a feature model looks: a block of features, then a block of the rules between them.</summary>
/// <remarks>
/// <para>
/// <c>selected</c> is a <see cref="ConditionalNotationCell"/> over the feature's own property, so a feature
/// that is not selected shows its name and nothing else. The selection is therefore visible everywhere the
/// feature is drawn, which is the whole argument for a conditional cell: the alternative - a separate list of
/// selected names - would let the configuration and the drawing disagree.
/// </para>
/// <para>
/// The two constraints are drawn by two notations that differ in one word. That is deliberate rather than
/// duplicated: each concept says what it means, and the drawn word <em>is</em> the meaning, so neither needs an
/// operator property a reader has to interpret.
/// </para>
/// </remarks>
internal static class FeatureModelNotations
{
    internal static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(FmConceptKeys.Model, ed => ed.Sequence(
            ed.Line(ed.Keyword("model", TextEmphasis.Bold), ed.Space, ed.Property("name"), ed.Space, ed.Punct("{")),
            ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("features:")),
                ed.Indent(ed.Children(FeatureModelLanguage.FeaturesSlot, "<none>", stacked: true)),
                ed.Line(ed.Keyword("constraints:")),
                ed.Indent(ed.Children(FeatureModelLanguage.ConstraintsSlot, "<none>", stacked: true)))),
            ed.Line(ed.Punct("}"))));

        notations.Add(FmConceptKeys.Feature, ed => ed.Line(
            ed.Keyword("feature", TextEmphasis.Bold),
            ed.Space,
            ed.Property("name"),
            ed.Conditional(
                FeatureModelLanguage.SelectedSlot,
                "true",
                ed.Space,
                ed.Keyword("selected", TextEmphasis.Italic))));

        notations.Add(FmConceptKeys.Requires, ed => ed.Line(
            ed.Reference(FeatureModelLanguage.SubjectSlot),
            ed.Space,
            ed.Keyword("requires", TextEmphasis.Italic),
            ed.Space,
            ed.Reference(FeatureModelLanguage.OtherSlot)));

        notations.Add(FmConceptKeys.Excludes, ed => ed.Line(
            ed.Reference(FeatureModelLanguage.SubjectSlot),
            ed.Space,
            ed.Keyword("excludes", TextEmphasis.Italic),
            ed.Space,
            ed.Reference(FeatureModelLanguage.OtherSlot)));
    }
}
