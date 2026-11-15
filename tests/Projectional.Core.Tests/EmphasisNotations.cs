using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>
/// A notation of <see cref="TestLanguage"/> that carries emphasis on the cells it declares.
/// </summary>
/// <remarks>
/// A language author chooses emphasis once, in the notation, because it is part of how the concept
/// always reads: the keyword is bold, the step text is italic. This registry is that statement for
/// the tests, kept beside <see cref="TestNotations"/> so a test can compare the two.
/// </remarks>
internal static class EmphasisNotations
{
    public static NotationRegistry Registry { get; } = Create();

    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        notations.Add(TestLanguage.Rule, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("rule", TextEmphasis.Bold),
                ed.Space,
                ed.Property("name")),
            ed.Indent(ed.Line(ed.ChildInline("body")))));

        notations.Add(TestLanguage.Body, ed => ed.Sequence(
            ed.Children("steps")));

        notations.Add(TestLanguage.Step, ed => ed.Line(
            ed.Property("text", TextEmphasis.Italic)));

        return notations;
    }
}
