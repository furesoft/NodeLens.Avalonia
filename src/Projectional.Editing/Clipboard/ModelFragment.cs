using Projectional.Core;

namespace Projectional.Editing;

/// <summary>A copied piece of a model: one or more subtrees, with their values and references.</summary>
/// <remarks>
/// <para>
/// This is the structural payload of the clipboard. It is deliberately not text and not a document: a fragment
/// names concepts by key and keeps the ids it came from, so pasting it needs a language that declares those
/// concepts and a model to put them into - and it can therefore answer "this does not fit here" instead of
/// producing a document that half-loads.
/// </para>
/// <para>
/// Several roots, because a selection can cover several siblings. The order is the order the user saw; a paste
/// inserts them in that order, so what was copied is what appears.
/// </para>
/// </remarks>
public sealed class ModelFragment
{
    /// <summary>Creates a fragment from roots that were already copied.</summary>
    /// <param name="roots">The copied subtrees, in the order a paste should insert them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="roots"/> is <see langword="null"/>.</exception>
    public ModelFragment(IReadOnlyList<FragmentNode> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);

        Roots = [.. roots];
    }

    /// <summary>The copied subtrees, in the order a paste should insert them.</summary>
    public IReadOnlyList<FragmentNode> Roots { get; }

    /// <summary>Whether there is nothing to paste.</summary>
    public bool IsEmpty => Roots.Count == 0;

    /// <summary>Copies <paramref name="nodes"/> and everything below them.</summary>
    /// <param name="nodes">The nodes to copy, in the order a paste should insert them.</param>
    /// <returns>A fragment holding one root per node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="nodes"/> is <see langword="null"/>.</exception>
    public static ModelFragment Of(IReadOnlyList<Node> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        List<FragmentNode> roots = [];

        foreach (var node in nodes)
        {
            roots.Add(new FragmentNode(node));
        }

        return new ModelFragment(roots);
    }

    /// <summary>
    /// The first concept key the fragment names that <paramref name="concepts"/> does not declare, or
    /// <see langword="null"/> when the whole fragment is expressible in that language.
    /// </summary>
    /// <param name="concepts">The registry of the language the fragment would be pasted into.</param>
    /// <remarks>
    /// The question a paste has to answer before it creates anything: a fragment from another language is the
    /// ordinary case, and refusing it with the first name it does not recognise is more useful than a
    /// half-built tree. It is answered here rather than in the action so that the model, the action and a test
    /// cannot disagree about what "expressible" means.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="concepts"/> is <see langword="null"/>.</exception>
    public string? MissingConcept(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        Stack<FragmentNode> pending = new(Roots);

        while (pending.Count > 0)
        {
            var node = pending.Pop();

            if (!concepts.TryGet(node.ConceptKey, out _))
            {
                return node.ConceptKey;
            }

            foreach (var child in node.Children)
            {
                pending.Push(child.Node);
            }
        }

        return null;
    }
}
