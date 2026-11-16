namespace Projectional.Language;

/// <summary>
/// What a language says about text typed at the edge of a node that already exists: put that node inside a
/// new concept of mine.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="TypingRule"/> answers "which concept did the user mean" for an <em>empty</em> slot; a trigger
/// answers the neighbouring question, "what did the user mean by typing at the edge of something that is
/// already there". Typing <c>+</c> to the right of <c>5</c> is not an attempt to write <c>5+</c> - no number
/// contains that - it is a user saying "I want an addition, with the five I just typed on its left". The
/// language says which concept that is, so the editor does not have to guess from the shape of a tree.
/// </para>
/// <para>
/// It lives beside the notation, like <see cref="TypingRule"/> and <see cref="ICellEmphasis"/>, because it is
/// a statement about how this language is written. The editor asks it only where typing would otherwise do
/// nothing, so a trigger can never take a character away from a property that would have accepted it: typing
/// <c>-</c> in front of <c>5</c> still writes <c>-5</c>, because a negative number is a number.
/// </para>
/// </remarks>
public sealed class ConceptTrigger
{
    private readonly Func<string, bool> _claims;

    /// <summary>Declares that text matching <paramref name="claims"/> typed on <paramref name="side"/> of a
    /// <paramref name="anchorConceptKey"/> becomes a <paramref name="conceptKey"/> around it.</summary>
    /// <param name="conceptKey">The concept to create around the node under the caret.</param>
    /// <param name="anchorConceptKey">The concept, or one of its super concepts, the node must have.</param>
    /// <param name="side">Which end of that node the caret has to stand at.</param>
    /// <param name="claims">Whether a burst of typed text belongs to this trigger.</param>
    /// <param name="anchorSlot">
    /// The single-child slot of <paramref name="conceptKey"/> that receives the node.
    /// </param>
    /// <param name="propertySlot">
    /// The property of <paramref name="conceptKey"/> the typed text is written into, or <see langword="null"/>
    /// when the text only selects the trigger.
    /// </param>
    /// <param name="caretSlot">
    /// The slot of <paramref name="conceptKey"/> the caret continues in, or <see langword="null"/> when it
    /// stays where the node it was at went.
    /// </param>
    /// <exception cref="ArgumentException">A key is empty, or <paramref name="side"/> is not a side.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="claims"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The typed text is kept only if the property it names accepts it, so a trigger that claims more than its
    /// wrapper can store still creates the wrapper and merely drops the text - the same rule a typing rule
    /// follows, and for the same reason: a keystroke must not be able to throw.
    /// </para>
    /// <para>
    /// <paramref name="caretSlot"/> is where the user continues, so a trigger names an empty one: <c>5</c>
    /// then <c>+</c> leaves the caret in the right operand, ready for the next number. Without one the caret
    /// stays inside the node that was wrapped, which is what a prefix operator wants - typing <c>!</c> in
    /// front of a condition must not move the caret away from the condition.
    /// </para>
    /// </remarks>
    public ConceptTrigger(
        string conceptKey,
        string anchorConceptKey,
        TriggerSide side,
        Func<string, bool> claims,
        string anchorSlot,
        string? propertySlot = null,
        string? caretSlot = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
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

        ConceptKey = conceptKey;
        AnchorConcept = anchorConceptKey;
        Side = side;
        AnchorSlot = anchorSlot;
        PropertySlot = propertySlot;
        CaretSlot = caretSlot;
        _claims = claims;
    }

    /// <summary>The concept to create around the node under the caret.</summary>
    public string ConceptKey { get; }

    /// <summary>The concept the node under the caret must be, or be a kind of.</summary>
    public string AnchorConcept { get; }

    /// <summary>Which end of that node the caret has to stand at.</summary>
    public TriggerSide Side { get; }

    /// <summary>The single-child slot of <see cref="ConceptKey"/> that receives the node.</summary>
    public string AnchorSlot { get; }

    /// <summary>
    /// The property of <see cref="ConceptKey"/> the typed text belongs in, or <see langword="null"/> when the
    /// text only selects the trigger.
    /// </summary>
    public string? PropertySlot { get; }

    /// <summary>
    /// The slot of <see cref="ConceptKey"/> the caret continues in, or <see langword="null"/> when it stays
    /// where the node it was at went.
    /// </summary>
    public string? CaretSlot { get; }

    /// <summary>Whether a burst of typed text belongs to this trigger.</summary>
    /// <param name="text">The characters the host typed.</param>
    public bool Claims(string text) => _claims(text);

    public override string ToString() =>
        $"{ConceptKey} <- {AnchorConcept} ({Side.ToString().ToLowerInvariant()})";
}
