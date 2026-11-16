namespace Projectional.Editing;

/// <summary>One source of intentions: a language aspect now, a host registration always.</summary>
/// <remarks>
/// <para>
/// A provider answers for one caret, so it is asked on every list derivation - the same "derived per call,
/// never cached" rule <see cref="QuickFixes"/> and <see cref="EditorMenu"/> follow. That is affordable only
/// because a provider is expected to be a cheap predicate over the declarations: a provider that needs scope
/// resolution or type inference is a provider that belongs in validation, not in a paint.
/// </para>
/// <para>
/// The context it receives is read-only, so a provider cannot change the model while being asked what it
/// offers. It may leave out the intentions that do not apply, and it may also return them marked
/// <see cref="IntentionAvailability.Absent"/> - the second shape exists so that a declared catalogue can be
/// handed back whole, which is what a language aspect does when it declares its entries once.
/// </para>
/// </remarks>
public interface IIntentionProvider
{
    /// <summary>What this source offers at the caret <paramref name="context"/> describes.</summary>
    /// <param name="context">The model, tree, layout and caret to decide against.</param>
    /// <returns>
    /// The intentions for this caret, in the order this source wants them listed; empty when it has nothing
    /// to say here.
    /// </returns>
    IReadOnlyList<Intention> At(IntentionContext context);
}
