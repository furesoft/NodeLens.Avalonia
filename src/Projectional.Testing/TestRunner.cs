using Projectional.Core;
using Projectional.Languages;
using Projectional.Persistence;

namespace Projectional.Testing;

/// <summary>Runs the tests a language project ships in its <c>tests/</c> folder.</summary>
/// <remarks>
/// <para>
/// This is the behaviour package's job, not the language's: a runner reads the documents, the assertions and the
/// persistence layer, and a language does not depend on its own tests. A host calls <see cref="Run"/> and gets a
/// <see cref="TestReport"/>; the xunit bridge in the repository's own suite is one such host, and a workbench
/// command can be another without the runner knowing either exists.
/// </para>
/// <para>
/// The folder is read in ordinal order of the file names, so a diff of one test is a diff of one row. A language
/// with no tests folder answers <see cref="TestReport.Empty"/> rather than failing: not every language has tests
/// yet, and "none" is a fact, not a mistake.
/// </para>
/// </remarks>
public static class TestRunner
{
    /// <summary>The folder, beside the aspect folders, that holds a language's tests.</summary>
    public const string Folder = "tests";

    /// <summary>Runs every test document in <paramref name="language"/>'s tests folder.</summary>
    /// <param name="language">The language project whose tests folder to run.</param>
    /// <returns>The rows of the run, in file-name order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="language"/> is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">A test document is not a test, or does not say enough.</exception>
    /// <exception cref="ModelFormatException">A test document is not a readable model.</exception>
    /// <remarks>
    /// A test document that cannot be read or lowered throws, naming the file: a broken suite is an authoring
    /// mistake to fix, not a result to report. A document <em>under</em> test that cannot be read is a failed row,
    /// because the test itself is well formed and its subject is not.
    /// </remarks>
    public static TestReport Run(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        var folder = Path.Combine(language.Directory, Folder);

        if (!Directory.Exists(folder))
        {
            return TestReport.Empty;
        }

        var files = Directory
            .EnumerateFiles(folder, "*" + ModelFile.Extension, SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToList();

        var results = new List<TestResult>(files.Count);

        foreach (var file in files)
        {
            results.Add(RunOne(language, file));
        }

        return new TestReport(results);
    }

    /// <summary>Runs the one test a document states.</summary>
    private static TestResult RunOne(LanguageProject language, string file)
    {
        var document = ModelFile.Load(TestingLanguage.NewProject(), file);

        if (document.Roots.Count != 1)
        {
            throw new ConceptDefinitionException(
                $"'{Path.GetFileName(file)}' holds {document.Roots.Count} definition(s); a tests document holds exactly one test.");
        }

        var test = TestingLowering.Lower(document.Roots[0]);

        var path = Path.Combine(language.Directory, test.Document);

        Model model;

        try
        {
            model = ModelFile.Load(language.NewProject(), path);
        }
        catch (Exception exception) when (exception is ModelFormatException or IOException or UnauthorizedAccessException)
        {
            return TestResult.Failed(test.Test, test.Document, test.AssertionName, expected: null, actual: exception.Message);
        }

        return test.Assertion switch
        {
            TestingConceptKeys.RoundTrips => RoundTrips(language, test, model),
            var other => throw new ConceptDefinitionException(
                $"'{other}' is not an assertion this framework runs."),
        };
    }

    /// <summary>Whether the document is written and read back to the same text.</summary>
    /// <remarks>
    /// The assertion is invariant 7 in miniature: a model that survives a write and a read unchanged. It is a text
    /// comparison rather than a byte comparison because a model file is text; the bytes are the text's encoding,
    /// and the invariant is about the model, not the encoding.
    /// </remarks>
    private static TestResult RoundTrips(LanguageProject language, LanguageTest test, Model model)
    {
        var written = ModelXml.Serialize(model);
        var reloaded = ModelXml.Deserialize(language.NewProject(), written);
        var rewritten = ModelXml.Serialize(reloaded);

        return written == rewritten
            ? TestResult.Passed(test.Test, test.Document, test.AssertionName)
            : TestResult.Failed(test.Test, test.Document, test.AssertionName, written, rewritten);
    }
}
