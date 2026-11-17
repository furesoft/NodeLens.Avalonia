namespace Projectional.Semantics;

/// <summary>What a name means in a scope.</summary>
public enum ScopeResolutionKind
{
    /// <summary>Nothing in the scope carries the name.</summary>
    Missing,

    /// <summary>Exactly one thing carries it, and that is what the name means.</summary>
    Found,

    /// <summary>More than one thing carries it, so the name does not mean anything yet.</summary>
    Ambiguous,
}
