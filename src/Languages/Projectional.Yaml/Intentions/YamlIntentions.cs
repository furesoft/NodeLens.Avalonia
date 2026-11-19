using Projectional.Core;
using Projectional.Editing;
using Projectional.Yaml;

namespace Projectional.Yaml.Intentions;

/// <summary>The two entries a YAML document is built with: one more member, one more item.</summary>
/// <remarks>
/// A YAML file is mostly mappings and sequences, and "one more line" is the operation a user performs all day. It is not
/// a gap the standard catalogue can fill: that catalogue derives what the *place* implies, and an empty list has no place
/// to derive from. Both entries climb to the nearest list the caret is inside, because a pair is always within the mapping
/// it belongs to, and they append through the named insertion since a list can end with a note.
/// </remarks>
public sealed class YamlIntentions : IIntentionProvider
{
    /// <summary>The stable id of "add a member".</summary>
    public const string AddMemberId = "yaml.add-member";

    /// <summary>The stable id of "add an item".</summary>
    public const string AddItemId = "yaml.add-item";

    private static readonly Intention AddMember = new(
        AddMemberId,
        "Add member",
        "Adds one more key/value line to this mapping.",
        context => OwnerOf(context, YamlLanguage.MembersSlot) is { } owner
            ? Append(owner, YamlLanguage.MembersSlot, YamlConceptKeys.Pair)
            : null);

    private static readonly Intention AddItem = new(
        AddItemId,
        "Add item",
        "Adds one more item to this sequence.",
        context => OwnerOf(context, YamlLanguage.ItemsSlot) is { } owner
            ? Append(owner, YamlLanguage.ItemsSlot, YamlConceptKeys.SequenceItem)
            : null);

    /// <summary>The provider a host registers with <c>session.AddIntentions(..., IntentionSource.Language)</c>.</summary>
    public static YamlIntentions Provider { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
        [
            AddMember.With(Availability(context, YamlLanguage.MembersSlot)),
            AddItem.With(Availability(context, YamlLanguage.ItemsSlot)),
        ];
    }

    private static IntentionAvailability Availability(IntentionContext context, string slot) =>
        OwnerOf(context, slot) is null ? IntentionAvailability.Absent : IntentionAvailability.Applies;

    /// <summary>The nearest block node that owns a many-valued <paramref name="slot"/>.</summary>
    /// <remarks>
    /// A flow collection has the same slot names but holds flow members, so only a block mapping or sequence answers:
    /// adding a block pair to a flow mapping would produce a document no reader accepts.
    /// </remarks>
    private static Node? OwnerOf(IntentionContext context, string slot)
    {
        for (var current = context.Node; current is not null; current = current.Parent)
        {
            if (current.Concept.Key is not (YamlConceptKeys.Mapping or YamlConceptKeys.Sequence))
            {
                continue;
            }

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
