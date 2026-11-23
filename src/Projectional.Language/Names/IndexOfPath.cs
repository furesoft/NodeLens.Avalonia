using System.Globalization;

using Projectional.Core;

namespace Projectional.Language;

/// <summary>The position of the node a path reaches among its siblings, counted from one.</summary>
/// <param name="Path">The steps to the node whose position is read.</param>
/// <remarks>
/// <para>
/// For a list whose elements have no name of their own: <c>join("stop ", index of self)</c> calls the third stop
/// <c>stop 3</c> without a property anyone has to keep in step with the order - and
/// <c>index of parent</c> is the same question about the node above.
/// </para>
/// <para>
/// Counted from one because it is read by people, in the slot's own order because that is the order the notation draws.
/// A position is not an identity: it moves when an element is inserted before it, which is why it belongs in a name and
/// not in a reference's fallback text.
/// </para>
/// </remarks>
public sealed record IndexOfPath : NameBody
{
    /// <summary>Creates a reading of the position <paramref name="path"/> reaches.</summary>
    /// <param name="path">The steps to the node whose position is read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public IndexOfPath(NodePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        Path = path;
    }

    /// <summary>The steps to the node whose position is read.</summary>
    public NodePath Path { get; }

    private protected override string Evaluate(Node node) => Path.Follow(node) is { } reached && reached.IndexInParent >= 0
        ? (reached.IndexInParent + 1).ToString(CultureInfo.InvariantCulture)
        : string.Empty;

    public override string ToString() => $"index of {Path}";
}
