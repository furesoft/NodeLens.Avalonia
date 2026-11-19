using Projectional.Core;
using Projectional.Editing;
using Projectional.Toml;

namespace Projectional.Toml.Intentions;

/// <summary>The three entries a TOML document is built with: a pair, a table and an array of tables.</summary>
/// <remarks>
/// <para>
/// A TOML file is written by adding one entry after another, and the three kinds of entry are what a user reaches for.
/// It is not a gap the standard catalogue can fill: that catalogue derives what the *place* implies (delete, move, wrap,
/// replace), and an empty list has no place to derive from - pressing Enter there creates what the slot's declarations
/// say is most likely, which is a pair. These entries say "a table" and "an array of tables" in the words of the format
/// instead.
/// </para>
/// <para>
/// They are offered on a document, a table and an array of tables, because all three hold entries - and they append:
/// <see cref="InsertChildAction"/> while the slot holds nothing, <see cref="InsertSiblingAction"/> after the last entry
/// once it holds one.
/// </para>
/// </remarks>
public sealed class TomlIntentions : IIntentionProvider
{
    /// <summary>The stable id of "add a pair".</summary>
    public const string AddPairId = "toml.add-pair";

    /// <summary>The stable id of "add a table".</summary>
    public const string AddTableId = "toml.add-table";

    /// <summary>The stable id of "add an array of tables".</summary>
    public const string AddArrayOfTablesId = "toml.add-array-of-tables";

    private static readonly Intention AddPair = new(
        AddPairId,
        "Add pair",
        "Adds one more key/value pair to this table.",
        context => ContainerOf(context.Node) is { } node ? Append(node, TomlConceptKeys.KeyValue) : null);

    private static readonly Intention AddTable = new(
        AddTableId,
        "Add table",
        "Adds a header in single brackets, with the entries written under it.",
        context => ContainerOf(context.Node) is { } node ? Append(node, TomlConceptKeys.Table) : null);

    private static readonly Intention AddArrayOfTables = new(
        AddArrayOfTablesId,
        "Add array of tables",
        "Adds a header in double brackets, which is one element of a list of tables.",
        context => ContainerOf(context.Node) is { } node ? Append(node, TomlConceptKeys.ArrayOfTables) : null);

    /// <summary>The provider a host registers with <c>session.AddIntentions(..., IntentionSource.Language)</c>.</summary>
    public static TomlIntentions Provider { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (ContainerOf(context.Node) is not { } container)
        {
            return
            [
                AddPair.With(IntentionAvailability.Absent),
                AddTable.With(IntentionAvailability.Absent),
                AddArrayOfTables.With(IntentionAvailability.Absent),
            ];
        }

        var availability = IntentionAvailability.Applies;

        return
        [
            AddPair.With(availability),
            AddTable.With(availability),
            AddArrayOfTables.With(availability),
        ];
    }

    /// <summary>The nearest node that holds entries, starting at the caret's own node.</summary>
    /// <remarks>
    /// A document draws nothing of its own, so a caret is always inside one of its entries - and inside an inline table's
    /// member the nearest list of entries is the table the pair belongs to. Climbing to it is what makes "add a table"
    /// reachable while the user is editing a pair, which is where they are when they want one.
    /// </remarks>
    private static Node? ContainerOf(Node? node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.Concept.FindChild(TomlLanguage.EntriesSlot) is not null)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>The edit that puts one more entry of <paramref name="conceptKey"/> at the end of the node's list.</summary>
    /// <remarks>
    /// The action is the named insertion rather than Enter's default one: a document holds pairs, headers and notes, so
    /// "add a table" has to name the table instead of copying whatever stands last.
    /// </remarks>
    private static InsertNamedNodeAction Append(Node owner, string conceptKey) =>
        new(owner.Id, TomlLanguage.EntriesSlot, owner.GetChildren(TomlLanguage.EntriesSlot).Count, conceptKey);
}
