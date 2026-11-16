namespace Projectional.Editing;

/// <summary>Which offers exist at the caret, asked fresh every time because nothing about them is stored.</summary>
/// <remarks>
/// <para>
/// One list, three sources, one order: the host's intentions, then the language's, then the quick fixes that
/// belong to the caret's line. The order is fixed here rather than left to registration, because it is what
/// the user sees - the same argument <see cref="EditorMenu"/> makes about its own entries.
/// </para>
/// <para>
/// Nothing is cached. An offer describes the caret, the layout and the model of the moment it was asked, and
/// an edit, an undo, a variant switch or a change the session did not make all invalidate it; a stale list of
/// "what you could do here" is worse than none, because it names a place the user has left. What makes
/// deriving it affordable is that it is one caret position's worth of work over providers that are expected
/// to be cheap predicates.
/// </para>
/// <para>
/// Two rules filter the answer. An intention marked <see cref="IntentionAvailability.IsAbsent"/> is dropped,
/// because it said it is not about this caret. Ids are de-duplicated across all three sources, first one wins,
/// which is what lets a language and a check describe one operation once: the catalogue deliberately gives
/// the standard repair and the standard intention the same id so the user does not read the same sentence
/// twice.
/// </para>
/// </remarks>
public static class IntentionList
{
    /// <summary>The offers at the caret of <paramref name="session"/>.</summary>
    /// <param name="session">The document to ask.</param>
    /// <remarks>
    /// A disabled intention is kept, because the user learns that the concept knows about this caret and why
    /// it cannot act - and a bulb that shows for it is the difference between "nothing here" and "here, but
    /// not now".
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<Intention> For(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var context = session.CreateIntentionContext();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var offers = new List<Intention>();

        foreach (var provider in session.IntentionProviders)
        {
            foreach (var intention in provider.At(context))
            {
                if (intention.IsAbsent || !seen.Add(intention.Id))
                {
                    continue;
                }

                offers.Add(intention);
            }
        }

        foreach (var fix in QuickFixes.At(session))
        {
            var intention = Intention.FromFix(fix);

            if (seen.Add(intention.Id))
            {
                offers.Add(intention);
            }
        }

        return offers;
    }
}
