using Projectional.Core;
using Projectional.Editing;
using Projectional.Structures;

namespace Projectional.Structures.Intentions;

/// <summary>Abstractness, as an operation rather than as a value somebody has to know the name of.</summary>
/// <remarks>
/// <para>
/// A concept's notation writes <c>abstract concept Rule</c>, so the flag has no cell of its own: the word is there
/// when the concept is abstract and gone when it is not. That is the right drawing and it leaves the value
/// unreachable by typing, which is exactly what an intention is for - the fact is still something a user changes,
/// it just cannot be spelled.
/// </para>
/// <para>
/// Two entries rather than one that toggles. A label is fixed when the intention is declared, so one entry would
/// have to say "Toggle abstract", and a toggle is a worse sentence than the outcome: what the user gets is a
/// concept that <em>is</em> abstract. Each is <see cref="IntentionAvailability.Absent"/> when it would change
/// nothing, so the list shows one line and never two.
/// </para>
/// <para>
/// The package sits beside the language rather than inside it for the reason
/// <c>Projectional.Expressions.Intentions</c> gives: an intention produces an <see cref="EditAction"/>, so it needs
/// the editing layer, and a language package that dragged the editor in could no longer be used headlessly.
/// </para>
/// </remarks>
public sealed class StructureIntentions : IIntentionProvider
{
    /// <summary>The stable id of "make abstract".</summary>
    public const string MakeAbstractId = "structures.make-abstract";

    /// <summary>The stable id of "make concrete".</summary>
    public const string MakeConcreteId = "structures.make-concrete";

    /// <summary>The stable id of "make interface".</summary>
    public const string MakeInterfaceId = "structures.make-interface";

    /// <summary>The stable id of "inherit from".</summary>
    public const string InheritFromId = "structures.inherit-from";

    /// <summary>The stable id of "implement interface".</summary>
    public const string ImplementInterfaceId = "structures.implement-interface";

    /// <summary>The stable id of "make concept".</summary>
    public const string MakeConceptId = "structures.make-concept";

    private static readonly Intention Abstract = new(
        MakeAbstractId,
        "Make abstract",
        "Makes the concept abstract, so no node can be created from it, and its notation starts with the word \"abstract\".",
        context => Action(context, "true"));

    private static readonly Intention Concrete = new(
        MakeConcreteId,
        "Make concrete",
        "Makes the concept concrete, so nodes can be created from it again and its notation no longer says \"abstract\".",
        context => Action(context, "false"));

    private static readonly Intention Interface = new(
        MakeInterfaceId,
        "Make interface",
        "Makes the concept an interface: abstract by definition, and the source of the properties its implementers inherit.",
        context => InterfaceAction(context, isInterface: true));

    private static readonly Intention Concept = new(
        MakeConceptId,
        "Make concept",
        "Makes an interface an ordinary abstract concept again, so the concepts below it stop inheriting its members.",
        context => InterfaceAction(context, isInterface: false));

    private static readonly Intention Inherit = new(
        InheritFromId,
        "Inherit from…",
        "Adds the first super concept. The caret lands on its key, ready to be typed.",
        context => Fill(context, StructureLanguage.SuperTypeSlot));

    private static readonly Intention Implement = new(
        ImplementInterfaceId,
        "Implement interface…",
        "Adds the first interface this concept implements. The caret lands on its key, ready to be typed.",
        context => Fill(context, StructureLanguage.ImplementsSlot));

    /// <summary>The provider a host registers with <c>session.AddIntentions(..., IntentionSource.Language)</c>.</summary>
    public static StructureIntentions Provider { get; } = new();

