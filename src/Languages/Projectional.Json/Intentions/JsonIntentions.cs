using Projectional.Core;
using Projectional.Editing;
using Projectional.Json;

namespace Projectional.Json.Intentions;

/// <summary>The two entries a JSON document is built with: one more member, one more item.</summary>
/// <remarks>
/// <para>
/// A JSON document is mostly containers, so "one more of these" is the operation a user performs all day. It is not a
/// gap the standard catalogue can fill: that catalogue derives what the *place* implies (delete, move, wrap, replace),
/// and an empty collection has no place to derive from - pressing Enter there creates what the slot's declarations say
/// is most likely, which for `Json.Value` is an object. These entries say "one more member" and "one more item" in the
/// words of the format instead.
/// </para>
/// <para>
/// The intentions of JSON live in this language's own project (`Intentions/`, namespace unchanged): an intention
/// produces an <see cref="EditAction"/>, so it names the editing layer, and the behaviour of a language belongs with
/// the language ([ADR 0021](../../../docs/adr/0021-one-project-per-language.md)).
/// </para>
/// <para>
/// Each entry appends: <see cref="InsertChildAction"/> while the slot holds nothing, <see cref="InsertSiblingAction"/>
/// after the last entry once it holds one.
/// </para>
/// </remarks>
public sealed class JsonIntentions : IIntentionProvider
{
    /// <summary>The stable id of "add a member".</summary>
    public const string AddMemberId = "json.add-member";

    /// <summary>The stable id of "add an item".</summary>
    public const string AddItemId = "json.add-item";

    private static readonly Intention AddMember = new(
        AddMemberId,
        "Add member",
        "Adds one more member to this object, after the ones it already has.",
        context => context.Node is { } node
            ? Append(node, JsonLanguage.MembersSlot)
            : null);

    private static readonly Intention AddItem = new(
        AddItemId,
        "Add item",
        "Adds one more item to this array, after the ones it already has.",
        context => context.Node is { } node
            ? Append(node, JsonLanguage.ItemsSlot)
            : null);

    /// <summary>The provider a host registers with <c>session.AddIntentions(..., IntentionSource.Language)</c>.</summary>
    public static JsonIntentions Provider { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
        [
            AddMember.With(AvailabilityOf(context, JsonConceptKeys.Object, JsonLanguage.MembersSlot)),
            AddItem.With(AvailabilityOf(context, JsonConceptKeys.Array, JsonLanguage.ItemsSlot)),
        ];
    }

    /// <summary>Whether the caret's node is a container with <paramref name="slot"/> to grow.</summary>
    /// <remarks>
    /// Absent rather than disabled where it would mean nothing: a scalar has no members, an object has no items, and a
    /// container always has room for one more of its own kind.
    /// </remarks>
    private static IntentionAvailability AvailabilityOf(IntentionContext context, string conceptKey, string slot)
    {
        if (context.Node is not { } node || !node.Concept.IsKindOf(conceptKey))
        {
            return IntentionAvailability.Absent;
        }

        return node.Concept.FindChild(slot) is not null
            ? IntentionAvailability.Applies
            : IntentionAvailability.Absent;
    }

    /// <summary>The edit that puts one more entry into <paramref name="slot"/> of <paramref name="owner"/>.</summary>
    private static EditAction Append(Node owner, string slot)
    {
        var existing = owner.GetChildren(slot);

        return existing.Count == 0
            ? new InsertChildAction(owner.Id, slot)
            : new InsertSiblingAction(existing[^1].Id, SiblingPlace.After);
    }
}
