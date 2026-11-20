using Projectional.Core;
using Projectional.Editing;
using Projectional.Languages;
using Projectional.Projection;

namespace Projectional.Testing;

/// <summary>Replays the steps of an editor test against a session, then compares what the document draws.</summary>
/// <remarks>
/// <para>
/// This is the one assertion that reaches the editing layer, and it reaches it the way a host does: an
/// <see cref="EditorSession"/> over the document, the language's own intentions registered on it, and then the two
/// functions the layer already publishes for a keystroke - <see cref="KeyRouter.Type(EditorSession, string)"/> and
/// <see cref="KeyRouter.For(EditorSession, EditorKey)"/>. Nothing here re-implements input, so a test cannot pass by
/// routing a key differently from the control.
/// </para>
/// <para>
/// A place is named by what the document shows there, found through <see cref="Finder"/>: a test that had to spell a
/// node id would be unreadable and would break the moment a fixture was regenerated, and "put the caret in front of
/// this text" is the same question a user's find asks. A caret step therefore needs the text to appear exactly once,
/// which is a property of the fixture rather than of the runner.
/// </para>
/// <para>
/// A step that the editor refuses is a failed row naming the step and the reason, because a script that could not be
/// replayed has not compared anything - the drawing afterwards would be the drawing of a different script. That is
/// the same reason a drawing that differs carries both texts: the report has to say what happened.
/// </para>
/// </remarks>
public static class ScriptRunner
{
    /// <summary>Replays <paramref name="test"/>'s steps and compares the drawing afterwards.</summary>
    /// <param name="language">The language of the document, which supplies the notation and the intentions.</param>
    /// <param name="test">The script assertion, with its steps and its expected lines.</param>
    /// <param name="root">The document to edit, which is the model's single root.</param>
    /// <returns>The row: held, or failed with the step that refused and why.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static TestResult Run(LanguageProject language, LanguageTest test, Node root)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(root);

        EditorSession session;

        try
        {
            session = new EditorSession(root, language.Notations);
        }
        catch (EditingException refused)
        {
            // A document nothing can be typed into cannot be scripted: it is the same answer a drawing of it gives.
            return TestResult.Failed(
                test.Test,
                test.Document,
                test.AssertionName,
                expected: null,
                refused.Message);
        }

        using (session)
        {
            session.AddIntentions(language.Intentions);

            foreach (var step in test.Steps ?? [])
            {
                if (Refusal(session, step) is { } refusal)
                {
                    return TestResult.Failed(
                        test.Test,
                        test.Document,
                        test.AssertionName,
                        step.ToString(),
                        refusal);
                }
            }

            var expected = string.Join('\n', test.Lines ?? []);
            var actual = LayoutSnapshot.Render(root.Model, language.Notations, test.Width);

            return expected == actual
                ? TestResult.Passed(test.Test, test.Document, test.AssertionName)
                : TestResult.Failed(test.Test, test.Document, test.AssertionName, expected, actual);
        }
    }

    /// <summary>Replays one step, or answers why it could not be replayed.</summary>
    private static string? Refusal(EditorSession session, TestStep step) => step.Kind switch
    {
        TestingConceptKeys.Caret => PlaceCaret(session, step.Value),
        TestingConceptKeys.Type => RefusalOf(session.Apply(KeyRouter.Type(session, step.Value))),
        TestingConceptKeys.Press => Key(step.Value) is { } key
            ? RefusalOf(session.Apply(KeyRouter.For(session, key)))
            : $"'{step.Value}' is not a key the editor routes",
        TestingConceptKeys.Invoke => Invoke(session, step.Value),
        var other => $"'{other}' is not a step this framework replays",
    };

    /// <summary>Puts the caret in front of the first occurrence of <paramref name="text"/>.</summary>
    /// <remarks>
    /// The search is <see cref="TestNodes"/>', so a caret step and a node-addressed assertion cannot disagree about
    /// which occurrence a text names. The caret goes in front of it rather than after, so a step that types prefixes
    /// a value the way a reader expects.
    /// </remarks>
    private static string? PlaceCaret(EditorSession session, string text)
    {
        if (TestNodes.Match(session, text) is not { } match)
        {
            return $"nothing shows '{text}', so the caret cannot go there";
        }

        session.Select(Selection.At(CaretPosition.At(match.Address, match.Offset)));

        return null;
    }

    /// <summary>Applies the offer whose id the step names, or answers why it is not there to apply.</summary>
    private static string? Invoke(EditorSession session, string id)
    {
        var offered = IntentionList.For(session);

        if (offered.FirstOrDefault(offer => string.Equals(offer.Id, id, StringComparison.Ordinal)) is not { } pick)
        {
            return offered.Count == 0
                ? $"nothing is offered at the caret, so '{id}' cannot be invoked"
                : $"'{id}' is not offered at the caret; the offers are {string.Join(", ", offered.Select(offer => offer.Id))}";
        }

        return RefusalOf(session.Apply(pick));
    }

    /// <summary>The editor's key for <paramref name="name"/>, or <see langword="null"/>.</summary>
    /// <remarks>
    /// Lowering refused an unknown name already; this is the second door, for a step a host built in code.
    /// </remarks>
    private static EditorKey? Key(string name) =>
        Enum.TryParse<EditorKey>(name, ignoreCase: false, out var key)
        && Enum.GetNames<EditorKey>().Contains(name, StringComparer.Ordinal)
            ? key
            : null;

    /// <summary>Why an edit was refused, or <see langword="null"/> when it was not.</summary>
    /// <remarks>
    /// <see cref="EditOutcome.Unchanged"/> is not a refusal: <c>press Escape</c> and a caret move legitimately change
    /// nothing, and a step with nothing to do is answered by the drawing afterwards.
    /// </remarks>
    private static string? RefusalOf(EditResult result) =>
        result.Outcome == EditOutcome.Rejected ? result.Reason ?? "the edit was refused" : null;
}
