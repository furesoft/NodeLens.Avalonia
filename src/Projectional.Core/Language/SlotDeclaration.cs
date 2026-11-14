namespace Projectional.Core;

/// <summary>
/// One named part of a concept. A slot is either a property, an owned child list or a
/// reference to another node; the three kinds differ in how they are stored, changed and
/// undone, which is why they are separate types instead of a mode flag.
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

    public override string ToString() => Name;
}
