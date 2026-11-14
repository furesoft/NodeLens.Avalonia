namespace Projectional.Core;

/// <summary>
/// A slot holding a single primitive value. Properties are the leaves of the model: they are
/// stored inline, never as nodes, so that editing text does not mint identity.
/// </summary>
public sealed record PropertySlot : SlotDeclaration
{
    public PropertySlot(string name, PropertyType type, string? defaultValue = null)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (defaultValue is not null && !type.IsValid(defaultValue))
        {
            throw new ArgumentException(
                $"'{defaultValue}' is not a valid {type.Name} value for slot '{name}'.",
                nameof(defaultValue));
        }

        Type = type;
        DefaultValue = defaultValue;
    }

    public PropertyType Type { get; }

    /// <summary>
    /// The value a freshly created node starts with. <see langword="null"/> means the model has
    /// to supply one before the slot counts as filled.
    /// </summary>
    public string? DefaultValue { get; }
}
