using System.Globalization;

namespace Projectional.Editing;

/// <summary>
/// The sentence a ghost carries in its corner: what is being previewed and how much of it changed.
/// </summary>
/// <remarks>
/// <para>
/// Without it the ghost is a document that appears to have been edited, which is the one thing a preview must
/// never look like. The label is the entry's own, so the badge cannot disagree with the list it came from; the
/// counts come from <see cref="PreviewMarks"/> and count nodes rather than cells.
/// </para>
/// <para>
/// The shape is fixed here rather than left to a host, because it is user-facing text with grammar in it: one
/// kind of change reads as a total ("4 changes"), and several are broken down ("2 added, 3 removed"), which is
/// what a reader needs to decide whether the edit is the one they meant.
/// </para>
/// </remarks>
public static class PreviewBadge
{
    /// <summary>The word every badge starts with, so a host that prints only the rest stays consistent.</summary>
    public const string Prefix = "Preview";

    /// <summary>The badge for <paramref name="label"/> with <paramref name="marks"/>.</summary>
    /// <param name="label">What the entry is called, verbatim.</param>
    /// <param name="marks">What the edit changed.</param>
    /// <returns>A sentence such as <c>Preview - rename, 4 changes</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="marks"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="label"/> is empty.</exception>
    public static string Of(string label, PreviewMarks marks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(marks);

        var summary = marks.IsEmpty ? "no changes" : Summary(marks);

        if (marks.Truncated > 0)
        {
            summary = string.Concat(
                summary,
                " (",
                Count(marks.Truncated, "mark"),
                " not drawn)");
        }

        return $"{Prefix} - {label}, {summary}";
    }

    private static string Summary(PreviewMarks marks)
    {
        var kinds = 0;

        if (marks.Added > 0)
        {
            kinds++;
        }

        if (marks.Changed > 0)
        {
            kinds++;
        }

        if (marks.Removed > 0)
        {
            kinds++;
        }

        if (kinds == 1)
        {
            return Count(marks.Added + marks.Changed + marks.Removed, "change");
        }

        var parts = new List<string>(kinds);

        if (marks.Added > 0)
        {
            parts.Add($"{marks.Added} added");
        }

        if (marks.Changed > 0)
        {
            parts.Add($"{marks.Changed} changed");
        }

        if (marks.Removed > 0)
        {
            parts.Add($"{marks.Removed} removed");
        }

        return string.Join(", ", parts);
    }

    private static string Count(int value, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{value} {noun}{(value == 1 ? string.Empty : "s")}");
}
