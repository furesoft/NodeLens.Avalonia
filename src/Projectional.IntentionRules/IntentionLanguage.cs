using Projectional.Core;
using Projectional.Language;

namespace Projectional.IntentionRules;

/// <summary>The language a language's own intentions are written in: concepts whose nodes are an entry.</summary>
/// <remarks>
/// <para>
/// A language like any other - same editor, same writer, same undo - because that is what makes the two authoring
/// paths of [ADR 0014](../../../docs/adr/0014-both-authoring-paths-are-permanent.md) equal. The C# twin is
/// <see cref="Projectional.Editing.Intention"/>, and <see cref="IntentionLowering"/> is what turns a rule written
/// here into one.
/// </para>
/// <para>
/// What a rule says is what the framework's own catalogue cannot derive from a concept's shape: a value a wrapper is
/// <em>born with</em>. That is the entry "Negate" exists for in <c>Projectional.Expressions.Intentions</c>, and the
/// reason a language needs to say it at all.
/// </para>
/// </remarks>
public static class IntentionLanguage
{
    /// <summary>The concept the entry is offered on; empty means any node.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>What the list shows.</summary>
    public const string LabelSlot = "label";

    /// <summary>The sentence that explains the entry.</summary>
    public const string DescriptionSlot = "description";

    /// <summary>The single child that says what the intention does.</summary>
    public const string ActionSlot = "action";

    /// <summary>The concept that surrounds the node.</summary>
    public const string WrapperSlot = "wrapper";

    /// <summary>The wrapper's single-child slot that receives the node.</summary>
    public const string WrapperChildSlot = "wraps";

    /// <summary>One value the wrapper is born with.</summary>
    public const string PresetSlot = "presets";

    /// <summary>A preset's property name.</summary>
    public const string PresetPropertySlot = "slot";

    /// <summary>A preset's value.</summary>
    public const string PresetValueSlot = "value";

    /// <summary>The side a declared insert puts the new sibling on.</summary>
    public const string SideSlot = "side";

    /// <summary>The property an intention writes on the node.</summary>
    public const string SetSlot = "slot";

    /// <summary>The value an intention writes.</summary>
    public const string SetValueSlot = "value";

    /// <summary>The concept declarations of the language.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            IntentionConceptKeys.Rule,
            [
                new PropertySlot(
                    ConceptKeySlot,
                    PropertyType.String,
                    description: "The concept this entry is offered on, or empty for any node."),
                new PropertySlot(LabelSlot, PropertyType.String, description: "What the list shows."),
                new PropertySlot(DescriptionSlot, PropertyType.String, description: "The sentence that explains it."),
                new ChildSlot(ActionSlot, IntentionConceptKeys.Action, Cardinality.One),
            ],
            nameSlot: LabelSlot,
            description: "One intention a language declares."),

        new ConceptDeclaration(
            IntentionConceptKeys.Action,
            [],
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "What an intention does, as one of the kinds below."),

        new ConceptDeclaration(
            IntentionConceptKeys.Wrap,
            [
                new PropertySlot(WrapperSlot, PropertyType.String, description: "The concept that surrounds the node."),
                new PropertySlot(WrapperChildSlot, PropertyType.String, description: "The wrapper's single-child slot that receives the node."),
                new ChildSlot(PresetSlot, IntentionConceptKeys.Preset, Cardinality.Many),
            ],
            [IntentionConceptKeys.Action],
            description: "Surrounds the node with a wrapper, born with the values listed."),

        new ConceptDeclaration(
            IntentionConceptKeys.Preset,
            [
                new PropertySlot(PresetPropertySlot, PropertyType.String, description: "A property of the wrapper."),
                new PropertySlot(PresetValueSlot, PropertyType.String, description: "The value it is born with."),
            ],
            description: "One value the wrapper starts with."),

        new ConceptDeclaration(
            IntentionConceptKeys.InsertSibling,
            [
                new PropertySlot(
                    SideSlot,
                    PropertyType.Enum(IntentionSiblings.Before, IntentionSiblings.After),
                    IntentionSiblings.After,
                    "Which side of the node the new sibling goes on.",
                    showDescriptionWhenFilled: true),
            ],
            [IntentionConceptKeys.Action],
            description: "Inserts a sibling of the node."),

        new ConceptDeclaration(
            IntentionConceptKeys.Set,
            [
                new PropertySlot(SetSlot, PropertyType.String, description: "The property of the node to write."),
                new PropertySlot(SetValueSlot, PropertyType.String, description: "The value to write."),
            ],
            [IntentionConceptKeys.Action],
            description: "Writes a property value on the node."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project in which an intention can be written.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model holding one rule with an empty <c>wrap</c> body, ready to be written.</summary>
    /// <param name="name">The model's name, which is also the file name it is saved under.</param>
    public static Model NewModel(string name = "intention")
    {
        var model = NewProject().NewModel(name);
        var rule = model.NewNode(IntentionConceptKeys.Rule);

        model.NewChild(rule, ActionSlot, IntentionConceptKeys.Wrap);

        return model;
    }
}
