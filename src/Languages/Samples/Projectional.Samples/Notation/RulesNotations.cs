using Projectional.Language;

namespace Projectional.Samples;

/// <summary>
/// How the rules language looks: a ruleset is a braced block, and a rule is one line that reads
/// left to right.
/// </summary>
internal static class RulesNotations
{
    /// <summary>The room the boxed table keeps around its grid, in layout units.</summary>
    private const double TablePadding = 4;

    internal static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(RulesConceptKeys.Ruleset, ed => ed.Sequence(
            ed.Line(ed.Keyword("ruleset", TextEmphasis.Bold), ed.Space, ed.Property("name"), ed.Space, ed.Punct("{")),
            ed.Indent(ed.Children("rules", stacked: true)),
            ed.Line(ed.Punct("}"))));

        // The same ruleset read as a table: the rules are rows, so a reader comparing outcomes sees a
        // column instead of a sequence of lines whose fields sit at different places.
        notations.Add(RulesConceptKeys.Ruleset, RulesLanguage.TableVariant, ed => ed.Sequence(
            ed.Line(ed.Keyword("ruleset", TextEmphasis.Bold), ed.Space, ed.Property("name")),
            ed.Indent(ed.Table(
                "rules",
                new CellPaint(padding: CellEdges.All(TablePadding), border: new CellBorder(1)),
                PaintRole.Muted,
                emptyText: "+ rule",
                ed.Column("name", ed.Keyword("rule")),
                ed.Column("outcome", ed.Keyword("then")),
                ed.Column("condition", ed.Keyword("when"))))));

        notations.Add(RulesConceptKeys.Rule, ed => ed.Line(
            ed.Keyword("rule", TextEmphasis.Bold),
            ed.Space,
            ed.Property("name"),
            ed.Space,
            ed.Keyword("when", TextEmphasis.Italic),
            ed.Space,
            ed.ChildInline("condition"),
            ed.Space,
            ed.Keyword("then", TextEmphasis.Underline),
            ed.Space,
            ed.Property("outcome")));
    }
}
