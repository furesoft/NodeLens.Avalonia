using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Which node, and which end of it, a caret place means - the question every
/// <see cref="ConceptTrigger"/> is asked about.
/// </summary>
/// <remarks>
/// <para>
/// A projectional document has no character between two nodes, so "next to" has to be read off the cells:
/// the caret is at the start of a node when it stands on the first cell that node owns, and at its end when it
/// stands on the last one. Inside a property cell the offset says it directly, which is the case a user meets
/// most - typing an operator after the last digit of a number.
/// </para>
/// <para>
/// <b>Ownership, not containment.</b> The cells counted here are the ones whose address names the node itself
/// (<see cref="CellAddress.Owner"/>), not the ones inside its children: a child's projection carries the
/// parent's element address and the child's own cells carry the child's id, so a node's own first and last
/// cell are exactly the edges of the thing the user sees. That is also why a keyword in the middle of a
/// notation - the <c>then</c> of an if - is no edge at all and offers nothing.
/// </para>
/// <para>
/// Every answer here is a candidate, never a decision: the caller tries them in order and the language's
/// triggers decide. A node drawn as one cell yields both sides, with the right one first, because
/// "and then continue" is what a user typing after such a node means.
/// </para>
/// </remarks>
internal static class TriggerAnchors
{
    /// <summary>The nodes <paramref name="place"/> could be an edge of, in the order to try them.</summary>
    /// <param name="context">The apply in progress, which owns the places and the tree.</param>
    /// <param name="place">Where the caret stands.</param>
    /// <param name="offset">The caret's offset inside that cell.</param>
    /// <returns>One or two candidates, or none when the place is not an edge of anything.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<TriggerAnchor> Of(EditContext context, CaretPlace place, int offset)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.NodeOf(place.Address) is not { } node)
        {
            return [];
        }

        if (place.Cell is PropertyCell)
        {
            return EdgesOf(place, offset) switch
            {
                Edges.Both => [new(node, TriggerSide.Right), new(node, TriggerSide.Left)],
                Edges.Start => [new(node, TriggerSide.Left)],
                Edges.End => [new(node, TriggerSide.Right)],
                _ => [],
            };
        }

        var (first, last) = BoundsOf(context, node);

        if (place.Order == first && place.Order == last)
        {
            return [new(node, TriggerSide.Right), new(node, TriggerSide.Left)];
        }

        if (place.Order == first)
        {
            return [new(node, TriggerSide.Left)];
        }

        return place.Order == last ? [new(node, TriggerSide.Right)] : [];
    }

    /// <summary>Which ends of a property's text an offset means: an empty value is both, an interior one neither.</summary>
    private static Edges EdgesOf(CaretPlace place, int offset)
    {
        if (place.Length == 0)
        {
            return Edges.Both;
        }

        if (offset <= 0)
        {
            return Edges.Start;
        }

        return offset >= place.Length ? Edges.End : Edges.None;
    }

    /// <summary>The document order of the first and last place whose address names <paramref name="node"/>.</summary>
    private static (int First, int Last) BoundsOf(EditContext context, Node node)
    {
        var first = int.MaxValue;
        var last = int.MinValue;

        foreach (var each in context.Places)
        {
            if (each.Address.Owner != node.Id)
            {
                continue;
            }

            first = Math.Min(first, each.Order);
            last = Math.Max(last, each.Order);
        }

        return (first, last);
    }

    /// <summary>The ends of a property's text an offset can name.</summary>
    private enum Edges
    {
        None,
        Start,
        End,
        Both,
    }
}
