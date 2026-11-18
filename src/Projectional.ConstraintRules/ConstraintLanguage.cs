using Projectional.Core;
using Projectional.Language;

namespace Projectional.ConstraintRules;

/// <summary>
/// The language a language's own constraints are written in: concepts whose nodes <em>are</em> a rule.
/// </summary>
/// <remarks>
/// <para>
/// A language like any other - same editor, same writer, same undo - because that is what makes the two authoring
/// paths of [ADR 0014](../../../docs/adr/0014-both-authoring-paths-are-permanent.md) equal. The C# twin is
/// <c>IConstraint</c> in <c>Projectional.Semantics</c>, and <see cref="ConstraintLowering"/> is what turns a rule
/// written here into one.
/// </para>
/// <para>
/// <b>A rule about every node is a rule with no concept key.</b> The seam exists for rules like "this name no longer
/// identifies one element", which are facts about references in general rather than about one concept, and the
/// declared form has to be able to say it - declaring the same rule once per concept is the mistake
/// <see cref="Projectional.Semantics.ConstraintSet.Everywhere"/> exists to prevent.
/// </para>
/// </remarks>
public static class ConstraintLanguage
{
    /// <summary>The slot naming the concept a rule is about, empty for every node.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>The sentence a rule reports.</summary>
    public const string MessageSlot = "message";

    /// <summary>How much the rule's finding matters.</summary>
    public const string SeveritySlot = "severity";

    /// <summary>The identifier a host groups findings by, or empty for none.</summary>
    public const string CodeSlot = "code";

    /// <summary>The slot of the node a body reads.</summary>
    public const string TargetSlot = "slot";

    /// <summary>The single child that says what a rule checks.</summary>
    public const string BodySlot = "body";

    /// <summary>The concept declarations of the language.</summary>
    /// <remarks>
    /// The body vocabulary is closed and small for the same reason the template language's is: a body asks the node
    /// one question. There is deliberately no "expression" body, because evaluating one would mean shipping an
    /// interpreter for a language whose only job is to say what is wrong.
    /// </remarks>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            ConstraintConceptKeys.Rule,
            [
                new PropertySlot(
                    ConceptKeySlot,
                    PropertyType.String,
                    description: "The concept this rule is about, or empty for every node."),
                new PropertySlot(
                    MessageSlot,
                    PropertyType.String,
                    description: "What is wrong, in one sentence the user can act on."),
                new PropertySlot(
                    SeveritySlot,
                    PropertyType.Enum(ConstraintSeverities.Error, ConstraintSeverities.Warning, ConstraintSeverities.Hint),
                    ConstraintSeverities.Error,
                    description: "How much it matters: error, warning or hint."),
                new PropertySlot(
                    CodeSlot,
                    PropertyType.String,
                    description: "The identifier a host groups findings by, such as rules.condition.required."),
                new ChildSlot(BodySlot, ConstraintConceptKeys.Body, Cardinality.One),
            ],
            nameSlot: MessageSlot,
            description: "One rule the language states about a node, or about every node."),

        new ConceptDeclaration(
            ConstraintConceptKeys.Body,
            [
                new PropertySlot(
                    TargetSlot,
                    PropertyType.String,
                    description: "The slot of the node this rule reads."),
            ],
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "What a rule checks, as one of the kinds below."),

        new ConceptDeclaration(
            ConstraintConceptKeys.EmptySlot,
            [],
            [ConstraintConceptKeys.Body],
            description: "A single-child slot that holds nothing."),

        new ConceptDeclaration(
            ConstraintConceptKeys.DefaultValue,
            [],
            [ConstraintConceptKeys.Body],
            description: "A property whose value is still the one its concept declares."),

        new ConceptDeclaration(
            ConstraintConceptKeys.SurroundingWhitespace,
            [],
            [ConstraintConceptKeys.Body],
            description: "A value with whitespace in front of or behind it."),

        new ConceptDeclaration(
            ConstraintConceptKeys.EmptyReference,
            [],
            [ConstraintConceptKeys.Body],
            description: "A reference that was never set or whose target is gone."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project in which a rule can be written.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model holding one empty rule, ready to be written.</summary>
    /// <param name="name">The model's name, which is also the file name it is saved under.</param>
    public static Model NewModel(string name = "constraint")
    {
        var model = NewProject().NewModel(name);

        model.NewNode(ConstraintConceptKeys.Rule);

        return model;
    }
}
