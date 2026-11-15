namespace Projectional.Editing;

/// <summary>
/// A rule between two groups of menu entries.
/// </summary>
/// <remarks>
/// A separator carries no identity, so it has no state and cannot be compared per instance: two
/// separators mean the same thing. A host therefore maps it to whatever its toolkit calls a rule and
/// ignores the entry itself; <see cref="EditorMenuEntry.Label"/> stays empty so that a host which
/// needs a name has one rather than a special case.
/// </remarks>
public sealed class EditorMenuSeparator : EditorMenuEntry
{
    /// <summary>The one separator; entries are descriptions, so a second instance would say nothing new.</summary>
    public static EditorMenuSeparator Instance { get; } = new();

    private EditorMenuSeparator()
        : base(string.Empty)
    {
    }
}
