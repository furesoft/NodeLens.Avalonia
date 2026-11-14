namespace Projectional.Core;

/// <summary>
/// Declares one named slot of a concept. A node's data is exactly the slot set its concept
/// declares, which is what rules out free-form dictionaries and the silent drift they allow.
/// </summary>
public abstract record SlotDeclaration
{
    private protected SlotDeclaration(string name)
    {
        if (!NameRules.IsValidSlotName(name))
        {
            throw new ArgumentException($"'{name}' is not a valid slot name.", nameof(name));
        }

        Name = name;
    }

    public string Name { get; }
}

/// <summary>A value of a primitive type. The storage form is always text.</summary>
public sealed record PropertySlot : SlotDeclaration
{
    public PropertySlot(string name, PropertyType type, string? defaultValue = null)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (defaultValue is not null && !type.IsValid(defaultValue))
        {
            throw new ArgumentException(
                $"'{defaultValue}' is not a valid value for a slot of type {type}.",
                nameof(defaultValue));
        }

        Type = type;
        DefaultValue = defaultValue;
    }

    public PropertyType Type { get; }

    /// <summary>
    /// The value a freshly created node starts with. <see langword="null"/> means the model
    /// has to supply one before the slot counts as filled.
    /// </summary>
    public string? DefaultValue { get; }
}

/// <summary>An owned subtree. Removing the child removes the node it contains.</summary>
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

    /// <summary>The declared concept; subconcepts of it are accepted too.</summary>
    public string ChildConceptKey { get; }

    public Cardinality Cardinality { get; }
}

/// <summary>A pointer to a node owned elsewhere, stored as target id plus resolve info.</summary>
public sealed record ReferenceSlot : SlotDeclaration
{
    public ReferenceSlot(
        string name,
        string targetConceptKey,
        Cardinality cardinality = Cardinality.One,
        bool allowOwnSubtree = false)
        : base(name)
    {
        if (!NameRules.IsValidConceptKey(targetConceptKey))
        {
            throw new ArgumentException($"'{targetConceptKey}' is not a valid concept key.", nameof(targetConceptKey));
        }

        TargetConceptKey = targetConceptKey;
        Cardinality = cardinality;
        AllowOwnSubtree = allowOwnSubtree;
    }

    /// <summary>The declared concept; subconcepts of it are accepted too.</summary>
    public string TargetConceptKey { get; }

    public Cardinality Cardinality { get; }

    /// <summary>
    /// Whether the slot may point into the subtree of the node that owns it. It may not by
    /// default, because such a reference makes removal and copying ambiguous.
    /// </summary>
    public bool AllowOwnSubtree { get; }
}
