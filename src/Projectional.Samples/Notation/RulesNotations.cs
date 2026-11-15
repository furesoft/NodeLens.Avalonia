using Projectional.Language;

namespace Projectional.Samples;

/// <summary>
/// How the rules language looks: a ruleset is a braced block, and a rule is one line that reads
/// left to right.
/// </summary>
internal static class RulesNotations
{
    internal static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(RulesConceptKeys.Ruleset, ed => ed.Sequence(
            ed.Line(ed.Keyword("ruleset", TextEmphasis.Bold), ed.Space, ed.Property("name"), ed.Space, ed.Punct("{")),
            ed.Indent(ed.Children("rules")),
            ed.Line(ed.Punct("}"))));

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
