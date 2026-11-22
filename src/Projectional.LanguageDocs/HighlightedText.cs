using System.Text;

using Projectional.Projection;

namespace Projectional.LanguageDocs;

/// <summary>Turns classified runs into HTML, so a drawing is coloured the way the editor colours it.</summary>
/// <remarks>
/// <para>
/// The classification comes from the renderer (<see cref="TextStyleKind"/>), never from the text: a page that decided
/// that a word starting with an upper-case letter is a type would be a highlighter that drifts from the editor it
/// documents. What this adds is only the other half - a class name per kind, and a stylesheet that gives those classes
/// the colours the editor's own palette gives them.
/// </para>
/// <para>
/// Runs are laid out by column, gaps become spaces, and a run that would overwrite one already written is cut, because
/// that is what the character grid does: two callers of the same drawing must not disagree about a character.
/// </para>
/// </remarks>
internal static class HighlightedText
{
    /// <summary>Renders <paramref name="runs"/> as lines of HTML, with every classified piece in a span.</summary>
    internal static string Of(IReadOnlyList<SnapshotRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);

        var lines = new List<string>();

        foreach (var row in runs.GroupBy(run => run.Row).OrderBy(group => group.Key))
        {
            var line = new StringBuilder();
            var column = 0;

            foreach (var run in row.OrderBy(run => run.Column, Comparer<int>.Default))
            {
                // A run that starts inside what was already drawn is cut: the grid would have overwritten the
                // characters, and a page that showed both would be showing something the editor never draws.
                if (run.Column < column)
                {
                    if (run.Column + run.Text.Length <= column)
                    {
                        continue;
                    }

                    var visible = run.Text[(column - run.Column)..];

                    line.Append(Span(run.Kind, visible));
                    column += visible.Length;

                    continue;
                }

                if (run.Column > column)
                {
                    line.Append(' ', run.Column - column);
                    column = run.Column;
                }

                line.Append(Span(run.Kind, run.Text));
                column += run.Text.Length;
            }

            lines.Add(line.ToString().TrimEnd());
        }

        // A snapshot drops trailing blank lines, so a page shows what was drawn rather than how much room was reserved.
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines);
    }

    /// <summary>The class a kind is drawn with, or an empty string for the unclassified default.</summary>
    internal static string Class(TextStyleKind kind) => kind switch
    {
        TextStyleKind.Keyword => "s-keyword",
        TextStyleKind.Punctuation => "s-punct",
        TextStyleKind.Identifier => "s-ident",
        TextStyleKind.Number => "s-number",
        TextStyleKind.Text => "s-text",
        TextStyleKind.Reference => "s-ref",
        TextStyleKind.Unresolved => "s-unresolved",
        TextStyleKind.Placeholder => "s-placeholder",
        TextStyleKind.Missing => "s-missing",
        TextStyleKind.Annotation => "s-annotation",
        _ => string.Empty,
    };

    /// <summary>One piece of text, wrapped only when its kind has a class of its own.</summary>
    private static string Span(TextStyleKind kind, string text)
    {
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var name = Class(kind);
        var escaped = Html.Escape(text);

        return name.Length == 0 ? escaped : $"<span class=\"{name}\">{escaped}</span>";
    }
}