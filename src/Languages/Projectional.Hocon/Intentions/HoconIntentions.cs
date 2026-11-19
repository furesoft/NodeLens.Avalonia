using Projectional.Core;
using Projectional.Editing;
using Projectional.Hocon;

namespace Projectional.Hocon.Intentions;

/// <summary>The two entries a HOCON document is built with: one more property, one more item.</summary>
/// <remarks>
/// <para>
/// A HOCON file is mostly objects and arrays, so "one more of these" is the operation a user performs all day. It is not
/// a gap the standard catalogue can fill: that catalogue derives what the *place* implies, and an empty collection has no
/// place to derive from - pressing Enter there creates what the slot's declarations say is most likely, which for
/// <c>Hocon.Member</c> is a property.
/// </para>
/// <para>
/// The entries climb to the nearest list they belong to, because a document draws nothing of its own and a pair is always
/// inside the object the list belongs to. They append through the named insertion, since a list of members can end with a
/// note or an include.
/// </para>
/// </remarks>
public sealed class HoconIntentions : IIntentionProvider
{
    /// <summary>The stable id of "add a property".</summary>
    public const string AddPropertyId = "hocon.add-property";

    /// <summary>The stable id of "add an item".</summary>
    public const string AddItemId = "hocon.add-item";

    private static readonly Intention AddProperty = new(
        AddPropertyId,
        "Add property",
        "Adds one more key/value pair to this object.",
        context => ListOwner(context, HoconLanguage.MembersSlot) is { } owner
            ? Append(owner, HoconLanguage.MembersSlot, HoconConceptKeys.Property)
            : null);

    private static readonly Intention AddItem = new(
        AddItemId,
        "Add item",
        "Adds one more value to this array.",
        context => ListOwner(context, HoconLanguage.ItemsSlot) is { } owner
            ? Append(owner, HoconLanguage.ItemsSlot, HoconConceptKeys.String)
            : null);

    /// <summary>The provider a host registers with <c>session.AddIntentions(..., IntentionSource.Language)</c>.</summary>
    public static HoconIntentions Provider { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
        [
            AddProperty.With(Availability(context, HoconLanguage.MembersSlot)),
            AddItem.With(Availability(context, HoconLanguage.ItemsSlot)),
        ];
    }

    private static IntentionAvailability Availability(IntentionContext context, string slot) =>
        ListOwner(context, slot) is null ? IntentionAvailability.Absent : IntentionAvailability.Applies;

    /// <summary>The nearest node that owns <paramref name="slot"/>, starting at the caret's own node.</summary>
    private static Node? ListOwner(IntentionContext context, string slot)
    {
        for (var current = context.Node; current is not null; current = current.Parent)
        {
            if (current.Concept.FindChild(slot) is { } declared && declared.Cardinality == Cardinality.Many)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>The edit that puts one more entry of <paramref name="conceptKey"/> at the end of the list.</summary>
    private static InsertNamedNodeAction Append(Node owner, string slot, string conceptKey) =>
        new(owner.Id, slot, owner.GetChildren(slot).Count, conceptKey);
}
