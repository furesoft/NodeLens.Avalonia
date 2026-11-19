using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;

namespace Projectional.Core.Tests;

/// <summary>The fixture language's intentions, declared once and filtered per caret.</summary>
/// <remarks>
/// <para>
/// This is the shape a language publishes: the entries are declared with everything that is true wherever
/// they appear, and <see cref="At"/> answers the same list with an availability per caret. That is what the
/// <see cref="IntentionAvailability.Absent"/> state exists for - a declared catalogue can be handed back
/// whole and let <see cref="IntentionList"/> filter it.
/// </para>
/// <para>
/// It lives in a folder of the test fixture's project rather than in a package of its own - the shape
/// [ADR 0021](../../docs/adr/0021-one-project-per-language.md) settled on: an intention produces an
/// <see cref="EditAction"/>, so it names the editing layer, and that behaviour belongs with the language it
/// edits.
/// </para>
/// </remarks>
internal sealed class TestIntentions : IIntentionProvider
{
    /// <summary>The id of the fixture's plain intention.</summary>
    internal const string RenameId = "test.rule.rename";

    /// <summary>The id of the fixture's destructive intention.</summary>
    internal const string DeleteId = "test.rule.delete";

    private static readonly Intention Rename = new(
        RenameId,
        "Rename to \"renamed\"",
        "Gives the rule a name you can type over.",
        context => context.Node is { } node && node.Concept.NameSlot is { } slot
            ? new SetPropertyAction(CellAddress.OfSlot(node.Id, slot.Name), "renamed")
            : null);

    private static readonly Intention Delete = new(
        DeleteId,
        "Delete this rule",
        "Removes the rule and everything inside it.",
        _ => new DeleteNodeAction(),
        safety: EditSafety.Destructive);

    /// <summary>The intentions the fixture language declares.</summary>
    internal static TestIntentions ForRules { get; } = new();

    /// <summary>The declared entries, marked applicable only where the caret stands on a rule.</summary>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        var availability = context.Node is { Concept.Key: TestLanguage.Rule }
            ? IntentionAvailability.Applies
            : IntentionAvailability.Absent;

        return [Rename.With(availability), Delete.With(availability)];
    }
}
