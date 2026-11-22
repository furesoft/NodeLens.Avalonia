using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Testing;
using Projectional.TypeRules;
using Xunit;

using TestResult = Projectional.Testing.TestResult;

namespace Projectional.Core.Tests;

/// <summary>
/// The door from the repository's own suite onto a language's <c>tests/</c> folder.
/// </summary>
/// <remarks>
/// <para>
/// A language's tests are documents, so the suite needs one bridge and not one test class per language: this runs
/// the folder and reports the rows. A language that ships a broken test fails here, naming the document, and a
/// language that ships none passes with nothing to say.
/// </para>
/// <para>
/// The sample's tests are copied to the output beside the documents they assert about, so the paths inside a test
/// document are the same paths the sample's own documents use.
/// </para>
/// </remarks>
public sealed class LanguageTestsAsDataTests
{
    private static LanguageProject Open(string folder)
    {
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, folder), out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        return language;
    }

    [Fact]
    public void ThePipelineSamplePassesTheTestsItShips()
    {
        var report = TestRunner.Run(Open("Declared"));

        Assert.True(report.Ran > 0, "the sample ships a tests folder with at least one document");
        Assert.True(report.Succeeded, report.ToString());
    }

    [Fact]
    public void ALanguageWithoutATestsFolderReportsNothing()
    {
        var report = TestRunner.Run(Open("Workflow"));

        Assert.Equal(0, report.Ran);
        Assert.True(report.Succeeded);
    }

    [Fact]
    public void ADocumentThatIsGoneIsAFailedRow()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            File.WriteAllText(
                Path.Combine(folder, "tests", "missing-document.mps"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <model formatVersion="1" id="3f0f9dbb-6a4b-4d7f-9c1e-1b7c2f0a5d31" name="missing-document">
                  <nodes>
                    <node id="2c1d8f0e-5b3a-4c9d-8e7f-0a1b2c3d4e5f" concept="Testing.Test" name="gone.mps">
                      <properties>
                        <document>gone.mps</document>
                      </properties>
                      <children role="assertion">
                        <node id="9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d" concept="Testing.RoundTrips" />
                      </children>
                    </node>
                  </nodes>
                </model>
                """);

            var report = TestRunner.Run(LanguageProject.Open(folder, out var error)!);

            Assert.Null(error);
            Assert.Equal(10, report.Ran);
            Assert.Equal(1, report.Failed);

            var failed = report.Results.Single(result => !result.Succeeded);

            Assert.Equal("missing-document", failed.Test);
            Assert.Equal("gone.mps", failed.Document);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TheTestDocumentDrawsAsASentence()
    {
        var model = TestingLanguage.NewModel();
        var test = model.Roots[0];

        test.Set(TestingLanguage.DocumentSlot, "pipeline.mps");
        model.NewChild(test, TestingLanguage.AssertionSlot, TestingConceptKeys.RoundTrips);

        Assert.Equal(
            """
              test pipeline.mps
                  round trips
            """,
            LayoutSnapshot.Render(model, TestingNotations.Registry, width: 80));
    }

    [Fact]
    public void AnExpectedFindingWithoutACodeStillDrawsACellToWriteOneInto()
    {
        // The code is optional - a declared complaint or constraint may report none - so the cell has to be
        // visible when it is empty. Empty is never invisible: a zero-width cell would be a caret stop on an
        // address that draws no text, so the placeholder is what makes the code writable at all.
        var model = TestingLanguage.NewModel();
        var test = model.Roots[0];

        test.Set(TestingLanguage.DocumentSlot, "pipeline.mps");

        var findings = model.NewChild(test, TestingLanguage.AssertionSlot, TestingConceptKeys.Findings);

        model.NewChild(findings, TestingLanguage.ExpectedSlot, TestingConceptKeys.ExpectedFinding);

        Assert.Equal(
            """
              test pipeline.mps
                  reports
                    << which code >> error << anywhere >>
            """,
            LayoutSnapshot.Render(model, TestingNotations.Registry, width: 80));
    }

    [Fact]
    public void ATestWithoutADocumentIsRefused()
    {
        var model = TestingLanguage.NewModel();
        var test = model.Roots[0];

        model.NewChild(test, TestingLanguage.AssertionSlot, TestingConceptKeys.RoundTrips);

        var refused = Assert.Throws<ConceptDefinitionException>(() => TestingLowering.Lower(test));

        Assert.Contains("document", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ATestWithoutAnAssertionIsRefused()
    {
        var model = TestingLanguage.NewModel();
        var test = model.Roots[0];

        test.Set(TestingLanguage.DocumentSlot, "pipeline.mps");

        var refused = Assert.Throws<ConceptDefinitionException>(() => TestingLowering.Lower(test));

        Assert.Contains("assertion", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ADocumentThatIsNotATestIsRefused()
    {
        var document = new Project(TestingLanguage.Concepts).NewModel("not-a-test");

        document.NewNode(TestingConceptKeys.RoundTrips);

        var refused = Assert.Throws<ConceptDefinitionException>(() => TestingLowering.Lower(document.Roots[0]));

        Assert.Contains("not a test", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ADrawingThatDiffersIsAFailedRowWithBothTexts()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            SaveTest(folder, "wrong-drawing", "pipeline.mps", TestingConceptKeys.Draws, (model, draws) =>
            {
                draws.Set(TestingLanguage.WidthSlot, "60");
                AddLine(model, draws, "not the table");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "wrong-drawing");

            Assert.False(row.Succeeded);
            Assert.Equal("not the table", row.Expected);
            Assert.Contains("pipeline Release", row.Actual, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AFindingsTestThatListsTheFindingPasses()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            PaddedDocument(folder);

            SaveTest(folder, "padded-reports", "padded.mps", TestingConceptKeys.Findings, (model, findings) =>
                AddFinding(model, findings, "pipeline.stage.command", "warning", "command"));

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "padded-reports");

            Assert.True(row.Succeeded, $"expected:{Environment.NewLine}{row.Expected}{Environment.NewLine}actual:{Environment.NewLine}{row.Actual}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AFindingsTestThatMissesTheFindingFails()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            PaddedDocument(folder);

            SaveTest(folder, "padded-clean", "padded.mps", TestingConceptKeys.Findings, (_, _) => { });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "padded-clean");

            Assert.False(row.Succeeded);
            Assert.Equal(string.Empty, row.Expected);
            Assert.Contains("pipeline.stage.command", row.Actual, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AGenerationThatDiffersIsAFailedRowWithBothTexts()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            SaveTest(folder, "wrong-generation", "pipeline.mps", TestingConceptKeys.Generates, (model, generates) =>
            {
                generates.Set(TestingLanguage.GeneratorSlot, "ShellScript");
                AddLine(model, generates, "# Release");
                AddLine(model, generates, "dotnet publish");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "wrong-generation");

            Assert.False(row.Succeeded);
            Assert.Equal("# Release\ndotnet publish", row.Expected);
            Assert.Equal("# Release\ndotnet build\ndotnet test", row.Actual);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AGeneratorTheLanguageDoesNotDeclareIsAFailedRowNamingTheOneItDoes()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            SaveTest(folder, "unknown-generator", "pipeline.mps", TestingConceptKeys.Generates, (_, generates) =>
                generates.Set(TestingLanguage.GeneratorSlot, "Yaml"));

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "unknown-generator");

            Assert.False(row.Succeeded);
            Assert.Equal("Yaml", row.Expected);
            Assert.Contains("ShellScript", row.Actual, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnEvaluationTestRunsWithTheRulesTheCallerSupplies()
    {
        var folder = Copy(Open("Workflow"));

        try
        {
            SaveTest(folder, "release-answers", "release.mps", TestingConceptKeys.Evaluates, (_, evaluates) =>
                evaluates.Set(TestingLanguage.OutcomeSlot, "Release"));

            var row = Row(
                TestRunner.Run(LanguageProject.Open(folder, out _)!, WorkflowRules()),
                "release-answers");

            Assert.True(row.Succeeded, $"expected:{Environment.NewLine}{row.Expected}{Environment.NewLine}actual:{Environment.NewLine}{row.Actual}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnEvaluationTestWithoutRulesIsAFailedRowSayingWhatItNeeded()
    {
        var folder = Copy(Open("Workflow"));

        try
        {
            SaveTest(folder, "release-answers", "release.mps", TestingConceptKeys.Evaluates, (_, evaluates) =>
                evaluates.Set(TestingLanguage.OutcomeSlot, "Release"));

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "release-answers");

            Assert.False(row.Succeeded);
            Assert.Equal("Release", row.Expected);
            Assert.Contains("no evaluation rules", row.Actual, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnEvaluationThatStopsShowsTheFailureInTheDiff()
    {
        var folder = Copy(Open("Workflow"));

        try
        {
            SaveTest(folder, "release-answers", "release.mps", TestingConceptKeys.Evaluates, (_, evaluates) =>
                evaluates.Set(TestingLanguage.OutcomeSlot, "Release"));

            // The rules are supplied but declare nothing, which is the language saying it cannot be run.
            var row = Row(
                TestRunner.Run(LanguageProject.Open(folder, out _)!, EvaluationRegistry.Empty),
                "release-answers");

            Assert.False(row.Succeeded);
            Assert.Equal("Release", row.Expected);
            Assert.Contains("declares no evaluation rule", row.Actual, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnEvaluationTestReadsItsVariablesTheWayTheRunBoxDoes()
    {
        var folder = Copy(Open("Workflow"));

        try
        {
            SaveTest(folder, "release-answers", "release.mps", TestingConceptKeys.Evaluates, (_, evaluates) =>
            {
                evaluates.Set(TestingLanguage.VariablesSlot, "amount=1500, currency=EUR");
                evaluates.Set(TestingLanguage.OutcomeSlot, "Release");
            });

            var row = Row(
                TestRunner.Run(LanguageProject.Open(folder, out _)!, WorkflowRules()),
                "release-answers");

            Assert.True(row.Succeeded, $"expected:{Environment.NewLine}{row.Expected}{Environment.NewLine}actual:{Environment.NewLine}{row.Actual}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnEvaluationWithAVariablesLineThatIsNotOneIsRefused()
    {
        var model = TestingLanguage.NewModel();
        var test = model.Roots[0];

        test.Set(TestingLanguage.DocumentSlot, "release.mps");

        var evaluates = model.NewChild(test, TestingLanguage.AssertionSlot, TestingConceptKeys.Evaluates);

        evaluates.Set(TestingLanguage.VariablesSlot, "amount 1500");
        evaluates.Set(TestingLanguage.OutcomeSlot, "Release");

        var refused = Assert.Throws<ConceptDefinitionException>(() => TestingLowering.Lower(test));

        Assert.Contains("name=value", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGenerationAndEvaluationAssertionsDrawAsSentences()
    {
        var generated = TestingLanguage.NewModel();
        var generatedTest = generated.Roots[0];

        generatedTest.Set(TestingLanguage.DocumentSlot, "pipeline.mps");

        var generates = generated.NewChild(
            generatedTest,
            TestingLanguage.AssertionSlot,
            TestingConceptKeys.Generates);

        generates.Set(TestingLanguage.GeneratorSlot, "ShellScript");
        AddLine(generated, generates, "# Release");

        Assert.Equal(
            """
              test pipeline.mps
                  generates ShellScript
                    # Release
            """,
            LayoutSnapshot.Render(generated, TestingNotations.Registry, width: 80));

        var evaluated = TestingLanguage.NewModel();
        var evaluatedTest = evaluated.Roots[0];

        evaluatedTest.Set(TestingLanguage.DocumentSlot, "release.mps");

        var evaluates = evaluated.NewChild(
            evaluatedTest,
            TestingLanguage.AssertionSlot,
            TestingConceptKeys.Evaluates);

        evaluates.Set(TestingLanguage.VariablesSlot, "amount=1500");
        evaluates.Set(TestingLanguage.OutcomeSlot, "Release");

        Assert.Equal(
            """
              test release.mps
                  evaluates amount=1500
                    answers Release
            """,
            LayoutSnapshot.Render(evaluated, TestingNotations.Registry, width: 80));
    }

    /// <summary>The declared workflow's rules, as a host of that language would supply them.</summary>
    /// <remarks>
    /// A language project carries no evaluation aspect, so a test that evaluates is given its rules by whoever runs
    /// the folder. One rule is enough here: the workflow's own name is what its document answers.
    /// </remarks>
    private static EvaluationRegistry WorkflowRules() =>
        EvaluationRegistry.Empty.Add(
            "Workflow.Workflow",
            new DelegateEvaluationRule(context => context.PropertyValue("name")));

    [Fact]
    public void AScriptRowReplaysItsStepsAndPasses()
    {
        var row = Row(TestRunner.Run(Open("Declared")), "script");

        Assert.True(row.Succeeded, $"expected:{Environment.NewLine}{row.Expected}{Environment.NewLine}actual:{Environment.NewLine}{row.Actual}");
        Assert.Equal("replays", row.Assertion);
    }

    [Fact]
    public void ACaretTheDocumentCannotShowIsAFailedRow()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            SaveTest(folder, "nowhere", "pipeline.mps", TestingConceptKeys.Script, (model, script) =>
                AddStep(model, script, TestingConceptKeys.Caret, TestingLanguage.AtSlot, "nowhere"));

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "nowhere");

            Assert.False(row.Succeeded);
            Assert.Equal("caret at 'nowhere'", row.Expected);
            Assert.Contains("nothing shows 'nowhere'", row.Actual, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnEditTheEditorRefusesIsAFailedRowNamingTheStep()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            SaveTest(folder, "cannot-type", "pipeline.mps", TestingConceptKeys.Script, (model, script) =>
            {
                AddStep(model, script, TestingConceptKeys.Caret, TestingLanguage.AtSlot, "1");
                AddStep(model, script, TestingConceptKeys.Type, TestingLanguage.TypedSlot, "x");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "cannot-type");

            Assert.False(row.Succeeded);
            Assert.Equal("type 'x'", row.Expected);
            Assert.False(string.IsNullOrEmpty(row.Actual));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnOfferTheCaretDoesNotHaveIsAFailedRowListingWhatItDoes()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            SaveTest(folder, "no-such-offer", "pipeline.mps", TestingConceptKeys.Script, (model, script) =>
            {
                AddStep(model, script, TestingConceptKeys.Caret, TestingLanguage.AtSlot, "Release");
                AddStep(model, script, TestingConceptKeys.Invoke, TestingLanguage.IntentionSlot, "intent.nothing");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "no-such-offer");

            Assert.False(row.Succeeded);
            Assert.Equal("invoke intent.nothing", row.Expected);
            Assert.Contains("nothing is offered", row.Actual, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void APressedKeyIsRoutedAndChangesTheDocument()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            SaveTest(folder, "delete-a-letter", "pipeline.mps", TestingConceptKeys.Script, (model, script) =>
            {
                script.Set(TestingLanguage.WidthSlot, "60");
                AddStep(model, script, TestingConceptKeys.Caret, TestingLanguage.AtSlot, "Release");
                AddStep(model, script, TestingConceptKeys.Press, TestingLanguage.KeySlot, "Delete");
                AddLine(model, script, "pipeline elease");
                AddLine(model, script, "  stage run          retries");
                AddLine(model, script, "  build dotnet build 1");
                AddLine(model, script, "  test  dotnet test  0");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "delete-a-letter");

            Assert.True(row.Succeeded, $"expected:{Environment.NewLine}{row.Expected}{Environment.NewLine}actual:{Environment.NewLine}{row.Actual}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnInvokedOfferEditsTheDocument()
    {
        var folder = Copy(Open("Workflow"));

        try
        {
            SaveTest(folder, "adds-a-step", "draft.mps", TestingConceptKeys.Script, (model, script) =>
            {
                script.Set(TestingLanguage.WidthSlot, "80");

                // The empty `first` slot is where the language's own intention applies, and a key is how a user
                // reaches a place that shows no text for the finder to match.
                AddStep(model, script, TestingConceptKeys.Press, TestingLanguage.KeySlot, "ControlEnd");
                AddStep(
                    model,
                    script,
                    TestingConceptKeys.Invoke,
                    TestingLanguage.IntentionSlot,
                    "intent.Workflow.Workflow.Add-the-first-step");

                // The notation now draws the reusable steps under the chain, so the drawing is two lines - and the
                // step the intention added is the `first` the header points at.
                AddLine(model, script, "workflow Draft -> <empty>");
                AddLine(model, script, "library");
                AddLine(model, script, "    :   <none> then <empty>");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "adds-a-step");

            Assert.True(row.Succeeded, $"expected:{Environment.NewLine}{row.Expected}{Environment.NewLine}actual:{Environment.NewLine}{row.Actual}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void APressStepNamingAKeyTheEditorDoesNotRouteIsRefused()
    {
        var model = TestingLanguage.NewModel();
        var test = model.Roots[0];

        test.Set(TestingLanguage.DocumentSlot, "pipeline.mps");

        var script = model.NewChild(test, TestingLanguage.AssertionSlot, TestingConceptKeys.Script);

        AddStep(model, script, TestingConceptKeys.Press, TestingLanguage.KeySlot, "Entr");

        var refused = Assert.Throws<ConceptDefinitionException>(() => TestingLowering.Lower(test));

        Assert.Contains("is not a key the editor routes", refused.Message, StringComparison.Ordinal);
        Assert.Contains("Enter", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AScriptDrawsAsItsStepsAndThenTheDrawing()
    {
        var model = TestingLanguage.NewModel();
        var test = model.Roots[0];

        test.Set(TestingLanguage.DocumentSlot, "pipeline.mps");

        var script = model.NewChild(test, TestingLanguage.AssertionSlot, TestingConceptKeys.Script);

        script.Set(TestingLanguage.WidthSlot, "60");
        AddStep(model, script, TestingConceptKeys.Caret, TestingLanguage.AtSlot, "Release");
        AddStep(model, script, TestingConceptKeys.Type, TestingLanguage.TypedSlot, "Pre");
        AddLine(model, script, "pipeline PreRelease");

        Assert.Equal(
            """
              test pipeline.mps
                  script 60 columns
                    caret at Release
                    type Pre
                  then draws
                    pipeline PreRelease
            """,
            LayoutSnapshot.Render(model, TestingNotations.Registry, width: 80));
    }

    [Fact]
    public void ATypeOfTestPinsTheTypeTheRulesAnswer()
    {
        var folder = Copy(Open("Workflow"));

        try
        {
            StepHasAType(folder, "string");

            SaveTest(folder, "build-is-a-string", "release.mps", TestingConceptKeys.TypeOf, (_, type) =>
            {
                type.Set(TestingLanguage.AtSlot, "build");
                type.Set(TestingLanguage.TypeSlot, "string");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "build-is-a-string");

            Assert.True(row.Succeeded, $"expected:{Environment.NewLine}{row.Expected}{Environment.NewLine}actual:{Environment.NewLine}{row.Actual}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ATypeOfTestThatExpectsAnotherTypeFailsWithBoth()
    {
        var folder = Copy(Open("Workflow"));

        try
        {
            StepHasAType(folder, "string");

            SaveTest(folder, "build-is-an-int", "release.mps", TestingConceptKeys.TypeOf, (_, type) =>
            {
                type.Set(TestingLanguage.AtSlot, "build");
                type.Set(TestingLanguage.TypeSlot, "int");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "build-is-an-int");

            Assert.False(row.Succeeded);
            Assert.Equal("int", row.Expected);
            Assert.Equal("string", row.Actual);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ATypeOfTestOnALanguageWithoutRulesPinsTheFallback()
    {
        // A language that declares no type rules answers "unknown" for every node, which is a fact a language author
        // can state - so the assertion holds, rather than failing for a reason they cannot act on.
        var folder = Copy(Open("Declared"));

        try
        {
            SaveTest(folder, "no-types-declared", "pipeline.mps", TestingConceptKeys.TypeOf, (_, type) =>
            {
                type.Set(TestingLanguage.AtSlot, "Release");
                type.Set(TestingLanguage.TypeSlot, "unknown");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "no-types-declared");

            Assert.True(row.Succeeded, $"expected:{Environment.NewLine}{row.Expected}{Environment.NewLine}actual:{Environment.NewLine}{row.Actual}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AScopeOfTestPinsWhatTheSlotSees()
    {
        var folder = Copy(Open("Workflow"));

        try
        {
            SaveTest(folder, "what-onfailure-sees", "release.mps", TestingConceptKeys.ScopeOf, (model, scope) =>
            {
                // The second step, because the language's own rule asks the ancestors and the first step has no
                // step above it - so it sees nothing, which is a fact worth pinning separately.
                scope.Set(TestingLanguage.AtSlot, "dotnet test");
                scope.Set(TestingLanguage.ScopeSlot, "onFailure");

                // `build` is the step above it, and the reusable steps of this workflow are visible too: a failure
                // handler may point at a step it shares with the chain.
                AddLine(model, scope, "build");
                AddLine(model, scope, "shared build");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "what-onfailure-sees");

            Assert.True(row.Succeeded, $"expected:{Environment.NewLine}{row.Expected}{Environment.NewLine}actual:{Environment.NewLine}{row.Actual}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AScopeOfTestThatMissesANameFails()
    {
        var folder = Copy(Open("Workflow"));

        try
        {
            SaveTest(folder, "forgets-a-name", "release.mps", TestingConceptKeys.ScopeOf, (_, scope) =>
            {
                scope.Set(TestingLanguage.AtSlot, "dotnet test");
                scope.Set(TestingLanguage.ScopeSlot, "onFailure");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "forgets-a-name");

            Assert.False(row.Succeeded);
            Assert.Equal(string.Empty, row.Expected);
            Assert.Contains("build", row.Actual, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ANodeTheDocumentCannotShowIsAFailedRow()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            SaveTest(folder, "no-such-node", "pipeline.mps", TestingConceptKeys.TypeOf, (_, type) =>
            {
                type.Set(TestingLanguage.AtSlot, "nowhere");
                type.Set(TestingLanguage.TypeSlot, "unknown");
            });

            var row = Row(TestRunner.Run(LanguageProject.Open(folder, out _)!), "no-such-node");

            Assert.False(row.Succeeded);
            Assert.Equal("nowhere", row.Expected);
            Assert.Contains("nothing shows 'nowhere'", row.Actual, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TheNodeAddressedAssertionsDrawAsSentences()
    {
        var typed = TestingLanguage.NewModel();
        var typedTest = typed.Roots[0];

        typedTest.Set(TestingLanguage.DocumentSlot, "release.mps");

        var type = typed.NewChild(typedTest, TestingLanguage.AssertionSlot, TestingConceptKeys.TypeOf);

        type.Set(TestingLanguage.AtSlot, "build");
        type.Set(TestingLanguage.TypeSlot, "string");

        Assert.Equal(
            """
              test release.mps
                  the node at build has type string
            """,
            LayoutSnapshot.Render(typed, TestingNotations.Registry, width: 80));

        var scoped = TestingLanguage.NewModel();
        var scopedTest = scoped.Roots[0];

        scopedTest.Set(TestingLanguage.DocumentSlot, "release.mps");

        var scope = scoped.NewChild(scopedTest, TestingLanguage.AssertionSlot, TestingConceptKeys.ScopeOf);

        scope.Set(TestingLanguage.AtSlot, "build");
        scope.Set(TestingLanguage.ScopeSlot, "onFailure");
        AddLine(scoped, scope, "Release");

        Assert.Equal(
            """
              test release.mps
                  the node at build sees into onFailure
                    Release
            """,
            LayoutSnapshot.Render(scoped, TestingNotations.Registry, width: 80));
    }

    /// <summary>Gives every step of the copied workflow the fixed type <paramref name="type"/>.</summary>
    /// <remarks>
    /// The rule is built with the declared type-rule language rather than written as XML, so the fixture cannot drift
    /// from the language it is written in - and a language project with no <c>types</c> folder is what the sample
    /// ships, so the folder has to be made for the assertion to have something to ask.
    /// </remarks>
    private static void StepHasAType(string folder, string type)
    {
        var model = TypeRuleLanguage.NewProject().NewModel("Workflow.Step");
        var rule = model.NewNode(TypeRuleConceptKeys.Rule);

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Workflow.Step");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);
        var primitive = model.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Primitive);

        primitive.Set(TypeRuleLanguage.NameSlot, type);

        model.AddRoot(rule);

        ModelFile.Save(model, Path.Combine(folder, LanguageProject.TypesFolder, "Workflow.Step.mps"));
    }

    /// <summary>Adds one step to a script assertion.</summary>
    private static void AddStep(Model model, Node script, string kind, string slot, string value) =>
        model.NewChild(script, TestingLanguage.StepsSlot, kind).Set(slot, value);

    /// <summary>Writes a test document into the copied language's <c>tests</c> folder.</summary>
    private static void SaveTest(
        string folder,
        string name,
        string document,
        string assertion,
        Action<Model, Node> fill)
    {
        var model = TestingLanguage.NewModel();

        model.Rename(name);

        var test = model.Roots[0];

        test.Set(TestingLanguage.DocumentSlot, document);

        fill(model, model.NewChild(test, TestingLanguage.AssertionSlot, assertion));

        ModelFile.Save(model, Path.Combine(folder, "tests", name + ".mps"));
    }

    private static void AddLine(Model model, Node draws, string text) =>
        model.NewChild(draws, TestingLanguage.LinesSlot, TestingConceptKeys.Line).Set(TestingLanguage.TextSlot, text);

    private static void AddFinding(Model model, Node findings, string code, string severity, string role)
    {
        var finding = model.NewChild(findings, TestingLanguage.ExpectedSlot, TestingConceptKeys.ExpectedFinding);

        finding.Set(TestingLanguage.CodeSlot, code);
        finding.Set(TestingLanguage.SeveritySlot, severity);
        finding.Set(TestingLanguage.RoleSlot, role);
    }

    /// <summary>Adds a document whose first stage's command is padded, which the sample's own rule warns about.</summary>
    private static void PaddedDocument(string folder)
    {
        var language = LanguageProject.Open(folder, out _)!;
        var model = ModelFile.Load(language.NewProject(), Path.Combine(folder, "pipeline.mps"));

        model.Rename("padded");
        model.Roots[0].GetChildren("stages")[0].Set("command", " build ");

        ModelFile.Save(model, Path.Combine(folder, "padded.mps"));
    }

    private static TestResult Row(TestReport report, string name) =>
        report.Results.Single(result => result.Test == name);

    private static string Copy(LanguageProject language)
    {
        var destination = Directory.CreateTempSubdirectory("language-tests").FullName;

        foreach (var file in Directory.EnumerateFiles(language.Directory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(language.Directory, file);
            var target = Path.Combine(destination, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        return destination;
    }
}
