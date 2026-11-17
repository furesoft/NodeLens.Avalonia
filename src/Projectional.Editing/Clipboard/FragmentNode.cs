using Projectional.Core;

namespace Projectional.Editing;

/// <summary>One node of a copied subtree: what concept it is, what it held, and what it contained.</summary>
/// <remarks>
/// <para>
/// The node keeps the id it had in the model it came from, as <see cref="Origin"/>, and that is not an
/// identity: a pasted node gets a new one. It is there so references <em>between</em> the copied nodes can be
/// rebound to the copies - a rule that pointed at a sibling must point at the sibling's copy rather than at
/// the original, which stays where it was.
/// </para>
/// <para>
/// Only properties that were actually set are recorded (<see cref="Node.IsSet"/>), so a value the model would
/// have defaulted is still defaulted after a paste. Copying the materialized default instead would write a
/// value the user never gave, and the difference between "declared default" and "written" is one the model
/// already has.
/// </para>
/// </remarks>
public sealed class FragmentNode
{
    /// <summary>Copies <paramref name="node"/> and everything below it.</summary>
    /// <param name="node">The node to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public FragmentNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        ConceptKey = node.Concept.Key;
        Origin = node.Id;

        Dictionary<string, string> properties = [];

        foreach (var slot in node.Concept.Slots)
        {
            if (slot is PropertySlot property && node.IsSet(property.Name) && node.GetOrNull(property.Name) is { } value)
            {
                properties[property.Name] = value;
            }
        }

        List<FragmentReference> references = [];

        foreach (var slot in node.Concept.Slots)
        {
            if (slot is ReferenceSlot reference && node.GetReference(reference.Name) is { } stored)
            {
                references.Add(new FragmentReference(reference.Name, stored.Target, stored.ResolveInfo));
            }
        }

        List<FragmentChild> children = [];

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            children.Add(new FragmentChild(child.ParentSlot!, new FragmentNode(child)));
        }

        Properties = properties;
        References = references;
        Children = children;
    }

    /// <summary>The concept the node was, as a key the target language has to declare.</summary>
    public string ConceptKey { get; }

    /// <summary>The id the node had where it was copied from, which a pasted copy does not reuse.</summary>
    public NodeId Origin { get; }

    /// <summary>The properties that were set, with the values they held.</summary>
    /// <remarks>
    /// A dictionary keeps no order, and it does not need one: a paste writes values rather than positions, and
    /// the persistence format orders properties by the concept's declarations rather than by insertion.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Properties { get; }

    /// <summary>The references the node held.</summary>
    public IReadOnlyList<FragmentReference> References { get; }

    /// <summary>The node's children, in the order a reader saw them, each with the slot it stood in.</summary>
    public IReadOnlyList<FragmentChild> Children { get; }
}
