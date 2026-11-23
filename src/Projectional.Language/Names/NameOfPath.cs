using Projectional.Core;

namespace Projectional.Language;

/// <summary>The name of the node a path reaches - the reading every navigation ends in.</summary>
/// <param name="Path">The steps to the node to name.</param>
/// <remarks>
/// The counterpart of the shorthand parts: <c>child x</c> is <c>name of child(x)</c>, and because the path is a chain,
/// so is <c>name of parent.reference(start)</c> - the name of the place the route above this node starts at. That
/// composition is the reason navigation and reading are two levels rather than one part per relation
/// (docs/architecture/behaviours.md).
/// </remarks>
public sealed record NameOfPath : NameBody
{
    /// <summary>Creates a reading of the name <paramref name="path"/> reaches.</summary>
    /// <param name="path">The steps to the node to name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public NameOfPath(NodePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        Path = path;
    }

    /// <summary>The steps to the node to name.</summary>
    public NodePath Path { get; }

    private protected override string Evaluate(Node node) => Path.Follow(node)?.Name ?? string.Empty;

    public override string ToString() => $"name of {Path}";
}
