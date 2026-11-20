using System.Globalization;
using Projectional.Language;

namespace Projectional.Workbench;

/// <summary>The sentence a successful import leaves in the shell's status line.</summary>
/// <remarks>
/// <para>
/// An import that succeeded can still have kept something the model does not state - a construct as raw text, a member
/// that names another twice - and those travel as warnings precisely so a host can say so without treating the file as
/// refused. Dropping them was the shell's behaviour, and it is the one thing an import must not do silently: the user
/// picked a file, part of it is only text, and nothing said so.
/// </para>
/// <para>
/// The place comes first among the warnings so the sentence stays a sentence: an import of ten thousand lines that says
/// "with 3 warnings" and nothing else leaves the user with no way in, and one that names a line and a column can be
/// acted on even when the others are not named. The message is the importer's own words, because it knows what it did.
/// </para>
/// <para>
/// A refusal is not this type's business: it has no document, so the caller reports it where the text's place can be
/// shown, and the two stories never share a sentence.
/// </para>
/// </remarks>
internal static class ImportSummary
{
    /// <summary>The status line for an import that produced a document.</summary>
    /// <param name="fileName">The file the user picked, as the status line should name it.</param>
    /// <param name="language">The language that read it, which is worth naming when a window has several.</param>
    /// <param name="result">What the importer answered, whose warnings are reported here.</param>
    /// <returns>The sentence, with the first warning's place when there is one.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="result"/> refused the text; that is not a summary.</exception>
    internal static string Of(string fileName, string language, ImportResult result)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Succeeded)
        {
            throw new ArgumentException("A refused import has no document to summarise.", nameof(result));
        }

        var imported = $"Imported {fileName} as a document of {language}";

        var warnings = result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == ImportSeverity.Warning)
            .ToList();

        if (warnings.Count == 0)
        {
            return $"{imported}.";
        }

        var first = warnings[0];
        var count = warnings.Count.ToString(CultureInfo.InvariantCulture);

        // The importer's message is prose and usually ends in a full stop of its own; adding a second one would make
        // the line look like a bug. A sentence that quotes another sentence cannot assume its punctuation.
        var end = first.Message.Length > 0 && first.Message[^1] is '.' or '!' or '?' ? string.Empty : ".";

        return warnings.Count == 1
            ? $"{imported}, with a warning at {first.Line}:{first.Column}: {first.Message}{end}"
            : $"{imported}, with {count} warnings, the first at {first.Line}:{first.Column}: {first.Message}{end}";
    }
}
