namespace Projectional.Language;

/// <summary>A property of the node that holds the node being expanded.</summary>
/// <remarks>
/// <para>
/// <see cref="PropertyPart"/> reads a property of the node itself; this reads one of the node that holds it - the
/// table a foreign key belongs to, the rule a step stands in. A template that walks into a child loses the child's
/// place, so without this part a fragment cannot name what it belongs to: the DDL generator could write the columns a
/// key points at but never the table the key hangs on.
/// </para>
/// <para>
/// The slot is named, not inferred, because every other part names a slot too and a role such as "the name" is a
/// property the parent's concept happens to declare - <c>ParentPart("name")</c> is how a name is read. A concept
/// whose name slot has another name is asked for that name.
/// </para>
/// <para>
/// The outermost node has no holding node, so the part emits nothing there: generating a subtree is an ordinary thing
/// to do, and a failure would refuse a template that is right for every node below the one it cannot ask about.
/// </para>
/// </remarks>
public sealed record ParentPart : TemplatePart
{
    /// <summary>Creates a part that emits the property <paramref name="slot"/> of the holding node.</summary>
    /// <param name="slot">The property slot to read, as the holding node's concept declares it.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public ParentPart(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        Slot = slot;
    }

    /// <summary>The property slot of the holding node.</summary>
    public string Slot { get; }

    public override string ToString() => $"the holding node's {Slot}";
}
