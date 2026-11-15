using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;

namespace Projectional.Samples;

/// <summary>
/// A toy block language: a named set of rules, each of which decides an outcome from a condition.
/// </summary>
/// <remarks>
/// <para>
/// This is the sample the design documents describe and the standalone editor opens, and it is written
/// as a host would write it: the condition slot is declared over
/// <see cref="ExpressionsConceptKeys.Expression"/>, so every expression the shared sub-language
/// declares can stand there, and none of that vocabulary is declared here.
/// </para>
/// <para>
/// It is deliberately not a package. It exists to prove that composing a language out of a shared
/// sub-language is a handful of declarations and a notation - if this file grew to look like a
/// framework, the composition story would have failed.
/// </para>
/// </remarks>
public static class RulesLanguage
{
    /// <summary>The concepts this language adds, which are the only ones it declares.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } = CreateDeclarations();

    /// <summary>This language's concepts together with the expression vocabulary it uses.</summary>
    public static ConceptRegistry Concepts { get; } =
        ConceptRegistry.Create([.. ExpressionsLanguage.ConceptDeclarations, .. ConceptDeclarations]);

    /// <summary>A registry holding both languages' notation.</summary>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);
        ExpressionsLanguage.AddNotations(notations);
        RulesNotations.AddTo(notations);

        return notations;
    }

    /// <summary>A project whose language is <see cref="Concepts"/>.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>
    /// What decorates a model of this language beyond what its notation already says.
    /// </summary>
    /// <remarks>
    /// A host passes this to the session; it is optional, because a language whose looks never depend
    /// on what the model holds needs nothing here.
    /// </remarks>
    public static ICellEmphasis Emphasis => RejectedRuleEmphasis.Instance;

    private static List<ConceptDeclaration> CreateDeclarations() =>
    [
        new ConceptDeclaration(
            RulesConceptKeys.Ruleset,
            [
                new PropertySlot("name", PropertyType.String, "unnamed"),
                new ChildSlot("rules", RulesConceptKeys.Rule),
            ],
            nameSlot: "name"),

        new ConceptDeclaration(
            RulesConceptKeys.Rule,
            [
                new PropertySlot("name", PropertyType.String, "unnamed"),
                new PropertySlot("outcome", PropertyType.Enum([RuleOutcomes.Accept, RuleOutcomes.Review, RuleOutcomes.Reject]), RuleOutcomes.Review),
                new ChildSlot("condition", ExpressionsConceptKeys.Expression, Cardinality.One),
            ],
            nameSlot: "name"),
    ];
}
