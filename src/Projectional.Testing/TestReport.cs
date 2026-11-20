using System.Globalization;
using System.Text;

namespace Projectional.Testing;

/// <summary>The rows one run of a language's tests folder produced, in the order the files are named.</summary>
/// <remarks>
/// A value rather than console output, so the same report can be a failing xunit test today and a row in the
/// workbench's output panel later. <see cref="ToString"/> renders a diff for the failures, because a report that
/// says "not equal" makes a width change read as fifty failures.
/// </remarks>
public sealed class TestReport
{
    /// <summary>A report of a folder that holds no tests.</summary>
    public static TestReport Empty { get; } = new([]);

    /// <summary>The rows of the run, in file-name order.</summary>
    public IReadOnlyList<TestResult> Results { get; }

    /// <summary>How many tests ran.</summary>
    public int Ran => Results.Count;

    /// <summary>How many held.</summary>
    public int Passed => Results.Count(result => result.Succeeded);

    /// <summary>How many did not.</summary>
    public int Failed => Ran - Passed;

    /// <summary>Whether every test held. A folder with no tests holds, and says so by <see cref="Ran"/>.</summary>
    public bool Succeeded => Failed == 0;

    /// <summary>Creates a report from the rows of a run.</summary>
    /// <param name="results">The rows, in the order the run produced them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="null"/>.</exception>
    public TestReport(IReadOnlyList<TestResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        Results = results;
    }

    /// <summary>The summary line, then one block per failed row with its expected and actual text.</summary>
    public override string ToString()
    {
        var text = new StringBuilder();

        text.Append(Ran == 0
            ? "No tests: the language's tests folder holds no documents."
            : string.Create(CultureInfo.InvariantCulture, $"{Ran} test(s), {Passed} passed, {Failed} failed."));

        foreach (var result in Results.Where(result => !result.Succeeded))
        {
            text.AppendLine();
            text.AppendLine();
            text.Append(CultureInfo.InvariantCulture, $"{result.Test}: {result.Document} {result.Assertion} - FAILED");

            if (result.Expected is not null)
            {
                text.AppendLine();
                text.AppendLine("expected:");
                text.Append(result.Expected);
            }

            if (result.Actual is not null)
            {
                text.AppendLine();
                text.AppendLine("actual:");
                text.Append(result.Actual);
            }
        }

        return text.ToString();
    }
}
