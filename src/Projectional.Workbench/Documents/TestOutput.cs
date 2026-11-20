using System.Globalization;

using Projectional.Avalonia;
using Projectional.Languages;
using Projectional.Testing;

namespace Projectional.Workbench;

/// <summary>Turns a run of a language's tests into the rows the output panel shows.</summary>
/// <remarks>
/// <para>
/// The panel already knows how to show one line per row, which is why a test run does not need a panel of its own:
/// a run of a language's tests is a list of things that happened, exactly like an interpretation's trace, and the
/// difference is only what a row means.
/// </para>
/// <para>
/// One row per test, in the order the runner produced them, and that order is the contract the window relies on: it
/// resolves a chosen row back to its <see cref="TestResult"/> by position, so a row must not be dropped, reordered or
/// added. A summary row is therefore not one of the rows - it is the headline, where the run's answer belongs.
/// </para>
/// <para>
/// A row names the test, the assertion and the document, and says pass or fail; the diff itself stays in
/// <see cref="TestReport.ToString"/> and in the failing test's own document, because a row is one line and the
/// point of the row is to be clickable.
/// </para>
/// </remarks>
internal static class TestOutput
{
    /// <summary>The report the output panel shows for a run of <paramref name="tests"/>.</summary>
    /// <param name="tests">The run's report.</param>
    /// <param name="language">The project that was run, which names the folder in the headline.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    internal static RunReport Report(TestReport tests, LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(language);

        return new RunReport(
            tests.Succeeded,
            Headline(tests, language),
            [.. tests.Results.Select(result => new RunRow(Line(result), Place: null))]);
    }

    /// <summary>The one line that says what the run answered.</summary>
    private static string Headline(TestReport tests, LanguageProject language) =>
        tests.Ran == 0
            ? $"{language.Name}: no tests folder, so there was nothing to run"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{language.Name}: {tests.Ran} test(s), {tests.Passed} passed, {tests.Failed} failed");

    /// <summary>One row: what passed or failed, which assertion, and about which document.</summary>
    private static string Line(TestResult result) => string.Create(
        CultureInfo.InvariantCulture,
        $"{(result.Succeeded ? "passed" : "FAILED")}  {result.Test}  {result.Assertion}  {result.Document}");
}
