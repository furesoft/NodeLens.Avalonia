using Projectional.Core;

namespace Projectional.Language;

/// <summary>
/// What a language says about text typed at the edge of a node that already exists.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of <see cref="ITypingRules"/>, which answers the same question for an empty slot. This one
/// is asked only where typing would otherwise do nothing - a slot that takes no text, or a property that
/// refuses the characters - so a trigger can never take a keystroke away from a cell that would have accepted
/// it. That is what makes typing <c>-</c> in front of <c>5</c> still produce <c>-5</c> rather than a negation.
/// </para>
/// <para>
/// An interface rather than the registry itself, for the same reason as <see cref="ITypingRules"/>: a host may
/// want to answer from somewhere else - a language loaded at run time, a plugin - and the editor should not
/// have to know which of the two it is holding.
/// </para>
/// </remarks>
public interface ITriggerRules
{
    /// <summary>Whether this source has a trigger at all.</summary>
    /// <remarks>
    /// A language that declares none is the common case, and the editor asks on every refused keystroke, so
    /// this answers without walking anything.
    /// </remarks>
    bool IsEmpty { get; }

    /// <summary>The triggers that apply to a node whose concept is <paramref name="anchor"/>.</summary>
    /// <param name="anchor">The concept of the node at the edge of the caret.</param>
    /// <returns>
    /// The triggers declared for that concept or for any of its super concepts, in the order the language
    /// declared them, so that the first one whose side and text match is the one the language meant.
    /// </returns>
    IReadOnlyList<ConceptTrigger> Find(Concept anchor);
}
