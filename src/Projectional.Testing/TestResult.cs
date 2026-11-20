namespace Projectional.Testing;

/// <summary>What one test document asserted, and what happened.</summary>
/// <param name="Test">The name of the test document, without its extension.</param>
/// <param name="Document">The model file the test asserted about.</param>
/// <param name="Assertion">The assertion as a person reads it.</param>
/// <param name="Succeeded">Whether the assertion held.</param>
/// <param name="Expected">What the assertion expected, when it failed; otherwise <see langword="null"/>.</param>
/// <param name="Actual">What the document turned out to be, when it failed; otherwise <see langword="null"/>.</param>
/// <remarks>
/// The pair of texts is the whole point of a row: a projection or a generator change that moves a cell should read
/// as a diff, not as "the test failed". A load failure carries its message in <paramref name="Actual"/> and leaves
/// <paramref name="Expected"/> empty, because there is nothing to compare against.
/// </remarks>
public sealed record TestResult(
    string Test,
    string Document,
    string Assertion,
    bool Succeeded,
    string? Expected = null,
    string? Actual = null)
{
    /// <summary>A row for an assertion that held.</summary>
    public static TestResult Passed(string test, string document, string assertion) =>
        new(test, document, assertion, Succeeded: true);

    /// <summary>A row for an assertion that did not hold, carrying both texts.</summary>
    public static TestResult Failed(
        string test,
        string document,
        string assertion,
        string? expected,
        string? actual) =>
        new(test, document, assertion, Succeeded: false, expected, actual);
}
