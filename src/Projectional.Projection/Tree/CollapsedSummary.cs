using Projectional.Core;

namespace Projectional.Projection;

/// <summary>What a folded node's one line says: its name if it has one, and a mark that something is hidden.</summary>
/// <remarks>
/// <para>
/// The rule follows the reference cell's - the name slot's value, or the concept key when there is none -
/// and then marks the omission with an ellipsis. So a folded rule reads <c>check-age …</c> and a nameless
/// expression reads <c>Expressions.Binary …</c>.
/// </para>
/// <para>
/// The result is never the empty string: layout measures empty text as one space, and a zero-width cell
/// would make the caret stop invisible.
/// </para>
/// </remarks>
public static class CollapsedSummary
{
    /// <summary>The mark that says the line stands for more than itself.</summary>
    public const string Omission = "\u2026";

    /// <summary>What a folded <paramref name="node"/> draws.</summary>
    /// <param name="node">The node being folded.</param>
    /// <returns>Its name or concept key, then a space and an ellipsis.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public static string Of(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var name = node.Name;

        return string.IsNullOrWhiteSpace(name)
            ? $"{node.Concept.Key} {Omission}"
            : $"{name.Trim()} {Omission}";
    }
}
