namespace Projectional.Core;

/// <summary>
/// A slot holding a single primitive value. Properties are the leaves of the model: they are
/// stored inline, never as nodes, so that editing text does not mint identity.
/// </summary>
public sealed record PropertySlot : SlotDeclaration
{
    public PropertySlot(string name, PropertyType type, string? defaultValue = null, string? description = null)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (defaultValue is not null && !type.IsValid(defaultValue))
        {
            throw new ArgumentException(
                $"'{defaultValue}' is not a valid {type.Name} value for slot '{name}'.",
                nameof(defaultValue));
        }

        if (description is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        }

        Type = type;
        DefaultValue = defaultValue;
        Description = description;
    }

    public PropertyType Type { get; }

    /// <summary>
    /// The value a freshly created node starts with. <see langword="null"/> means the model has
    /// to supply one before the slot counts as filled.
    /// </summary>
    public string? DefaultValue { get; }

    /// <summary>
    /// What the language says about this slot, shown as a tooltip while it is still empty.
    /// </summary>
    /// <remarks>
    /// A property is the one place where "empty" is a visible state - the notation draws the value's cell even
    /// when there is no value - so it is the one place where documentation can meet the moment it is needed.
    /// <see langword="null"/> means the language says nothing, and no box appears.
    /// </remarks>
    public string? Description { get; }
}
