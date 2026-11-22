using Projectional.Core;
using Projectional.Language;

namespace Projectional.EvaluationRules;

/// <summary>The language an evaluation rule is written in: concepts whose nodes <em>are</em> a rule.</summary>
/// <remarks>
/// <para>
/// It is a language like any other - same editor, same writer, same undo - which is what makes the two authoring paths of
/// ADR 0014 equal. <see cref="EvaluationRuleLowering"/> is the C# twin of the language, the way
/// <c>TypeRuleLowering</c> is the twin of the type rules.
/// </para>
/// <para>
/// What it does not cover, and cannot: the <em>meaning</em> of an operator symbol. That is a computation, and this
/// repository has no language for computation - so an <see cref="EvaluationRuleConceptKeys.Operator"/> body names the
/// property that holds the symbol and the framework answers what it does
/// (<see cref="OperatorSemantics"/>). A concept whose meaning is a computation of its own - resolving a call, deciding by
/// the first non-empty entry - keeps a hand-written rule, which is the escape hatch an aspect as an interface exists for.
/// </para>
/// </remarks>
public static class EvaluationRuleLanguage
{
    /// <summary>The property naming the concept a rule answers for, such as <c>Expressions.Binary</c>.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>The slot holding a rule's body.</summary>
    public const string BodySlot = "body";

    /// <summary>The property naming a slot of the node a body reads.</summary>
    public const string SlotSlot = "slot";

    /// <summary>The property naming the property that holds an operator symbol.</summary>
    public const string OperatorSlot = "operatorSlot";

    /// <summary>The property naming the child slot of the left operand.</summary>
    public const string LeftSlot = "leftSlot";

    /// <summary>The property naming the child slot of the right operand, empty for a unary operator.</summary>
    public const string RightSlot = "rightSlot";

    /// <summary>The property naming the child slot whose value the choose body reads.</summary>
    public const string ConditionSlot = "conditionSlot";

    /// <summary>The body a choose body answers with while its condition is true.</summary>
    public const string ThenSlot = "then";

    /// <summary>The body a choose body answers with otherwise.</summary>
    public const string OtherwiseSlot = "otherwise";

    /// <summary>Every concept this language declares, in declaration order.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } = CreateDeclarations();

    /// <summary>This language's concepts on their own.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A registry holding this language's notation and nothing else.</summary>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);
        EvaluationRuleNotations.AddTo(notations);

        return notations;
    }

    /// <summary>Adds the notations of this language to an existing registry.</summary>
    /// <exception cref="NotationException">
    /// <paramref name="notations"/> was built for a concept registry that does not contain these concepts.
    /// </exception>
    public static void AddNotations(NotationRegistry notations) => EvaluationRuleNotations.AddTo(notations);

    private static List<ConceptDeclaration> CreateDeclarations() =>
    [
        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Rule,
            [
                new PropertySlot(ConceptKeySlot, PropertyType.String, description: "The concept this rule answers for, such as Expressions.Binary."),
                new ChildSlot(BodySlot, EvaluationRuleConceptKeys.Body, Cardinality.One),
            ],
            description: "What one concept evaluates to.",
            nameSlot: ConceptKeySlot),

        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Body,
            isAbstract: true,
            description: "What a rule answers with, as one of the five bodies below."),

        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Property,
            [new PropertySlot(SlotSlot, PropertyType.String)],
            [EvaluationRuleConceptKeys.Body],
            description: "The value of a property, read through the type the property declares.",
            nameSlot: SlotSlot),

        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Child,
            [new PropertySlot(SlotSlot, PropertyType.String)],
            [EvaluationRuleConceptKeys.Body],
            description: "The value of the single child in a slot.",
            nameSlot: SlotSlot),

        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Variable,
            [new PropertySlot(SlotSlot, PropertyType.String)],
            [EvaluationRuleConceptKeys.Body],
            description: "The value the run bound to the name a property holds.",
            nameSlot: SlotSlot),

        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Operator,
            [
                new PropertySlot(OperatorSlot, PropertyType.String, description: "The property holding the symbol, such as operator."),
                new PropertySlot(LeftSlot, PropertyType.String, description: "The child slot of the left operand, or the only one."),
                new PropertySlot(RightSlot, PropertyType.String, description: "The child slot of the right operand, empty for a unary operator."),
            ],
            [EvaluationRuleConceptKeys.Body],
            description: "An operator applied to the values of its operands.",
            nameSlot: OperatorSlot),

        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Choose,
            [
                new PropertySlot(ConditionSlot, PropertyType.String, description: "The child slot whose value decides."),
                new ChildSlot(ThenSlot, EvaluationRuleConceptKeys.Body, Cardinality.One),
                new ChildSlot(OtherwiseSlot, EvaluationRuleConceptKeys.Body, Cardinality.One),
            ],
            [EvaluationRuleConceptKeys.Body],
            description: "One of two bodies, decided by the truth value of a child slot's value.",
            nameSlot: ConditionSlot),
    ];
}
