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
    /// <summary>The variant that reads a ruleset as a table of its rules.</summary>
    /// <remarks>
    /// A host offers this name to the reader as the alternative representation; declaring it here keeps
    /// the string the notation is registered under and the string a host asks for the same one.
    /// </remarks>
    public const string TableVariant = "table";

    /// <summary>The slot of a ruleset that holds its rules.</summary>
    /// <remarks>
    /// Named here rather than written out in each place that needs it - the notation, the declarations and
    /// the sample checks - because a slot name is part of the language, and a check that spelled it itself
    /// would keep compiling after the language renamed it.
    /// </remarks>
    public const string RulesSlot = "rules";

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
    /// What this language says about text typed into an empty slot.
    /// </summary>
    /// <remarks>
    /// Borrowed from the expression sub-language rather than declared again, for the same reason the
    /// concepts are: the rules that decide a digit is an integer literal belong to whoever declares
    /// integer literals. This language adds none of its own, because the only slots it can leave empty are
    /// its ruleset's rule list - a list, not one empty slot - and every condition is declared over an
    /// expression.
    /// </remarks>
    public static ITypingRules TypingRules => ExpressionsLanguage.TypingRules;

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
                new ChildSlot(RulesSlot, RulesConceptKeys.Rule),
            ],
            nameSlot: "name",
            description: "A named set of rules, evaluated in the order they are written."),

        new ConceptDeclaration(
            RulesConceptKeys.Rule,
            [
                new PropertySlot("name", PropertyType.String, "unnamed"),
                new PropertySlot("outcome", PropertyType.Enum([RuleOutcomes.Accept, RuleOutcomes.Review, RuleOutcomes.Reject]), RuleOutcomes.Review),
                new ChildSlot("condition", ExpressionsConceptKeys.Expression, Cardinality.One),

                // The permission to fold, as a value on the node: a rule with a long body is worth
                // hiding and its sibling with one line is not. Defaulting to true means every rule folds
                // unless it opts out, which is the cheapest way for a language to say "this can be big".
                new PropertySlot("foldable", PropertyType.Boolean, "true"),
            ],
            nameSlot: "name",
            description: "One rule: a name, an optional condition and the outcome it decides.",
            collapseSlot: "foldable"),
    ];
}
