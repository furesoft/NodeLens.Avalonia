namespace Projectional.Testing;

/// <summary>One test, lowered from a document into the facts a runner needs.</summary>
/// <param name="Test">The name of the test document, without its extension - the row's name in a report.</param>
/// <param name="Document">The model file the test asserts about, relative to the language project.</param>
/// <param name="Assertion">The concept key of the assertion, one of the closed set in <see cref="TestingConceptKeys"/>.</param>
/// <param name="Width">The columns a drawing assertion renders into; ignored by the others.</param>
/// <param name="Lines">The lines a drawing or generation assertion expects, in order; ignored by the others.</param>
/// <param name="Findings">The findings a findings assertion expects, in order; ignored by the others.</param>
/// <param name="Generator">The generator a generation assertion runs; ignored by the others.</param>
/// <param name="Variables">The <c>name=value</c> pairs an evaluation assertion runs with; ignored by the others.</param>
/// <param name="Outcome">The answer or failure an evaluation assertion expects; ignored by the others.</param>
/// <param name="Steps">The steps a script assertion replays, in order; ignored by the others.</param>
/// <remarks>
/// <para>
/// A value rather than the node it came from: the runner needs to say what it is about to run before it runs it,
/// and a node cannot be read without a session. Lowering is where a malformed test is refused, so the runner
/// never has to ask whether a slot is filled - the fields the assertion needs are filled by the time a
/// <see cref="LanguageTest"/> exists.
/// </para>
/// <para>
/// The expectation is flat rather than a hierarchy of assertion types: six assertions need six different
/// shapes, and a record with one field per shape is still smaller than a type per assertion and easier to read in
/// a report. The assertion key says which fields matter, and lowering fills only those - a field set on the wrong
/// assertion is never read.
/// </para>
/// </remarks>
public sealed record LanguageTest(
    string Test,
    string Document,
    string Assertion,
    int Width = 0,
    IReadOnlyList<string>? Lines = null,
    IReadOnlyList<ExpectedFinding>? Findings = null,
    string? Generator = null,
    string? Variables = null,
    string? Outcome = null,
    IReadOnlyList<TestStep>? Steps = null)
{
    /// <summary>The assertion as a person reads it.</summary>
    public string AssertionName => Assertion switch
    {
        TestingConceptKeys.RoundTrips => "round trips",
        TestingConceptKeys.Draws => "draws",
        TestingConceptKeys.Findings => "reports",
        TestingConceptKeys.Generates => "generates",
        TestingConceptKeys.Evaluates => "evaluates",
        TestingConceptKeys.Script => "replays",
        _ => Assertion,
    };
}
