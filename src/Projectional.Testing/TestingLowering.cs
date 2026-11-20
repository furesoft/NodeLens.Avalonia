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

        if (assertion is not TestingConceptKeys.RoundTrips)
        {
            throw new ConceptDefinitionException(
                $"'{assertion}' is not an assertion this framework runs.");
        }

        return new LanguageTest(
            document.Model.Name ?? Path.GetFileNameWithoutExtension(asserted),
            asserted.Trim(),
            assertion);
    }
}
