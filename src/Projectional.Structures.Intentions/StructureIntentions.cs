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
        ];
    }

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

        var isAbstract = bool.TryParse(node.GetOrNull(StructureLanguage.IsAbstractProperty), out var value) && value;

        return isAbstract == makingAbstract ? IntentionAvailability.Absent : IntentionAvailability.Applies;
    }

    private static SetNodePropertyAction? Action(IntentionContext context, string value) =>
        context.Node is { } node
            ? new SetNodePropertyAction(node.Id, StructureLanguage.IsAbstractProperty, value)
            : null;
}
