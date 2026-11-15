using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>
/// A body whose steps are drawn as a table, which is the smallest notation that needs more than one
/// dimension.
/// </summary>
/// <remarks>
/// The columns cover the three things a row can show - a property, a single child and nothing at all -
/// so one snapshot tests all three paths of the builder. The <c>detail</c> column is a child of the
/// row concept, which is what makes a table different from a collection: its cells name slots of
/// another concept.
/// </remarks>
internal static class TableNotations
{
    public static NotationRegistry Registry { get; } = Create();

    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        notations.Add(TestLanguage.Step, ed => ed.Line(ed.Property("text")));

        notations.Add(TestLanguage.Body, ed => ed.Table(
            "steps",
            ed.Column("text", ed.Keyword("step")),
            ed.Column("index", ed.Keyword("no")),
            ed.Column("detail", ed.Keyword("detail"))));

        return notations;
    }
}
