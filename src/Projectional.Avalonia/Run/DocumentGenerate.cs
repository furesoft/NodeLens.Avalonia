using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Avalonia;

/// <summary>Generates a document's text and answers the lines the output panel shows.</summary>
/// <remarks>
/// The report is one row per generated line, and each row points at the node that produced the first stretch of it -
/// which is the traceability map put to use: clicking a line of the generated file moves the caret to what wrote it.
/// </remarks>
public static class DocumentGenerate
{
    /// <summary>Generates <paramref name="document"/> with <paramref name="generator"/>.</summary>
    /// <param name="document">The root the session shows.</param>
    /// <param name="generator">The generator to use, chosen by the user.</param>
    /// <returns>The text that was produced, and the report the panel shows.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static GeneratedText Generate(Node document, TextGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(generator);

        return new Generator().Generate(document, generator);
    }

    /// <summary>The report for <paramref name="generated"/>, one row per line.</summary>
    /// <param name="generated">What the generator produced.</param>
    /// <param name="generator">The generator that produced it, for the headline.</param>
    public static RunReport Report(GeneratedText generated, TextGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(generator);

        var headline = generated.Succeeded
            ? $"{generator.Name}: {generated.Text.Length} character(s), {generated.Regions.Count} region(s)"
            : $"{generator.Name}: {generated.Failure!.Message}";

        return new RunReport(generated.Succeeded, headline, RowsOf(generated));
    }

    private static List<RunRow> RowsOf(GeneratedText generated)
    {
        List<RunRow> rows = [];
        var start = 0;

        while (start <= generated.Text.Length)
        {
            var end = generated.Text.IndexOf('\n', start);
            var line = end < 0 ? generated.Text[start..] : generated.Text[start..end];

            // The row points at whatever wrote the beginning of this line; a line of pure whitespace has no owner.
            rows.Add(new RunRow(
                line.Replace("\r", string.Empty, StringComparison.Ordinal),
                generated.RegionAt(start) is { } region ? new CellAddress(region.Node) : null));

            if (end < 0)
            {
                break;
            }

            start = end + 1;
        }

        return rows;
    }
}
