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
    /// <summary>The slot holding a rule's body.</summary>
    public const string BodySlot = "body";

    /// <summary>The slot holding a switch's cases.</summary>
    public const string CaseSlot = "cases";

    /// <summary>The slot holding a switch's fallback.</summary>
    public const string OtherwiseSlot = "otherwise";

    /// <summary>The concept a rule is about.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>The type a fixed body, or a record field, holds.</summary>
    public const string TypeSlot = "type";

    /// <summary>The name of a primitive type or of a record field.</summary>
    public const string NameSlot = "name";

    /// <summary>The fields of a record.</summary>
    public const string FieldsSlot = "fields";

    /// <summary>The element type of a list.</summary>
    public const string ElementSlot = "element";

    /// <summary>What an optional value is when it is there.</summary>
    public const string InnerSlot = "inner";

    /// <summary>The child slot a child body reads.</summary>
    public const string SlotSlot = "slot";

    /// <summary>The property a switch reads.</summary>
    public const string PropertySlot = "property";

    /// <summary>The value a case matches.</summary>
    public const string ValueSlot = "value";

    /// <summary>The concept declarations of the language.</summary>
    /// <remarks>
    /// A rule is a document of its own, and so is a named type: the root <em>is</em> the definition, the shape MPS
    /// stores one root per file in, so a language's types aspect is a folder of rules rather than one document that
    /// lists them.
    /// </remarks>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
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
            [new ChildSlot(TypeSlot, TypeRuleConceptKeys.TypeExpr, Cardinality.One)],
            [TypeRuleConceptKeys.Body],
            description: "A type written out, so it is the same whatever the node holds."),

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

        new ConceptDeclaration(
            TypeRuleConceptKeys.TypeExpr,
            [],
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "A type written as data, as one of the kinds below."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Primitive,
            [new PropertySlot(NameSlot, PropertyType.String)],
            [TypeRuleConceptKeys.TypeExpr],
            nameSlot: NameSlot,
            description: "A type named outright: string, int, bool or date."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Record,
            [new ChildSlot(FieldsSlot, TypeRuleConceptKeys.Field, Cardinality.Many)],
            [TypeRuleConceptKeys.TypeExpr],
            description: "A record: a fixed set of named fields."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Field,
            [
                new PropertySlot(NameSlot, PropertyType.String),
                new ChildSlot(TypeSlot, TypeRuleConceptKeys.TypeExpr, Cardinality.One),
            ],
            nameSlot: NameSlot,
            description: "One field of a record.",

            // A field is drawn as `(amount: int, currency: string)`, so its name reads like a variable.
            nameRule: NameRule.Camel),

        new ConceptDeclaration(
            TypeRuleConceptKeys.List,
            [new ChildSlot(ElementSlot, TypeRuleConceptKeys.TypeExpr, Cardinality.One)],
            [TypeRuleConceptKeys.TypeExpr],
            description: "A list: any number of values of one element type."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Optional,
            [new ChildSlot(InnerSlot, TypeRuleConceptKeys.TypeExpr, Cardinality.One)],
            [TypeRuleConceptKeys.TypeExpr],
            description: "A value that may be absent."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Ref,
            [new PropertySlot(NameSlot, PropertyType.String)],
            [TypeRuleConceptKeys.TypeExpr],
            nameSlot: NameSlot,
            description: "A type expression that names a declared type."),

        new ConceptDeclaration(
            TypeRuleConceptKeys.Named,
            [
                new PropertySlot(NameSlot, PropertyType.String),
                new ChildSlot(TypeSlot, TypeRuleConceptKeys.TypeExpr, Cardinality.One),
            ],
            nameSlot: NameSlot,
            description: "A type the language declares under a name, written as a root of the types model.",

            // A type name is what a finding quotes back (`Amount`), so it is the convention types are written in
            // everywhere: PascalCase.
            nameRule: NameRule.Pascal),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project in which a type rule can be written.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model holding one empty rule, ready to be written.</summary>
    /// <remarks>
    /// A rule is the only thing a types document can hold, so the helper creates it: a language with two kinds of
    /// definition (the structure language's concept and enumeration) leaves that choice to the caller.
    /// </remarks>
    public static Model NewModel(string name = "rule")
    {
        var model = NewProject().NewModel(name);

        model.NewNode(TypeRuleConceptKeys.Rule);

        return model;
    }
}
