using Projectional.Core;
using Projectional.Language;

namespace Projectional.ScopeRules;

/// <summary>The language a language's own scopes are written in: concepts whose nodes <em>are</em> a scope rule.</summary>
/// <remarks>
/// <para>
/// A language like any other - same editor, same writer, same undo - because that is what makes the two authoring
/// paths of [ADR 0014](../../../docs/adr/0014-both-authoring-paths-are-permanent.md) equal. The C# twin is
/// <see cref="Projectional.Semantics.NamedScopeProvider"/> in <c>Projectional.Semantics</c>, and
/// <see cref="ScopeLowering"/> is what turns a rule written here into one.
/// </para>
/// <para>
/// <b>A rule names a reference, not a node.</b> A scope is a fact about the model: the same reference slot has the
/// same scope whether it is drawn in a line, in a table or not at all, which is why the slot is a property and the
/// concept is named rather than the cell.
/// </para>
/// </remarks>
public static class ScopeLanguage
{
    /// <summary>The slot naming the concept whose reference this rule answers for.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>The slot naming that concept's reference slot.</summary>
    public const string SlotSlot = "slot";

    /// <summary>The slot naming where that reference's names are written.</summary>
    public const string PlaceSlot = "place";

    /// <summary>The slot naming the concept the visible nodes must be, empty for any named node.</summary>
    public const string TargetSlot = "target";

    /// <summary>The concept declarations of the language.</summary>
    /// <remarks>
    /// The body is the <see cref="PlaceSlot"/> property rather than a child concept: the five places are the whole
    /// vocabulary, and a node per place would be five concepts that each hold nothing.
    /// </remarks>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            ScopeConceptKeys.Rule,
            [
                new PropertySlot(
                    ConceptKeySlot,
                    PropertyType.String,
                    description: "The concept whose reference this rule answers for."),
                new PropertySlot(
                    SlotSlot,
                    PropertyType.String,
                    description: "The reference slot on that concept."),
                new PropertySlot(
                    PlaceSlot,
                    PropertyType.Enum(ScopePlaces.Local, ScopePlaces.Ancestors, ScopePlaces.Model, ScopePlaces.Project, ScopePlaces.Everywhere),
                    ScopePlaces.Everywhere,
                    "Where the names are written.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    TargetSlot,
                    PropertyType.String,
                    description: "The concept the visible nodes must be, or empty for any named node."),
            ],
            description: "Where one reference's names come from."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project in which a scope rule can be written.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model holding one empty rule, ready to be written.</summary>
    /// <param name="name">The model's name, which is also the file name it is saved under.</param>
    public static Model NewModel(string name = "scope")
    {
        var model = NewProject().NewModel(name);

        model.NewNode(ScopeConceptKeys.Rule);

        return model;
    }
}
