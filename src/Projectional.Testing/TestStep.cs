namespace Projectional.Testing;

/// <summary>One step of a script: something a user does at the caret.</summary>
/// <param name="Kind">The concept key of the step, one of the four in <see cref="TestingConceptKeys"/>.</param>
/// <param name="Value">
/// What the step names - the text the caret goes to, the characters typed, the key pressed, or the id of the offer
/// invoked.
/// </param>
/// <remarks>
/// <para>
/// A value rather than the node it came from, for the same reason <see cref="LanguageTest"/> is: a step has to be
/// replayable without a session, and a session cannot be read out of a node. Lowering is where a malformed step is
/// refused, so the runner never has to ask whether a step says enough.
/// </para>
/// <para>
/// The four kinds are the vocabulary of an editor test, and it is deliberately the vocabulary of a <em>user</em>:
/// go somewhere, type, press a key, pick an offer. Nothing here can select a node by identity, because a test is
/// written as data and a node id is a GUID nobody can read - a place is named by what the document shows there,
/// which is also what a reviewer can check.
/// </para>
/// </remarks>
public sealed record TestStep(string Kind, string Value)
{
    /// <summary>The step as a person reads it, which is how a failed step names itself in a report.</summary>
    public override string ToString() => Kind switch
    {
        TestingConceptKeys.Caret => $"caret at '{Value}'",
        TestingConceptKeys.Type => $"type '{Value}'",
        TestingConceptKeys.Press => $"press {Value}",
        TestingConceptKeys.Invoke => $"invoke {Value}",
        _ => $"{Kind} {Value}",
    };
}
