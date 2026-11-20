namespace Projectional.Testing;

/// <summary>One test, lowered from a document into the two facts a runner needs.</summary>
/// <param name="Test">The name of the test document, without its extension - the row's name in a report.</param>
/// <param name="Document">The model file the test asserts about, relative to the language project.</param>
/// <param name="Assertion">The concept key of the assertion, one of the closed set in <see cref="TestingConceptKeys"/>.</param>
/// <remarks>
/// A value rather than the node it came from: the runner needs to say what it is about to run before it runs it,
/// and a node cannot be read without a session. Lowering is where a malformed test is refused, so the runner
/// never has to ask whether the document slot is filled.
/// </remarks>
public sealed record LanguageTest(string Test, string Document, string Assertion)
{
    /// <summary>The assertion as a person reads it.</summary>
    public string AssertionName => Assertion switch
    {
        TestingConceptKeys.RoundTrips => "round trips",
        _ => Assertion,
    };
}
