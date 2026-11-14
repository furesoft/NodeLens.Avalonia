namespace Projectional.Core;

/// <summary>
/// What a reference slot stores: the target id, plus the text the target was called when the
/// reference was written.
/// </summary>
/// <remarks>
/// The fallback text is what makes an unresolved reference degrade into a readable marker
/// instead of an exception, and it is what lets a reference be re-bound when a node comes back
/// with a new id — a paste, a merge, or a file that was edited outside the editor.
/// </remarks>
public readonly record struct NodeReference(NodeId Target, string? ResolveInfo)
{
    /// <summary>Whether this reference points at <paramref name="project"/>'s node index.</summary>
    public bool IsResolvedIn(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.FindNode(Target) is not null;
    }

    public override string ToString() => ResolveInfo is { } info ? $"{ResolveInfo} ({Target})" : Target.ToString();
}
