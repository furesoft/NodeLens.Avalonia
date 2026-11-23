using Projectional.Core;

namespace Projectional.Language;

/// <summary>A chain of steps from a node to another node - the navigation a name body reads through.</summary>
/// <param name="Step">The hop this link makes.</param>
/// <param name="Slot">The slot a <see cref="PathStep.Child"/> or <see cref="PathStep.Reference"/> reads, otherwise nothing.</param>
/// <param name="Next">The rest of the path, or <see langword="null"/> when it ends here.</param>
/// <remarks>
/// <para>
/// A linked list rather than a list of steps, because that is what a model is: each step is a node with an optional
/// next one, so a path is as long as the document says and no part has to know how long.
/// </para>
/// <para>
/// Every hop may answer nothing - a root has no parent, a reference may be unset, a list has ends - and a path that
/// answers nothing at any point answers nothing at all. That is what makes a composed name total: the recursion ends
/// where the document does.
/// </para>
/// </remarks>
public sealed record NodePath
{
    /// <summary>Creates one link of a path.</summary>
    /// <param name="step">The hop this link makes.</param>
    /// <param name="slot">The slot <paramref name="step"/> reads, required by a child or a reference hop.</param>
    /// <param name="next">The rest of the path, or <see langword="null"/> when it ends here.</param>
    /// <exception cref="ArgumentException">
    /// A child or reference hop names no slot, or another hop names one.
    /// </exception>
    public NodePath(PathStep step, string? slot = null, NodePath? next = null)
    {
        var needsSlot = step is PathStep.Child or PathStep.Reference;

        if (needsSlot && slot is not { Length: > 0 })
        {
            throw new ArgumentException($"A '{step}' step has to name the slot it reads.", nameof(slot));
        }

        if (!needsSlot && slot is not null)
        {
            throw new ArgumentException($"A '{step}' step reads no slot.", nameof(slot));
        }

        Step = step;
        Slot = slot;
        Next = next;
    }

    /// <summary>The hop this link makes.</summary>
    public PathStep Step { get; }

    /// <summary>The slot a child or reference hop reads.</summary>
    public string? Slot { get; }

    /// <summary>The rest of the path, or <see langword="null"/> when it ends here.</summary>
    public NodePath? Next { get; }

    /// <summary>The node <paramref name="from"/> reaches along this path, or <see langword="null"/>.</summary>
    /// <param name="from">The node the path starts at.</param>
    /// <exception cref="ArgumentNullException"><paramref name="from"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Total, like every part of a name: a step that cannot be taken - a root has no parent, a slot holds nothing, a name
    /// the concept no longer declares - answers nothing rather than throwing, so a name is never the reason a reference
    /// cell's build fails. Only the first step of a path can be checked against a known concept, which is what the
    /// lowering does for the step it can see; the rest is a property of the document.
    /// </remarks>
    public Node? Follow(Node from)
    {
        ArgumentNullException.ThrowIfNull(from);

        var reached = Step switch
        {
            PathStep.Self => from,
            PathStep.Parent => from.Parent,
            PathStep.Child => from.Concept.TryGetSlot(Slot!, out var child) && child is ChildSlot
                ? from.GetChild(Slot!)
                : null,
            PathStep.Reference => from.Concept.TryGetSlot(Slot!, out var reference) && reference is ReferenceSlot
                ? from.GetReferenceTarget(Slot!)
                : null,
            PathStep.NextSibling => Sibling(from, 1),
            PathStep.PreviousSibling => Sibling(from, -1),
            _ => null,
        };

        return reached is null || Next is null ? reached : Next.Follow(reached);
    }

    /// <summary>The sibling <paramref name="offset"/> places away in the same slot, or nothing at the list's end.</summary>
    private static Node? Sibling(Node from, int offset)
    {
        if (from.Parent is not { } parent || from.ParentSlot is not { } slot)
        {
            return null;
        }

        var siblings = parent.GetChildren(slot);
        var at = from.IndexInParent + offset;

        return at >= 0 && at < siblings.Count ? siblings[at] : null;
    }

    public override string ToString() =>
        Step is PathStep.Child or PathStep.Reference
            ? $"{Step}({Slot}){(Next is null ? string.Empty : "." + Next)}"
            : $"{Step}{(Next is null ? string.Empty : "." + Next)}";
}
