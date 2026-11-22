using Projectional.Core;
using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>A language with a list of entries, which is the shape a <c>first</c> body walks.</summary>
/// <remarks>
/// Its own fixture rather than a concept of the expression vocabulary, which has no list to walk: the evaluator language has
/// to be provable on a shape a real DSL has, not only on the one language this repository happens to ship. The same reason
/// <see cref="ExtractLanguage"/> exists for the editing layer.
/// </remarks>
internal static class FirstLanguage
{
    internal const string Container = "First.Container";
    internal const string Entry = "First.Entry";
    internal const string EntriesSlot = "entries";
    internal const string EnabledSlot = "enabled";
    internal const string OutcomeSlot = "outcome";

    internal static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            Container,
            [new ChildSlot(EntriesSlot, Entry, Cardinality.Many)],
            description: "Something holding a list of entries."),

        new ConceptDeclaration(
            Entry,
            [
                new PropertySlot(EnabledSlot, PropertyType.Boolean, description: "Whether this entry applies."),
                new PropertySlot(OutcomeSlot, PropertyType.Integer, description: "The number it answers with."),
            ],
            description: "One entry: whether it applies, and what it answers."),
    ];

    internal static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    internal static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);

        notations.Add(Container, ed => ed.Sequence(
            ed.Line(ed.Keyword("entries")),
            ed.Indent(ed.Children(EntriesSlot))));

        notations.Add(Entry, ed => ed.Line(
            ed.Property(EnabledSlot),
            ed.Space,
            ed.Property(OutcomeSlot)));

        return notations;
    }
}
