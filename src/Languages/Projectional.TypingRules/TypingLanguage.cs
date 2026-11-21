using Projectional.Core;
using Projectional.Language;

namespace Projectional.TypingRules;

/// <summary>The language a language's own typing rules are written in: concepts whose nodes <em>are</em> a rule.</summary>
/// <remarks>
/// <para>
/// A language like any other - same editor, same writer, same undo - because that is what makes the two authoring
/// paths of [ADR 0014](../../../docs/adr/0014-both-authoring-paths-are-permanent.md) equal. The C# twin is
/// <see cref="TypingRuleRegistry"/> in <c>Projectional.Language</c>, and <see cref="TypingLowering"/> is what turns a
/// rule written here into a <see cref="TypingRule"/>.
/// </para>
/// <para>
/// <b>A rule names a concept and a property, not a cell.</b> What the user types is written into the concept's
/// property whichever notation draws it - in a line, in a table or not at all - which is why the slot is named here
/// and the cell is not.
/// </para>
/// </remarks>
public static class TypingLanguage
{
    /// <summary>The slot naming the concept the typed text creates.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>The slot saying how the text is recognised.</summary>
    public const string ClaimsSlot = "claims";

    /// <summary>The slot holding the prefix or the words <see cref="ClaimsSlot"/> recognises the text by.</summary>
    public const string TextSlot = "text";

    /// <summary>The slot naming the property the text is written into, empty for a concept that has none.</summary>
    public const string PropertySlotSlot = "propertySlot";

    /// <summary>The concept declarations of the language.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            TypingConceptKeys.Rule,
            [
                new PropertySlot(
                    ConceptKeySlot,
                    PropertyType.String,
                    description: "The concept the typed text creates."),
                new PropertySlot(
                    ClaimsSlot,
                    PropertyType.Enum(TypingClaims.Digits, TypingClaims.Name, TypingClaims.Prefix, TypingClaims.AnyOf, TypingClaims.Alias),
                    TypingClaims.Digits,
                    "How the text is recognised.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    TextSlot,
                    PropertyType.String,
                    description: "The prefix, or the words separated by spaces; empty for digits and name."),
                new PropertySlot(
                    PropertySlotSlot,
                    PropertyType.String,
                    description: "The property the text is written into, or empty for a concept with no property."),
            ],
            description: "Which node a burst of typing creates."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project in which a typing rule can be written.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model holding one empty rule, ready to be written.</summary>
    /// <param name="name">The model's name, which is also the file name it is saved under.</param>
    public static Model NewModel(string name = "typing")
    {
        var model = NewProject().NewModel(name);

        model.NewNode(TypingConceptKeys.Rule);

        return model;
    }
}
