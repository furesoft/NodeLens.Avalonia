namespace Projectional.Core;

/// <summary>
/// What a concept says about text typed at the edge of a node that already exists: put that node inside me.
/// </summary>
/// <remarks>
/// <para>
/// Declared <em>on the concept it creates</em> rather than in a registry beside the language, for two reasons.
/// Locality: every slot it names - the one that receives the node, the property the text goes into, the slot the
/// caret continues in - is a slot of this very concept, so a rename cannot leave the trigger describing
/// something that no longer exists elsewhere in the file. Reuse: a language borrows another's concepts with
/// <c>[.. TheirLanguage.ConceptDeclarations, .. own]</c>, and a trigger declared here travels with the concept
/// while a registry entry does not - the host would have to remember to forward it, and forgetting is silent.
/// </para>
/// <para>
/// The concept key is therefore absent: a trigger always creates the concept that declares it.
/// </para>
/// <para>
/// A <see cref="TypingRule"/> answers the neighbouring question - which concept did the user mean in an
/// <em>empty</em> slot - and lives in the language because it is about a concept that does not exist yet.
/// </para>
/// </remarks>
public sealed class ConceptTrigger
{
    private readonly Func<string, bool> _claims;

    /// <summary>Declares that text matching <paramref name="claims"/> typed on <paramref name="side"/> of a
    /// <paramref name="anchorConceptKey"/> becomes this concept around it.</summary>
    /// <param name="anchorConceptKey">The concept, or one of its super concepts, the node must have.</param>
    /// <param name="side">Which end of that node the caret has to stand at.</param>
    /// <param name="claims">Whether a burst of typed text belongs to this trigger.</param>
    /// <param name="anchorSlot">The single-child slot of this concept that receives the node.</param>
    /// <param name="propertySlot">
    /// The property of this concept the typed text is written into, or <see langword="null"/> when the text only
    /// selects the trigger.
    /// </param>
    /// <param name="caretSlot">
    /// The slot of this concept the caret continues in, or <see langword="null"/> when it stays where the node it
    /// was at went.
    /// </param>
    /// <exception cref="ArgumentException">A key is empty, or <paramref name="side"/> is not a side.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="claims"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The typed text is kept only if the property it names accepts it, so a trigger that claims more than its
    /// concept can store still creates the concept and merely drops the text - the same rule a typing rule
    /// follows, and for the same reason: a keystroke must not be able to throw.
    /// </remarks>
    public ConceptTrigger(
        string anchorConceptKey,
        TriggerSide side,
        Func<string, bool> claims,
        string anchorSlot,
        string? propertySlot = null,
        string? caretSlot = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(anchorConceptKey);
        ArgumentException.ThrowIfNullOrEmpty(anchorSlot);
        ArgumentNullException.ThrowIfNull(claims);

        if (propertySlot is not null)
        {
            ArgumentException.ThrowIfNullOrEmpty(propertySlot);
        }

        if (caretSlot is not null)
        {
            ArgumentException.ThrowIfNullOrEmpty(caretSlot);
        }

        if (!Enum.IsDefined(side))
        {
            throw new ArgumentException($"'{side}' is not a trigger side.", nameof(side));
        }

        AnchorConcept = anchorConceptKey;
        Side = side;
        AnchorSlot = anchorSlot;
        PropertySlot = propertySlot;
        CaretSlot = caretSlot;
        _claims = claims;
    }

    /// <summary>The concept the node under the caret must be, or be a kind of.</summary>
    public string AnchorConcept { get; }

    /// <summary>Which end of that node the caret has to stand at.</summary>
    public TriggerSide Side { get; }

    /// <summary>The single-child slot of the declaring concept that receives the node.</summary>
    public string AnchorSlot { get; }

    /// <summary>
    /// The property of the declaring concept the typed text belongs in, or <see langword="null"/> when the text
    /// only selects the trigger.
    /// </summary>
    public string? PropertySlot { get; }

    /// <summary>
    /// The slot of the declaring concept the caret continues in, or <see langword="null"/> when it stays where
    /// the node it was at went.
    /// </summary>
    public string? CaretSlot { get; }

    /// <summary>Whether a burst of typed text belongs to this trigger.</summary>
    /// <param name="text">The characters the host typed.</param>
    public bool Claims(string text) => _claims(text);

    public override string ToString() =>
        $"<- {AnchorConcept} ({Side.ToString().ToLowerInvariant()})";
}
