using Projectional.Core;

namespace Projectional.Language;

/// <summary>The names of the children in a slot, one after another, the unnamed ones left out.</summary>
/// <param name="Slot">The child slot to read.</param>
/// <param name="Separator">What stands between two answers that are both non-empty.</param>
/// <remarks>
/// <para>
/// The sibling of <see cref="NameChild"/>, for the shape one child cannot express: a route called after the places it
/// stops at is a name over a <em>list</em>, and a list has no single child to read. The slot may hold one child or many,
/// because either answers the names it holds.
/// </para>
/// <para>
/// Like <see cref="NameJoin"/> it omits rather than concatenates, so a list whose second element is still unnamed is
/// called after its first instead of ending in a separator no user typed.
/// </para>
/// </remarks>
public sealed record NameChildren : NameBody
{
    /// <summary>Creates a part that answers the names of the children in <paramref name="slot"/>.</summary>
    /// <param name="slot">The child slot to read.</param>
    /// <param name="separator">What stands between two answers; may be empty.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="separator"/> is <see langword="null"/>.</exception>
    public NameChildren(string slot, string separator)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(separator);

        Slot = slot;
        Separator = separator;
    }

    /// <summary>The child slot to read.</summary>
    public string Slot { get; }

    /// <summary>What stands between two answers that are both non-empty.</summary>
    public string Separator { get; }

    private protected override string Evaluate(Node node)
    {
        if (node.Concept.FindChild(Slot) is null)
        {
            throw new InvalidOperationException(
                $"'{Slot}' is not a child slot of {node.Concept.Key}, so a name body cannot read its names. The declared form refuses this at load.");
        }

        List<string> answers = [];

        foreach (var child in node.GetChildren(Slot))
        {
            var answer = child.Name ?? string.Empty;

            if (answer.Length > 0)
            {
                answers.Add(answer);
            }
        }

        return string.Join(Separator, answers);
    }

    public override string ToString() => $"children {Slot} \"{Separator}\"";
}
