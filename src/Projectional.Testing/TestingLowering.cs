using System.Globalization;

using Projectional.Core;

namespace Projectional.Testing;

/// <summary>Turns a written test into the <see cref="LanguageTest"/> a runner runs.</summary>
/// <remarks>
/// <para>
/// It checks what a compiler would: the document names a concept of this language, names a document to assert
/// about, and names one of the assertions the framework knows how to run. The document path is deliberately not
/// checked against the file system here - the runner resolves it against the language project, and a test whose
/// document has gone is a failing row naming the path, not a language that refuses to load.
/// </para>
/// <para>
/// A test document that cannot be lowered is a broken suite, not a failing test: the runner throws naming the
/// file, because "the tests folder holds something that is not a test" is an authoring mistake to fix, not a
/// result to report.
/// </para>
/// </remarks>
public static class TestingLowering
{
    /// <summary>The test one document declares.</summary>
    /// <param name="document">The root of a tests document.</param>
    /// <returns>The test that document states.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">The document is not a test, or does not say enough.</exception>
    public static LanguageTest Lower(Node document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Concept.Key != TestingConceptKeys.Test)
        {
            throw new ConceptDefinitionException(
                $"'{document.Concept.Key}' is not a test; a tests document holds one '{TestingConceptKeys.Test}'.");
        }

        var asserted = document.GetOrNull(TestingLanguage.DocumentSlot);

        if (string.IsNullOrWhiteSpace(asserted))
        {
            throw new ConceptDefinitionException("A test has to name the document it is about.");
        }

        var children = document.GetChildren(TestingLanguage.AssertionSlot);

        if (children.Count != 1)
        {
            throw new ConceptDefinitionException(
                $"A test has to make exactly one assertion, but makes {children.Count}.");
        }

        var assertion = children[0].Concept.Key;

        var name = document.Model.Name ?? Path.GetFileNameWithoutExtension(asserted);
        var target = asserted.Trim();

        return assertion switch
        {
            TestingConceptKeys.RoundTrips => new LanguageTest(name, target, assertion),
            TestingConceptKeys.Draws => new LanguageTest(
                name,
                target,
                assertion,
                Width(children[0]),
                Lines(children[0]),
                Findings: null),
            TestingConceptKeys.Findings => new LanguageTest(
                name,
                target,
                assertion,
                Width: 0,
                Lines: null,
                Findings(children[0])),
            _ => throw new ConceptDefinitionException(
                $"'{assertion}' is not an assertion this framework runs."),
        };
    }

    /// <summary>The columns a drawing assertion renders into.</summary>
    private static int Width(Node draws)
    {
        var text = draws.GetOrNull(TestingLanguage.WidthSlot);

        if (string.IsNullOrWhiteSpace(text))
        {
            return 80;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) && width > 0
            ? width
            : throw new ConceptDefinitionException(
                $"'{text}' is not a number of columns; a drawing has to render into a positive width.");
    }

    /// <summary>The lines a drawing assertion expects, in order.</summary>
    private static IReadOnlyList<string> Lines(Node draws) =>
        [.. draws.GetChildren(TestingLanguage.LinesSlot).Select(line =>
            line.GetOrNull(TestingLanguage.TextSlot) ?? string.Empty)];

    /// <summary>The findings a findings assertion expects, in order.</summary>
    private static IReadOnlyList<ExpectedFinding> Findings(Node findings) =>
        [.. findings.GetChildren(TestingLanguage.ExpectedSlot).Select(finding => new ExpectedFinding(
            Required(finding, TestingLanguage.CodeSlot),
            Required(finding, TestingLanguage.SeveritySlot),
            finding.GetOrNull(TestingLanguage.RoleSlot)?.Trim() ?? string.Empty))];

    private static string Required(Node node, string slot)
    {
        var value = node.GetOrNull(slot);

        return string.IsNullOrWhiteSpace(value)
            ? throw new ConceptDefinitionException($"'{node.Concept.Key}' has to fill '{slot}'.")
            : value.Trim();
    }
}
