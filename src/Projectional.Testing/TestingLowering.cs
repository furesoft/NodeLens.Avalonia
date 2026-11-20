using System.Globalization;

using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;

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
            TestingConceptKeys.Generates => new LanguageTest(
                name,
                target,
                assertion,
                Width: 0,
                Lines(children[0]),
                Findings: null,
                Generator: Required(children[0], TestingLanguage.GeneratorSlot)),
            TestingConceptKeys.Evaluates => new LanguageTest(
                name,
                target,
                assertion,
                Width: 0,
                Lines: null,
                Findings: null,
                Generator: null,
                Variables: Variables(children[0]),
                Outcome: Required(children[0], TestingLanguage.OutcomeSlot)),
            TestingConceptKeys.Script => new LanguageTest(
                name,
                target,
                assertion,
                Width(children[0]),
                Lines(children[0]),
                Findings: null,
                Generator: null,
                Variables: null,
                Outcome: null,
                Steps: Steps(children[0])),
            TestingConceptKeys.TypeOf => new LanguageTest(
                name,
                target,
                assertion,
                Width: 0,
                Lines: null,
                Findings: null,
                Generator: null,
                Variables: null,
                Outcome: null,
                Steps: null,
                At: Required(children[0], TestingLanguage.AtSlot),
                Type: Required(children[0], TestingLanguage.TypeSlot)),
            TestingConceptKeys.ScopeOf => new LanguageTest(
                name,
                target,
                assertion,
                Width: 0,
                Lines(children[0]),
                Findings: null,
                Generator: null,
                Variables: null,
                Outcome: null,
                Steps: null,
                At: Required(children[0], TestingLanguage.AtSlot),
                Type: null,
                Scope: Required(children[0], TestingLanguage.ScopeSlot)),
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
    /// <remarks>
    /// The severity is required and the code is not, because a finding may be reported without one: a declared
    /// complaint states a message and how much it matters, and the type-rule language has no slot for an identifier
    /// yet (<c>docs/open-questions.md</c>, question 115), so demanding a code would make such a finding impossible
    /// to name.
    /// </remarks>
    private static IReadOnlyList<ExpectedFinding> Findings(Node findings) =>
        [.. findings.GetChildren(TestingLanguage.ExpectedSlot).Select(finding => new ExpectedFinding(
            finding.GetOrNull(TestingLanguage.CodeSlot)?.Trim() ?? string.Empty,
            Required(finding, TestingLanguage.SeveritySlot),
            finding.GetOrNull(TestingLanguage.RoleSlot)?.Trim() ?? string.Empty))];

    /// <summary>The variables an evaluation assertion is run with.</summary>
    /// <remarks>
    /// Read here rather than in the runner for the same reason every other slot is: a line the parser refuses is a
    /// test that does not say what it means, which is an authoring mistake and not a result to report.
    /// </remarks>
    private static string Variables(Node evaluates)
    {
        var text = evaluates.GetOrNull(TestingLanguage.VariablesSlot)?.Trim() ?? string.Empty;

        return VariableInputs.TryParse(text, out _, out var error)
            ? text
            : throw new ConceptDefinitionException($"'{text}' is not a line of name=value pairs: {error}");
    }

    /// <summary>The steps a script assertion replays, in order.</summary>
    /// <remarks>
    /// Each step is checked against the one property its kind needs, and a key is checked against the routing
    /// table's own names - so a script that names a key the editor does not route is refused when the suite is
    /// read, rather than reporting a failed row for a mistake in the test.
    /// </remarks>
    private static IReadOnlyList<TestStep> Steps(Node script) =>
        [.. script.GetChildren(TestingLanguage.StepsSlot).Select(Step)];

    /// <summary>One step, as the kind and the value it names.</summary>
    private static TestStep Step(Node step) => step.Concept.Key switch
    {
        TestingConceptKeys.Caret => new TestStep(step.Concept.Key, Required(step, TestingLanguage.AtSlot)),
        TestingConceptKeys.Type => new TestStep(step.Concept.Key, Required(step, TestingLanguage.TypedSlot)),
        TestingConceptKeys.Press => new TestStep(step.Concept.Key, Key(step)),
        TestingConceptKeys.Invoke => new TestStep(step.Concept.Key, Required(step, TestingLanguage.IntentionSlot)),
        var other => throw new ConceptDefinitionException(
            $"'{other}' is not a step this framework replays."),
    };

    /// <summary>The key a press step presses, refused when the routing table has no such name.</summary>
    private static string Key(Node press)
    {
        var name = Required(press, TestingLanguage.KeySlot);

        return EditorKeyNames.Contains(name)
            ? name
            : throw new ConceptDefinitionException(
                $"'{name}' is not a key the editor routes. It handles {string.Join(", ", EditorKeyNames)}.");
    }

    /// <summary>The keys the editor routes, in the order the routing table names them.</summary>
    private static IReadOnlyList<string> EditorKeyNames { get; } = [.. Enum.GetNames<EditorKey>()];

    private static string Required(Node node, string slot)
    {
        var value = node.GetOrNull(slot);

        return string.IsNullOrWhiteSpace(value)
            ? throw new ConceptDefinitionException($"'{node.Concept.Key}' has to fill '{slot}'.")
            : value.Trim();
    }
}
