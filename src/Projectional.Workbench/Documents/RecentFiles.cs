namespace Projectional.Workbench;

/// <summary>The files opened lately, newest first, without repeats.</summary>
/// <remarks>
/// <para>
/// A value, not a mutable list: opening a file produces the next list rather than editing one, so a menu built from it
/// cannot be looking at a list somebody else is changing. That is also why the rules below are all on this type - the
/// store only moves the bytes.
/// </para>
/// <para>
/// Comparison is case-insensitive because a path on Windows is: opening <c>C:\a.mps</c> and <c>c:\a.mps</c> is opening
/// one file, and a list that showed it twice would be a list that lies about what the user did.
/// </para>
/// </remarks>
internal sealed record RecentFiles(IReadOnlyList<string> Paths)
{
    /// <summary>How many entries are remembered.</summary>
    /// <remarks>
    /// Ten is a menu, not an archive: a longer list is one nobody reads to the end, and every entry costs a click to
    /// keep alive in the file picker instead.
    /// </remarks>
    public const int Capacity = 10;

    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>Nothing has been opened yet.</summary>
    public static RecentFiles Empty { get; } = new([]);

    /// <summary>Whether anything has been opened.</summary>
    public bool IsEmpty => Paths.Count == 0;

    /// <summary>Reads a stored list, applying the same rules as <see cref="With"/>.</summary>
    /// <param name="paths">What a store answered, which may hold repeats or more entries than fit.</param>
    /// <remarks>
    /// A store is a file a user can edit, so what comes out of it is validated rather than trusted: blank lines are
    /// dropped and the cap is applied, which is what keeps a hand-edited file from producing a menu of forty entries.
    /// </remarks>
    public static RecentFiles From(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var kept = new List<string>();

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)
                || kept.Contains(path, Comparer)
                || kept.Count == Capacity)
            {
                continue;
            }

            kept.Add(path);
        }

        return new RecentFiles(kept);
    }

    /// <summary>This list with <paramref name="path"/> first.</summary>
    /// <param name="path">The file just opened or saved.</param>
    public RecentFiles With(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return From([path, .. Paths.Where(kept => !Comparer.Equals(kept, path))]);
    }

    /// <summary>This list without <paramref name="path"/>, for a file that is gone.</summary>
    /// <param name="path">The file to forget.</param>
    public RecentFiles Without(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return From(Paths.Where(kept => !Comparer.Equals(kept, path)));
    }
}