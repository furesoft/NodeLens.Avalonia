namespace Projectional.Semantics;

/// <summary>
/// What a name means in a scope: one thing, several things, or nothing.
/// </summary>
/// <remarks>
/// <para>
/// Ambiguity is a value rather than a silent pick, because the design says so: a name that matches two
/// things in the same scope does not mean either of them, and an editor that chose would be inventing
/// a decision the user has to make. The caller decides what to do with it - a completion list shows
/// both, a typed name is refused with a reason.
/// </para>
/// <para>
/// A struct rather than a class hierarchy: the three answers differ in one field each, and every
/// consumer switches on <see cref="Kind"/> anyway.
/// </para>
/// </remarks>
public readonly record struct ScopeResolution
{
    private ScopeResolution(ScopeResolutionKind kind, ScopeEntry entry, IReadOnlyList<ScopeEntry> candidates)
    {
        Kind = kind;
        Entry = entry;
        Candidates = candidates;
    }

    /// <summary>Which of the three answers this is.</summary>
    public ScopeResolutionKind Kind { get; }

    /// <summary>The one thing the name means, when <see cref="Kind"/> is <see cref="ScopeResolutionKind.Found"/>.</summary>
    public ScopeEntry Entry { get; }

    /// <summary>
    /// Everything the name matches, when <see cref="Kind"/> is
    /// <see cref="ScopeResolutionKind.Ambiguous"/>; empty otherwise.
    /// </summary>
    public IReadOnlyList<ScopeEntry> Candidates { get; }

    /// <summary>Nothing carries the name.</summary>
    public static ScopeResolution Missing { get; } = new(ScopeResolutionKind.Missing, default, []);

    /// <summary>Exactly one thing carries the name.</summary>
    public static ScopeResolution Found(ScopeEntry entry) =>
        new(ScopeResolutionKind.Found, entry, [entry]);

    /// <summary>More than one thing carries the name.</summary>
    /// <exception cref="ArgumentException">Fewer than two candidates were given.</exception>
    public static ScopeResolution Ambiguous(IReadOnlyList<ScopeEntry> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count < 2)
        {
            throw new ArgumentException(
                "An ambiguous resolution needs at least two candidates; one candidate is a found one.",
                nameof(candidates));
        }

        return new ScopeResolution(ScopeResolutionKind.Ambiguous, default, candidates);
    }

    public override string ToString() => Kind switch
    {
        ScopeResolutionKind.Found => $"found {Entry}",
        ScopeResolutionKind.Ambiguous => $"ambiguous between {string.Join(", ", Candidates)}",
        _ => "missing",
    };
}
