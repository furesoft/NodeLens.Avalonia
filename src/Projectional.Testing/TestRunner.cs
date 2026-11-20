using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Semantics;

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
    /// <param name="rules">
    /// The language's evaluation rules, or <see langword="null"/> when the caller has none to offer.
    /// </param>
    /// <returns>The rows of the run, in file-name order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="language"/> is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">A test document is not a test, or does not say enough.</exception>
    /// <exception cref="ModelFormatException">A test document is not a readable model.</exception>
    /// <remarks>
    /// <para>
    /// A test document that cannot be read or lowered throws, naming the file: a broken suite is an authoring
    /// mistake to fix, not a result to report. A document <em>under</em> test that cannot be read is a failed row,
    /// because the test itself is well formed and its subject is not.
    /// </para>
    /// <para>
    /// The rules are the caller's to pass because a language project does not carry an evaluation aspect: the
    /// interpreter's rules are written in code, and which registry belongs to which language is a fact the host
    /// knows and a <c>tests/</c> folder cannot state. An <c>evaluates</c> assertion run without one is a failed row
    /// naming what it needed, so a host that forgets to pass them sees every such test fail rather than silently
    /// pass - and the other assertions still report.
    /// </para>
    /// </remarks>
    public static TestReport Run(LanguageProject language, EvaluationRegistry? rules = null)
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
            results.Add(RunOne(language, file, rules));
        }

        return new TestReport(results);
    }

    /// <summary>Runs the one test a document states.</summary>
    private static TestResult RunOne(LanguageProject language, string file, EvaluationRegistry? rules)
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
            TestingConceptKeys.Draws => Draws(language, test, model),
            TestingConceptKeys.Findings => Findings(language, test, model),
            TestingConceptKeys.Generates => Generates(language, test, model),
            TestingConceptKeys.Imports => Imports(language, test, model),
            TestingConceptKeys.Evaluates => Evaluates(test, model, rules),
            TestingConceptKeys.Script => Script(language, test, model),
            TestingConceptKeys.TypeOf => TypeOf(language, test, model),
            TestingConceptKeys.ScopeOf => ScopeOf(language, test, model),
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

    /// <summary>Whether the document draws as the text the test spells out.</summary>
    /// <remarks>
    /// Exact text is honest and brittle: a change of measure or width rewrites every expectation. That is why the
    /// row carries both texts - the report shows the diff rather than "not equal", so a width change reads as the
    /// lines it moved.
    /// </remarks>
    private static TestResult Draws(LanguageProject language, LanguageTest test, Model model)
    {
        var expected = string.Join('\n', test.Lines ?? []);
        var actual = LayoutSnapshot.Render(model, language.Notations, test.Width);

        return expected == actual
            ? TestResult.Passed(test.Test, test.Document, test.AssertionName)
            : TestResult.Failed(test.Test, test.Document, test.AssertionName, expected, actual);
    }

    /// <summary>Whether the document reports exactly the findings the test lists, in order.</summary>
    /// <remarks>
    /// The findings are the language's own declared constraints, run through the engine a session runs. A host's
    /// check catalogue is not part of a language project, so a test that pins those is the host's to write; the
    /// language's own rules are what a language author can state and this assertion can reach.
    /// <para>
    /// "The engine a session runs" includes the language's scopes and types: a rule that asks a slot for its type
    /// sees <c>unknown</c> without them, so a type-dependent complaint would stay silent and this assertion would
    /// pin the silence as if it were the answer. The scopes and types are the language's own, for the same reason
    /// the constraints are.
    /// </para>
    /// </remarks>
    private static TestResult Findings(LanguageProject language, LanguageTest test, Model model)
    {
        var expected = string.Join('\n', (test.Findings ?? []).Select(finding => finding.ToLine()));
        var actual = string.Join(
            '\n',
            new ConstraintEngine(language.Constraints, scopes: language.Scopes, types: language.Types)
                .Check(model)
                .Select(finding => new ExpectedFinding(
                    finding.Code ?? string.Empty,
                    finding.Severity.ToString().ToLowerInvariant(),
                    finding.Role ?? string.Empty).ToLine()));

        return expected == actual
            ? TestResult.Passed(test.Test, test.Document, test.AssertionName)
            : TestResult.Failed(test.Test, test.Document, test.AssertionName, expected, actual);
    }

    /// <summary>Whether the document generates as the text the test spells out.</summary>
    /// <remarks>
    /// <para>
    /// The generator is named rather than assumed, because a language may declare several - the same rule table as
    /// C# and as JSON - and "the" generator is a choice the test makes. A name the language does not declare is a
    /// failed row listing the names it could have meant: the test is well formed, the language is not what the test
    /// thinks it is, and reporting that beside the other rows is more useful than throwing on the first one.
    /// </para>
    /// <para>
    /// A run that stopped mid-document reports the failure's message as the actual text. Half a file compared
    /// against the expectation would read as a diff of the wrong kind - the difference a reader has to fix is that
    /// generation stopped, and where.
    /// </para>
    /// </remarks>
    /// <summary>Whether the text the document generates reads back to a document that generates the same text.</summary>
    /// <remarks>
    /// The assertion is the round trip and nothing else: the importer is the inverse of the generator, so a template
    /// that writes a spelling it cannot read fails here rather than in a user's file. A generator with no inverse is a
    /// failed row naming why, because a language that cannot read back what it prints is exactly what this asserts.
    /// </remarks>
    private static TestResult Imports(LanguageProject language, LanguageTest test, Model model)
    {
        if (SingleRoot(model) is not { } root)
        {
            return NotASingleRoot(test, model);
        }

        var generator = language.Generators
            .FirstOrDefault(candidate => string.Equals(candidate.Name, test.Generator, StringComparison.Ordinal));

        if (generator is null)
        {
            return TestResult.Failed(
                test.Test,
                test.Document,
                test.AssertionName,
                expected: test.Generator ?? string.Empty,
                actual: $"no generator named '{test.Generator}'; '{language.Name}' declares {string.Join(", ", language.Generators.Select(candidate => candidate.Name))}");
        }

        if (!language.TryCreateImporter(generator.Name, out var importer, out var error))
        {
            return TestResult.Failed(
                test.Test,
                test.Document,
                test.AssertionName,
                expected: $"{generator.Name} reads back",
                actual: error ?? string.Empty);
        }

        var written = new Generator().Generate(root, generator);

        if (written.Failure is { } failure)
        {
            return TestResult.Failed(test.Test, test.Document, test.AssertionName, expected: null, actual: failure.Message);
        }

        var target = language.NewProject().NewModel("imported");
        var imported = importer.Import(target, written.Text);

        if (!imported.Succeeded || imported.Roots.Count != 1)
        {
            return TestResult.Failed(
                test.Test,
                test.Document,
                test.AssertionName,
                expected: written.Text,
                actual: string.Join('\n', imported.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }

        var again = new Generator().Generate(imported.Roots[0], generator);

        return string.Equals(again.Text, written.Text, StringComparison.Ordinal)
            ? TestResult.Passed(test.Test, test.Document, test.AssertionName)
            : TestResult.Failed(test.Test, test.Document, test.AssertionName, written.Text, again.Text);
    }

    private static TestResult Generates(LanguageProject language, LanguageTest test, Model model)
    {
        var generator = language.Generators
            .FirstOrDefault(candidate => string.Equals(candidate.Name, test.Generator, StringComparison.Ordinal));

        if (generator is null)
        {
            return TestResult.Failed(
                test.Test,
                test.Document,
                test.AssertionName,
                test.Generator,
                language.HasGenerators
                    ? $"'{language.Name}' declares {string.Join(", ", language.Generators.Select(candidate => candidate.Name))}"
                    : $"'{language.Name}' declares no generator");
        }

        if (SingleRoot(model) is not { } root)
        {
            return NotASingleRoot(test, model);
        }

        var expected = string.Join('\n', test.Lines ?? []);
        var generated = new Generator().Generate(root, generator);
        var actual = generated.Succeeded ? generated.Text : generated.Failure!.Message;

        return expected == actual
            ? TestResult.Passed(test.Test, test.Document, test.AssertionName)
            : TestResult.Failed(test.Test, test.Document, test.AssertionName, expected, actual);
    }

    /// <summary>Whether the document evaluates to the answer or the failure the test spells out.</summary>
    /// <remarks>
    /// <para>
    /// One expected text, because a run produces one text: the value's display when it succeeded, the failure's
    /// message when it did not. A test that pins a failure therefore writes the message, and a test that pins an
    /// answer finds the failure in the diff when the run stops - the report shows what happened either way, which
    /// is what a reader needs and what a second field would only obscure.
    /// </para>
    /// <para>
    /// The variables are read by <see cref="VariableInputs"/>, the same parser the editor's run box uses, so a run
    /// in a test and a run in the shell cannot disagree about what <c>1500</c> means.
    /// </para>
    /// </remarks>
    private static TestResult Evaluates(LanguageTest test, Model model, EvaluationRegistry? rules)
    {
        if (rules is null)
        {
            return TestResult.Failed(
                test.Test,
                test.Document,
                test.AssertionName,
                test.Outcome,
                "no evaluation rules were supplied, and this assertion needs them");
        }

        if (!VariableInputs.TryParse(test.Variables, out var variables, out var error))
        {
            // Lowering refused this already; a LanguageTest built in code can still hand one over.
            throw new ConceptDefinitionException($"'{test.Test}': {error}");
        }

        if (SingleRoot(model) is not { } root)
        {
            return NotASingleRoot(test, model);
        }

        var result = new Interpreter(rules).Run(root, variables);
        var actual = result.Succeeded ? result.Value!.Display : result.Failure!.Message;

        return test.Outcome == actual
            ? TestResult.Passed(test.Test, test.Document, test.AssertionName)
            : TestResult.Failed(test.Test, test.Document, test.AssertionName, test.Outcome, actual);
    }

    /// <summary>Replays an editor test's steps against the document and compares the drawing afterwards.</summary>
    /// <remarks>
    /// The steps belong to <see cref="ScriptRunner"/>, which needs a session rather than a model; this branch only
    /// holds the rule every run shares - a run starts at exactly one root.
    /// </remarks>
    private static TestResult Script(LanguageProject language, LanguageTest test, Model model) =>
        SingleRoot(model) is { } root
            ? ScriptRunner.Run(language, test, root)
            : NotASingleRoot(test, model);

    /// <summary>Whether the node the test names has the type the test spells out.</summary>
    /// <remarks>
    /// <para>
    /// The type is computed, never stored, so this asserts what the language's type rules answer about that node -
    /// which is the whole of the type aspect a language author can state. A language that declares no rules answers
    /// <c>unknown</c> for every node, and a test that expects that is asserting the framework's fallback rather than
    /// the language: it holds, and says so, instead of failing for a reason the author cannot act on.
    /// </para>
    /// <para>
    /// The comparison is the type's <see cref="IType.Name"/> - the way the framework writes the type, which is also
    /// what a diagnostic quotes, so a test and a finding spell a type the same way.
    /// </para>
    /// </remarks>
    private static TestResult TypeOf(LanguageProject language, LanguageTest test, Model model)
    {
        if (SingleRoot(model) is not { } root)
        {
            return NotASingleRoot(test, model);
        }

        if (TestNodes.Resolve(language, root, test.At ?? string.Empty, out var refusal) is not { } node)
        {
            return Unnamed(test, refusal!);
        }

        var actual = language.Types.TypeOf(node).Name;

        return test.Type == actual
            ? TestResult.Passed(test.Test, test.Document, test.AssertionName)
            : TestResult.Failed(test.Test, test.Document, test.AssertionName, test.Type, actual);
    }

    /// <summary>Whether the reference slot the test names sees exactly the names the test lists.</summary>
    /// <remarks>
    /// <para>
    /// The entries are in the scope's own order, which is the order a completion list offers them in - nearest
    /// first - so pinning the order pins the ranking as well as the contents. That is deliberate: a language that
    /// starts seeing one more name, or stops ranking one first, has changed what its users can reference.
    /// </para>
    /// <para>
    /// The scope is the language's own registry, with the framework's default chain behind it, which is exactly what
    /// a session installs - so a test reports the scope a user would meet, including for a language that declares no
    /// scopes of its own.
    /// </para>
    /// </remarks>
    private static TestResult ScopeOf(LanguageProject language, LanguageTest test, Model model)
    {
        if (SingleRoot(model) is not { } root)
        {
            return NotASingleRoot(test, model);
        }

        if (TestNodes.Resolve(language, root, test.At ?? string.Empty, out var refusal) is not { } node)
        {
            return Unnamed(test, refusal!);
        }

        var expected = string.Join('\n', test.Lines ?? []);
        var actual = string.Join('\n', language.Scopes.ScopeOf(node, test.Scope!).Entries.Select(entry => entry.Name));

        return expected == actual
            ? TestResult.Passed(test.Test, test.Document, test.AssertionName)
            : TestResult.Failed(test.Test, test.Document, test.AssertionName, expected, actual);
    }

    /// <summary>The row for a test whose node the document does not show.</summary>
    private static TestResult Unnamed(LanguageTest test, string refusal) =>
        TestResult.Failed(test.Test, test.Document, test.AssertionName, test.At, refusal);

    /// <summary>The one root a generation, an evaluation or a script starts at, or <see langword="null"/>.</summary>
    /// <remarks>
    /// These three assertions ask a document one question, so they need the document to be one thing: a model
    /// holding several roots has no single answer, and the row says so rather than running the first and hiding
    /// the rest.
    /// </remarks>
    private static Node? SingleRoot(Model model) => model.Roots.Count == 1 ? model.Roots[0] : null;

    /// <summary>The row for a document a run cannot start in, because it does not have exactly one root.</summary>
    private static TestResult NotASingleRoot(LanguageTest test, Model model) =>
        TestResult.Failed(
            test.Test,
            test.Document,
            test.AssertionName,
            expected: null,
            $"the document has {model.Roots.Count} roots, and a run starts at exactly one");
}
