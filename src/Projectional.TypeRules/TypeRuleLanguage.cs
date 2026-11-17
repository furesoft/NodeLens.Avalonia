using Projectional.Core;
using Projectional.Language;

namespace Projectional.TypeRules;

/// <summary>
/// The language a type rule is written in: concepts whose nodes <em>are</em> a type rule.
/// </summary>
/// <remarks>
/// <para>
/// It is a language like any other - same editor, same writer, same undo - because that is what makes the
/// two authoring paths of [ADR 0014](../../../docs/adr/0014-both-authoring-paths-are-permanent.md) equal.
/// <see cref="TypeRuleLowering"/> is the C# twin of the language itself.
/// </para>
/// <para>
/// The vocabulary is deliberately three bodies rather than a statement list. MPS' typesystem is
/// imperative - <c>do { typeof(node) :== int; }</c> - and copying that would mean shipping an interpreter
/// for a language whose only job is to answer one question per node. A fixed type, a child's type and a
/// switch over a property cover what a DSL's rules actually say, and each lowers to a delegate the C#
/// path could have written by hand.
/// </para>
/// </remarks>
public static class TypeRuleLanguage
{
    /// <summary>The slot holding the rules of one language.</summary>
    public const string RulesSlot = "rules";

    /// <summary>The slot holding a rule's body.</summary>
    public const string BodySlot = "body";

    /// <summary>The slot holding a switch's cases.</summary>
    public const string CaseSlot = "cases";

    /// <summary>The slot holding a switch's fallback.</summary>
    public const string OtherwiseSlot = "otherwise";

    /// <summary>The concept a rule is about.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>The type a fixed body names.</summary>
    public const string TypeNameSlot = "typeName";

    /// <summary>The child slot a child body reads.</summary>
    public const string SlotSlot = "slot";

    /// <summary>The property a switch reads.</summary>
    public const string PropertySlot = "property";

    /// <summary>The value a case matches.</summary>
    public const string ValueSlot = "value";

    /// <summary>The concept declarations of the language.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            TypeRuleConceptKeys.Rules,
            [new ChildSlot(RulesSlot, TypeRuleConceptKeys.Rule, Cardinality.Many)],
            description: "One language's type rules."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Rule,
            [
                new PropertySlot(ConceptKeySlot, PropertyType.String),
                new ChildSlot(BodySlot, TypeRuleConceptKeys.Body, Cardinality.One),
            ],
            nameSlot: ConceptKeySlot,
            description: "What one concept evaluates to."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Body,
            [],
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "What a rule answers, as one of the three kinds below."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Fixed,
            [new PropertySlot(TypeNameSlot, PropertyType.String)],
            [TypeRuleConceptKeys.Body],
            nameSlot: TypeNameSlot,
            description: "A type named outright: string, int, bool or date."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Child,
            [new PropertySlot(SlotSlot, PropertyType.String)],
            [TypeRuleConceptKeys.Body],
            nameSlot: SlotSlot,
            description: "The type of one of the node's children."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Switch,
            [
                new PropertySlot(PropertySlot, PropertyType.String),
                new ChildSlot(CaseSlot, TypeRuleConceptKeys.Case, Cardinality.Many),
                new ChildSlot(OtherwiseSlot, TypeRuleConceptKeys.Body, Cardinality.One),
            ],
            [TypeRuleConceptKeys.Body],
            nameSlot: PropertySlot,
            description: "A choice between bodies, decided by a property's value."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Case,
            [
                new PropertySlot(ValueSlot, PropertyType.String),
                new ChildSlot(BodySlot, TypeRuleConceptKeys.Body, Cardinality.One),
            ],
            nameSlot: ValueSlot,
            description: "One value of a switch, and the body it selects."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project in which a type rule can be written.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model holding one empty rules document.</summary>
    public static Model NewModel(string name = "types") => NewProject().NewModel(name);
}
