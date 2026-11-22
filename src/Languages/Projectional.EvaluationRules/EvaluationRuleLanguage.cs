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
    /// <summary>The language name a project and a documentation set call it by.</summary>
    /// <remarks>
    /// <c>EvaluationRules</c> rather than <c>Evaluation</c>: every language also has an <em>evaluation page</em> that says what
    /// its own concepts mean, and one word for both would make the tree read as if the aspect and the language were the same
    /// thing.
    /// </remarks>
    public const string Name = "EvaluationRules";

    /// <summary>The property naming the concept a rule answers for, such as <c>Expressions.Binary</c>.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>The slot holding a rule's body.</summary>
    public const string BodySlot = "body";

    /// <summary>The property naming a slot of the node a body reads.</summary>
    public const string SlotSlot = "slot";

    /// <summary>The symbol an operator body applies itself, which wins over the one its node holds.</summary>
    public const string SymbolSlot = "symbol";

    /// <summary>The property naming the property that holds the symbol, for a body that names none itself.</summary>
    public const string OperatorSlot = "operatorSlot";

    /// <summary>The body that answers the left operand, or the only one.</summary>
    public const string LeftSlot = "left";

    /// <summary>The body that answers the right operand, empty for a unary operator.</summary>
    public const string RightSlot = "right";

    /// <summary>The body whose value decides a choose body.</summary>
    public const string ConditionSlot = "condition";

    /// <summary>The body a choose body answers with while its condition is true.</summary>
    public const string ThenSlot = "then";

    /// <summary>The body a choose body answers with otherwise.</summary>
    public const string OtherwiseSlot = "otherwise";

    /// <summary>The property naming the child slot whose entries a first body walks.</summary>
    public const string OverSlot = "over";

    /// <summary>The body a first body asks about each entry, answered for that entry.</summary>
    public const string WhenSlot = "when";

    /// <summary>The property holding the name a bind body introduces.</summary>
    public const string NameSlot = "name";

    /// <summary>The body whose value a bind body binds.</summary>
    public const string ValueSlot = "value";

    /// <summary>The property holding the sentence a fail body refuses with.</summary>
    public const string MessageSlot = "message";

    /// <summary>The values a constant may hold, in the order the property offers them.</summary>
    public static IReadOnlyList<string> ConstantKinds { get; } = ["int", "bool", "string"];

    /// <summary>The type of the property that says which kind of value a constant holds.</summary>
    public static PropertyType ConstantKindType { get; } = PropertyType.Enum("int", "bool", "string");

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
            description: "What a rule answers with, as one of the bodies below."),

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

        // The two bodies that ask about the node's shape rather than answering with a value of it. Without them a rule
        // cannot guard a slot that may be empty - `choose` has no way to ask - and cannot say how much a node holds.
        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Has,
            [new PropertySlot(SlotSlot, PropertyType.String)],
            [EvaluationRuleConceptKeys.Body],
            description: "Whether a slot of the node holds anything: a value in a property, an entry in a child slot, or a target in a reference.",
            nameSlot: SlotSlot),

        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Count,
            [new PropertySlot(SlotSlot, PropertyType.String)],
            [EvaluationRuleConceptKeys.Body],
            description: "How many entries a child slot holds.",
            nameSlot: SlotSlot),

        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Variable,
            [new PropertySlot(SlotSlot, PropertyType.String)],
            [EvaluationRuleConceptKeys.Body],
            description: "The value the run bound to the name a property holds.",
            nameSlot: SlotSlot),

        // Written in the rule rather than read from the node, which is what a rule that answers with an outcome needs: the
        // alternative was a node whose property happens to hold the same text.
        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Constant,
            [
                new PropertySlot(
                    "kind",
                    ConstantKindType,
                    description: "Which kind of value the text holds, such as int.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(ValueSlot, PropertyType.String, description: "The value as text, such as 42."),
            ],
            [EvaluationRuleConceptKeys.Body],
            description: "A value written into the rule itself: the outcome of a decision, or a number the rule needs.",
            nameSlot: ValueSlot),

        // The operands are bodies rather than slot names, which is what lets a rule say `(the first applicable rule's
        // outcome) + 1`: an operand that can only name a child slot would stop at the node's own shape.
        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Operator,
            [
                new PropertySlot(
                    SymbolSlot,
                    PropertyType.String,
                    description: "The symbol this body applies, such as >=. Left empty, the rule reads the one the node holds in the property named below."),
                new PropertySlot(OperatorSlot, PropertyType.String, description: "The property holding the symbol, for a rule whose operator varies with the node."),
                new ChildSlot(LeftSlot, EvaluationRuleConceptKeys.Body, Cardinality.One),
                new ChildSlot(RightSlot, EvaluationRuleConceptKeys.Body, Cardinality.One, minimum: 0),
            ],
            [EvaluationRuleConceptKeys.Body],
            description: "An operator applied to the values of its operands: leave the right operand empty for a unary operator, and the rule applies the symbol to the left one alone.",
            nameSlot: OperatorSlot),

        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Choose,
            [
                new ChildSlot(ConditionSlot, EvaluationRuleConceptKeys.Body, Cardinality.One),
                new ChildSlot(ThenSlot, EvaluationRuleConceptKeys.Body, Cardinality.One),
                new ChildSlot(OtherwiseSlot, EvaluationRuleConceptKeys.Body, Cardinality.One),
            ],
            [EvaluationRuleConceptKeys.Body],
            // No name slot: a choice has no string of its own, and a name slot has to be a string property - the condition
            // is a body, which is a child.
            description: "One of two bodies, decided by the truth value of a third."),

        // The shape every rule-based DSL needs: the first child that applies decides, and what it answers is read from
        // *that* child. Both bodies are evaluated with the entry as their subject, which is what a context for another node
        // is for.
        new ConceptDeclaration(
            EvaluationRuleConceptKeys.First,
            [
                new PropertySlot(OverSlot, PropertyType.String, description: "The child slot whose entries are tried in order."),
                new ChildSlot(WhenSlot, EvaluationRuleConceptKeys.Body, Cardinality.One),
                new ChildSlot(ThenSlot, EvaluationRuleConceptKeys.Body, Cardinality.One),
                new ChildSlot(OtherwiseSlot, EvaluationRuleConceptKeys.Body, Cardinality.One, minimum: 0),
            ],
            [EvaluationRuleConceptKeys.Body],
            description: "The first entry of a list whose condition holds, answered by the body beside the condition; with no otherwise body an entry that never applies is a refusal.",
            nameSlot: OverSlot),

        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Bind,
            [
                new PropertySlot(NameSlot, PropertyType.String, description: "The name the body may read, such as total."),
                new ChildSlot(ValueSlot, EvaluationRuleConceptKeys.Body, Cardinality.One),
                new ChildSlot(BodySlot, EvaluationRuleConceptKeys.Body, Cardinality.One),
            ],
            [EvaluationRuleConceptKeys.Body],
            description: "A name bound to one body while another one answers: the once-only value a rule would otherwise compute twice.",
            nameSlot: NameSlot),

        new ConceptDeclaration(
            EvaluationRuleConceptKeys.Fail,
            [new PropertySlot(MessageSlot, PropertyType.String, description: "What is wrong, in the words the user sees.")],
            [EvaluationRuleConceptKeys.Body],
            description: "A refusal with a sentence, for the case the rule can recognise and has no value for.",
            nameSlot: MessageSlot),
    ];
}
