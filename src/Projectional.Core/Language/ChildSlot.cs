namespace Projectional.Core;

/// <summary>
/// A slot holding owned child nodes. Children are deleted with their parent and keep their
/// order, because a projection shows them in that order and document order is content.
/// </summary>
public sealed record ChildSlot : SlotDeclaration
{
    public ChildSlot(string name, string childConceptKey, Cardinality cardinality = Cardinality.Many)
        : base(name)
    {
        if (!NameRules.IsValidConceptKey(childConceptKey))
        {
            throw new ArgumentException($"'{childConceptKey}' is not a valid concept key.", nameof(childConceptKey));
        }

        ChildConceptKey = childConceptKey;
        Cardinality = cardinality;
    }

    /// <summary>Every child has to be of this concept or extend it.</summary>
    public string ChildConceptKey { get; }

    public Cardinality Cardinality { get; }
}
