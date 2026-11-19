using Projectional.Core;
using Projectional.Editing;
using Projectional.Xml;

namespace Projectional.Xml.Intentions;

/// <summary>The two entries XML owes its own notation: add an attribute, add a child element.</summary>
/// <remarks>
/// <para>
/// They are not a convenience. The notation hides both collections while they are empty, because in XML an empty slot
/// cannot be drawn as a blank - whitespace inside an element is content, and a placeholder would make an empty element
/// indistinguishable from one holding a space. Hiding a slot is a pair, and this is the other half: without these two
/// entries the first attribute and the first child of an element would have no gesture at all.
/// </para>
/// <para>
/// A package beside the language rather than inside it, for the reason <c>Projectional.Expressions.Intentions</c> gives:
/// an intention produces an <see cref="EditAction"/>, so it needs the editing layer, and a language package that dragged
/// the editor in could no longer be used headlessly.
/// </para>
/// <para>
/// Each entry appends: an <see cref="InsertChildAction"/> while the slot holds nothing (the only form that works on an
/// empty slot), and an <see cref="InsertSiblingAction"/> after the last entry once it has one. A member of a filled list
/// is where the caret usually is, so the second half is what makes the entry feel like "one more of these" rather than
/// like a special case.
/// </para>
/// </remarks>
public sealed class XmlIntentions : IIntentionProvider
{
    /// <summary>The stable id of "add an attribute".</summary>
    public const string AddAttributeId = "xml.add-attribute";

    /// <summary>The stable id of "add a child element".</summary>
    public const string AddChildId = "xml.add-child";

    private static readonly Intention AddAttribute = new(
        AddAttributeId,
        "Add attribute",
        "Adds an attribute to this element - the slot its notation cannot draw while it is empty.",
        context => context.Node is { } node
            ? Append(node, XmlLanguage.AttributesSlot, XmlConceptKeys.Attribute)
            : null);

    private static readonly Intention AddChild = new(
        AddChildId,
        "Add child element",
        "Adds an element to this element's content - the slot its notation cannot draw while it is empty.",
        context => context.Node is { } node
            ? Append(node, XmlLanguage.ChildrenSlot, XmlConceptKeys.Element)
            : null);

    /// <summary>The provider a host registers with <c>session.AddIntentions(..., IntentionSource.Language)</c>.</summary>
    public static XmlIntentions Provider { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
        [
            AddAttribute.With(AvailabilityOf(context, XmlLanguage.AttributesSlot)),
            AddChild.With(AvailabilityOf(context, XmlLanguage.ChildrenSlot)),
        ];
    }

    /// <summary>Whether this element can take one more of <paramref name="slot"/>'s children.</summary>
    /// <remarks>
    /// Absent rather than disabled everywhere it would mean nothing: off an element, and off a slot the element does not
    /// declare. Presence is the whole test - both slots are many-valued, so an element always has room for one more.
    /// </remarks>
    private static IntentionAvailability AvailabilityOf(IntentionContext context, string slot)
    {
        if (context.Node is not { } node || !node.Concept.IsKindOf(XmlConceptKeys.Element))
        {
            return IntentionAvailability.Absent;
        }

        return node.Concept.FindChild(slot) is not null
            ? IntentionAvailability.Applies
            : IntentionAvailability.Absent;
    }

    /// <summary>The edit that puts one more <paramref name="conceptKey"/> into <paramref name="slot"/>.</summary>
    private static EditAction Append(Node owner, string slot, string conceptKey)
    {
        var existing = owner.GetChildren(slot);

        return existing.Count == 0
            ? new InsertChildAction(owner.Id, slot)
            : new InsertSiblingAction(existing[^1].Id, SiblingPlace.After);
    }
}
