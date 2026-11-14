using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>
/// The notation of <see cref="TestLanguage"/>, written the way a language author would write it.
/// </summary>
/// <remarks>
/// The projection tests need a notation that exercises every cell kind, and they need the same one
/// every time, so it lives here rather than in each test. It is deliberately a plausible little
/// language: something a reviewer can read as a notation, not as a test fixture.
/// </remarks>
internal static class TestNotations
{
    public static NotationRegistry Registry { get; } = Create();

    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        notations.Add(TestLanguage.Rule, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("rule"),
                ed.Space,
                ed.Property("name"),
                ed.Space,
                ed.Keyword("as"),
                ed.Space,
                ed.Reference("tag"),
                ed.Space,
                ed.Punct("{")),
            ed.Indent(
                ed.Line(ed.ChildInline("body")),
                ed.Line(
                    ed.Keyword("extends"),
                    ed.Space,
                    ed.ChildInline("extends"))),
            ed.Line(ed.Punct("}"))));

        notations.Add(TestLanguage.Body, ed => ed.Sequence(
            ed.Children("steps"),
            ed.Collection("notes", separator: ed.Punct(";"))));

        notations.Add(TestLanguage.Step, ed => ed.Line(
            ed.Property("index"),
            ed.Space,
            ed.Punct(":"),
            ed.Space,
            ed.Property("text"),
            ed.Space,
            ed.ChildInline("detail")));

        notations.Add(TestLanguage.Note, ed => ed.Line(
            ed.Keyword("note"),
            ed.Space,
            ed.Property("text")));

        return notations;
    }
}