    /// <summary>Both entries, each absent where the concept already is what it would make it.</summary>
    /// <param name="context">The caret to decide against.</param>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
        [
            Abstract.With(AvailabilityOf(context, makingAbstract: true)),
            Concrete.With(AvailabilityOf(context, makingAbstract: false)),
            Interface.With(InterfaceAvailabilityOf(context, becomingInterface: true)),
            Concept.With(InterfaceAvailabilityOf(context, becomingInterface: false)),
            Inherit.With(FillAvailabilityOf(context, StructureLanguage.SuperTypeSlot)),
            Implement.With(FillAvailabilityOf(context, StructureLanguage.ImplementsSlot)),
        ];
    }

    /// <summary>
    /// Whether the caret's concept still needs the first entry of <paramref name="slot"/>.
    /// </summary>
    /// <remarks>
    /// Both entries exist for the state in which the header draws nothing: the notation hides the list while it is
    /// empty, so without a second way in the first super concept could never be written at all. Once the list is
    /// on screen it is its own affordance - the caret stands in it and Enter adds the next entry - so the entry is
    /// <see cref="IntentionAvailability.Absent"/> rather than a second way to do what the caret already does.
    /// </remarks>
    private static IntentionAvailability FillAvailabilityOf(IntentionContext context, string slot)
    {
        if (context.Node is not { } node || node.Concept.Key != StructureConceptKeys.Concept)
        {
            return IntentionAvailability.Absent;
        }

        return node.GetChildren(slot).Count > 0 ? IntentionAvailability.Absent : IntentionAvailability.Applies;
    }

    private static InsertChildAction? Fill(IntentionContext context, string slot) =>
        context.Node is { } node ? new InsertChildAction(node.Id, slot) : null;

    /// <summary>
    /// Whether the caret's concept can become an interface (or stop being one).
    /// </summary>
    /// <remarks>
    /// A concept becomes an interface and abstract in one step, because the registry refuses an interface that is
    /// concrete - an entry that produced a definition the language then refuses would be a broken repair. The other
    /// direction makes it an ordinary concrete concept, which is what a user means by undoing the interface.
    /// </remarks>
    private static IntentionAvailability InterfaceAvailabilityOf(IntentionContext context, bool becomingInterface)
    {
        if (context.Node is not { } node || node.Concept.Key != StructureConceptKeys.Concept)
        {
            return IntentionAvailability.Absent;
        }

        if (IsInterface(node) == becomingInterface)
        {
            return IntentionAvailability.Absent;
        }

        return becomingInterface && node.GetChildren(StructureLanguage.ChildrenSlot).Count > 0
            ? IntentionAvailability.Disabled(
                "An interface contributes properties and references only, so this concept's child slots would have to go first.")
            : IntentionAvailability.Applies;
    }

    private static bool IsInterface(Node node) =>
        bool.TryParse(node.GetOrNull(StructureLanguage.IsInterfaceProperty), out var value) && value;

    private static EditProgram? InterfaceAction(IntentionContext context, bool isInterface) =>
        context.Node is { } node
            ? EditProgram.Of(
                new SetNodePropertyAction(node.Id, StructureLanguage.IsInterfaceProperty, isInterface ? "true" : "false"),

                // An interface is abstract by definition, and an ordinary concept is not: leaving the flag as it
                // was would make one of the two directions produce a definition the registry refuses.
                new SetNodePropertyAction(node.Id, StructureLanguage.IsAbstractProperty, isInterface ? "true" : "false"))
            : null;

    /// <summary>
    /// Whether the caret's concept can be made abstract (or concrete) by this intention.
    /// </summary>
    /// <remarks>
    /// Only the concept node itself counts, not a slot it holds: making the concept abstract while the caret stands
    /// on one of its properties would edit a node the list is not about, and a user who asked for an intention at
    /// the property meant something about the property. The other direction - offering it everywhere inside the
    /// concept - would be a different feature with its own rule about which ancestor counts.
    /// </remarks>
    private static IntentionAvailability AvailabilityOf(IntentionContext context, bool makingAbstract)
    {
        if (context.Node is not { } node || node.Concept.Key != StructureConceptKeys.Concept)
        {
            return IntentionAvailability.Absent;
        }

        // An interface is abstract because of what it is, not because of the flag alone, so neither direction is
        // this entry's business: "make concrete" would produce a definition the registry refuses, and "make
        // abstract" would say something the interface already means.
        if (IsInterface(node))
        {
            return IntentionAvailability.Absent;
        }

        var isAbstract = bool.TryParse(node.GetOrNull(StructureLanguage.IsAbstractProperty), out var value) && value;

        return isAbstract == makingAbstract ? IntentionAvailability.Absent : IntentionAvailability.Applies;
    }

    private static SetNodePropertyAction? Action(IntentionContext context, string value) =>
        context.Node is { } node
            ? new SetNodePropertyAction(node.Id, StructureLanguage.IsAbstractProperty, value)
            : null;
}
