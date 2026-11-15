using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>
/// A language whose notes can be read two ways, which is the smallest thing that needs a variant.
/// </summary>
/// <remarks>
/// The two variants differ only in text, so a test that asks for one of them is testing the choice and
/// not a new cell kind. The note keeps its <c>text</c> property in both, which is what lets a test
/// check that the same address is reachable through either representation.
/// </remarks>
internal static class VariantNotations
{
    /// <summary>The variant that reads a note as a bracketed assertion instead of a sentence.</summary>
    public const string Bracketed = "bracketed";

    /// <summary>A variant name no notation declares.</summary>
    public const string Unknown = "hologram";

    public static NotationRegistry Registry { get; } = Create();

    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        notations.Add(TestLanguage.Note, ed => ed.Line(
            ed.Keyword("note"),
            ed.Space,
            ed.Property("text")));

        notations.Add(TestLanguage.Note, Bracketed, ed => ed.Line(
            ed.Punct("("),
            ed.Property("text"),
            ed.Punct(")")));

        notations.Add(TestLanguage.Body, ed => ed.Collection("notes", separator: ed.Punct(", ")));

        return notations;
    }
}
