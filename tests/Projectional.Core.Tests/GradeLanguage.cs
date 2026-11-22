using Projectional.Core;
using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>A small language whose whole meaning is a declared rule: a grade report that is pass or fail.</summary>
/// <remarks>
/// Its own fixture rather than a concept of the expression vocabulary, because an evaluator is interesting only on a shape
/// a real DSL has: one property a person fills in, and an answer the language owes them for it. The concept has no
/// operator property and no list, which is what makes it a proof that a rule can supply its own symbol.
/// </remarks>
internal static class GradeLanguage
{
    internal const string Report = "Grade.Report";
    internal const string PointsSlot = "points";
    internal const string NameSlot = "name";

    internal static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            Report,
            [
                new PropertySlot(NameSlot, PropertyType.String, description: "What the report is about."),
                new PropertySlot(PointsSlot, PropertyType.Integer, description: "The points the report scored."),
            ],
            description: "A report with a name and the points it scored."),
    ];

    internal static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    internal static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);

        notations.Add(Report, ed => ed.Line(
            ed.Keyword("report"),
            ed.Space,
            ed.Property(NameSlot),
            ed.Keyword("scored"),
            ed.Space,
            ed.Property(PointsSlot)));

        return notations;
    }
}
