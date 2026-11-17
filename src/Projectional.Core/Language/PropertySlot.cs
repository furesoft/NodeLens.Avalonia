namespace Projectional.Core;

/// <summary>
/// A slot holding a single primitive value. Properties are the leaves of the model: they are
/// stored inline, never as nodes, so that editing text does not mint identity.
/// </summary>
public sealed record PropertySlot : SlotDeclaration
{
    public PropertySlot(
        string name,
        PropertyType type,
        string? defaultValue = null,
        string? description = null,
        bool showDescriptionWhenFilled = false)
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

        if (showDescriptionWhenFilled && description is null)
        {
            throw new ArgumentException(
                $"Slot '{name}' cannot show a description over its value because it declares none.",
                nameof(showDescriptionWhenFilled));
        }

        Type = type;
        DefaultValue = defaultValue;
        Description = description;
        ShowDescriptionWhenFilled = showDescriptionWhenFilled;
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
    /// <see langword="null"/> means the language says nothing, and no box appears. A slot whose value does not
    /// explain itself asks for the sentence over a filled value too, with
    /// <see cref="ShowDescriptionWhenFilled"/>.
    /// </remarks>
    public string? Description { get; }

    /// <summary>
    /// Whether <see cref="Description"/> is also shown on hover while the slot holds a value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An empty slot always shows its sentence when it declares one, because an empty box is a question the user
    /// is asking. A filled slot is the opposite case: the value is the answer, and a box over it would be in the
    /// way of reading it. A language therefore opts in per slot, for the values whose text does not explain
    /// itself - an identifier, an operator, a key the notation never spells out.
    /// </para>
    /// <para>
    /// It is off by default so that documentation on a slot does not start covering text that already says what
    /// it is. The flag without a <see cref="Description"/> is refused at construction: a slot that asks for a box
    /// it can never show is a language mistake, not a setting without an effect.
    /// </para>
    /// </remarks>
    public bool ShowDescriptionWhenFilled { get; }
}
