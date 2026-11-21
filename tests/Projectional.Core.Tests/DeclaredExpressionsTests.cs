using Projectional.Core;
using Projectional.Editing;
using Projectional.Editors;
using Projectional.ImportRules;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Semantics;
using Projectional.Structures;
using Projectional.Testing;
using Projectional.TypingRules;
using Projectional.TypeRules;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The declared expression language, whose type rules are documents rather than C#.</summary>
/// <remarks>
/// This is the first language in the repository whose types live in a <c>types/</c> folder. The declared vocabulary
/// was proved by tests before this: a lowering test here and a parity test there. What a shipped document adds is the
/// answer to "does an author who only has the documentation get a working language", and that is a different question
/// from "does each body lower".
/// </remarks>
public sealed class DeclaredExpressionsTests
{
    private static LanguageProject Language => Open();

    [Fact]
    public void TheLanguageOpensAndDeclaresItsTypes()
    {
        var language = Language;

        Assert.True(language.HasTypes);

        // Four rules - two fixed types, a switch and a widest - and one complaint. A complaint states a diagnostic
        // rather than a type, so it is in the constraints and not in the registry.
        Assert.Equal(4, language.Types.Count);
        Assert.Equal(1, language.Constraints.Count);
    }

    [Fact]
    public void ALiteralIsTheTypeItsRuleNames()
    {
        var model = NewModel();
        var number = model.NewNode("Decl.Int");
        var flag = model.NewNode("Decl.Bool");

        Assert.Equal("int", Language.Types.TypeOf(number).Name);
        Assert.Equal("bool", Language.Types.TypeOf(flag).Name);
    }

    [Fact]
    public void AComparisonIsATruthValueAndAnUnknownOperatorIsAsWideAsItsLeftOperand()
    {
        var model = NewModel();

        var comparison = Binary(model, "<");

        var addition = Binary(model, "+");

        Assert.Equal("bool", Language.Types.TypeOf(comparison).Name);
        Assert.Equal("int", Language.Types.TypeOf(addition).Name);
    }

    [Fact]
    public void AConditionalIsAsWideAsItsTwoBranches()
    {
        // The rule the vocabulary could not express until `widest of` existed, in a language that is only documents.
        var model = NewModel();

        var both = model.NewNode("Decl.If");
        model.NewChild(both, "then", "Decl.Int");
        model.NewChild(both, "else", "Decl.Int");

        var mixed = model.NewNode("Decl.If");
        model.NewChild(mixed, "then", "Decl.Int");
        model.NewChild(mixed, "else", "Decl.Bool");

        Assert.Equal("int", Language.Types.TypeOf(both).Name);
        Assert.Same(UnknownType.Instance, Language.Types.TypeOf(mixed));
    }

    [Fact]
    public void TheDeclaredComplaintReportsOperandsThatCannotBeOneValue()
    {
        // The conditional complaint, written as a document: it fires only where the condition holds, which is what
        // makes it a rule about operands rather than a sentence about the concept.
        var model = NewModel();
        var mixed = model.NewNode("Decl.Binary");

        mixed.Set("operator", "==");
        model.NewChild(mixed, "left", "Decl.Int");
        model.NewChild(mixed, "right", "Decl.Bool");

        var finding = Assert.Single(Check(model));

        Assert.Equal(mixed.Id, finding.Target);
        Assert.Equal(DiagnosticSeverity.Error, finding.Severity);
        Assert.Contains("not comparable", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDeclaredComplaintStaysSilentWhenTheOperandsFit()
    {
        var model = NewModel();
        var numbers = model.NewNode("Decl.Binary");

        numbers.Set("operator", "==");
        model.NewChild(numbers, "left", "Decl.Int");
        model.NewChild(numbers, "right", "Decl.Int");

        Assert.Empty(Check(model));
    }

    [Fact]
    public void TheShippedDocumentDrawsItsNotations()
    {
        // The editor documents are part of the language, so a language that opens can still draw nothing useful. The
        // type rules go in because the sample's editors now end their lines with a `typeof` cell, so what this pins is
        // the drawing a reader actually sees - including the annotation each literal and the conditional carry.
        var written = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Expressions", "expressions.mps"));
        var model = ModelXml.Deserialize(Language.NewProject(), written);

        Assert.Equal(
            "if 1 : int < 2 : int then 3 : int else 4 : int",
            LayoutSnapshot.Render(model, Language.Notations, width: 60, types: Language.Types));
    }

    [Fact]
    public void TheShippedDocumentIsCleanAndRoundTrips()
    {
        // What a user opens first: the document must not greet them with a finding, and saving it back must not
        // change a byte.
        var written = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Expressions", "expressions.mps"));
        var model = ModelXml.Deserialize(Language.NewProject(), written);

        Assert.Empty(Check(model));
        Assert.Equal(written.Replace("\r\n", "\n", StringComparison.Ordinal), ModelXml.Serialize(model));
    }

    [Fact]
    public void EveryDefinitionDocumentSavesBackByteIdentically()
    {
        // The same guard the other declared sample has, and the reason a hand-written definition is safe to keep:
        // the writer is the authority, so a file that is not the writer's own output fails here rather than drifting.
        var directory = Path.Combine(AppContext.BaseDirectory, "Expressions");
        var temporary = Directory.CreateTempSubdirectory("declared-expressions-definitions");

        try
        {
            AssertByteIdentical(temporary.FullName, directory, "structure", new Project(StructureLanguage.Concepts));
            AssertByteIdentical(temporary.FullName, directory, "editor", new Project(EditorLanguage.Concepts));
            AssertByteIdentical(temporary.FullName, directory, "types", new Project(TypeRuleLanguage.Concepts));

            // The importer is a definition like the rest: a file that would change on its first save is drift the author
            // would meet the moment they opened it.
            AssertByteIdentical(temporary.FullName, directory, LanguageProject.ImportFolder, ImportLanguage.NewProject());

            // The typing rules too, and for the same reason: they are the aspect a language author writes to make
            // typing produce a node, so a file the writer would re-word is a rule that looks wrong the moment it opens.
            AssertByteIdentical(temporary.FullName, directory, LanguageProject.TypingFolder, TypingLanguage.NewProject());
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    [Fact]
    public void EveryDocumentOfTheSampleSavesBackByteIdentically()
    {
        // A sample ships documents as well as definitions, and they are the writer's bytes for the same reason: a
        // user opens one, edits it and saves it, so a file that would change on the first save is drift they would
        // meet immediately. The two places differ in whose concepts they need - the root holds documents of the
        // declared language, the tests folder holds testing-language documents.
        var directory = Path.Combine(AppContext.BaseDirectory, "Expressions");
        var temporary = Directory.CreateTempSubdirectory("declared-expressions-documents");

        try
        {
            AssertRoundTripsIn(temporary.FullName, directory, Language.NewProject());
            AssertRoundTripsIn(temporary.FullName, Path.Combine(directory, TestRunner.Folder), TestingLanguage.NewProject());
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    /// <summary>Round-trips every document in <paramref name="folder"/> and compares the bytes.</summary>
    private static void AssertByteIdentical(string temporary, string root, string folder, Project project)
    {
        var files = Directory.EnumerateFiles(Path.Combine(root, folder), "*.mps").ToList();

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            AssertByteIdentical(temporary, file, project);
        }
    }

    /// <summary>Round-trips the documents in <paramref name="folder"/> without descending into its own folders.</summary>
    private static void AssertRoundTripsIn(string temporary, string folder, Project project)
    {
        var files = Directory.EnumerateFiles(folder, "*.mps", SearchOption.TopDirectoryOnly).ToList();

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            AssertByteIdentical(temporary, file, project);
        }
    }

    private static void AssertByteIdentical(string temporary, string file, Project project)
    {
        var model = ModelFile.Load(project, file);
        var target = Path.Combine(temporary, Path.GetFileName(file));

        ModelFile.Save(model, target);

        Assert.Equal(File.ReadAllBytes(file), File.ReadAllBytes(target));
    }

    /// <summary>The language tests itself: its own <c>tests/</c> folder runs green.</summary>
    /// <remarks>
    /// Two of the assertions are about findings, one for a document that reports nothing and one for the complaint in
    /// <c>types/operands-not-comparable.mps</c>. That pair is what proves the runner hands the engine the language's
    /// types: without them the operands read as <c>unknown</c>, the complaint stays silent, and the row that expects a
    /// finding fails. A runner that skipped them would be running a different engine than a session does.
    /// </remarks>
    [Fact]
    public void TheSampleRunsItsOwnDeclaredTests()
    {
        var report = TestRunner.Run(Language);

        Assert.Equal(4, report.Ran);
        Assert.True(report.Succeeded, report.ToString());
    }

    [Fact]
    public void TheSampleCreatesABinaryFromItsTypedAlias()
    {
        // The alias is what the completion list offers, so it is also what a keystroke creates - MPS' "an instance
        // created from a typed alias", proven on the sample rather than on a fixture: `Decl.Binary` declares `binary`,
        // and its typing rule claims that alias instead of spelling it a second time.
        var language = Language;

        var model = language.NewProject().NewModel("typed");
        var root = model.NewNode("Decl.Binary");
        root.Set("operator", "+");
        model.AddRoot(root);

        using var session = new EditorSession(root, language.Notations, typing: language.Typing);

        session.Select(Selection.At(CellAddress.OfSlot(root.Id, "left")));
        session.Apply(KeyRouter.Type("binary"));

        var created = Assert.Single(root.GetChildren("left"));

        Assert.Equal("Decl.Binary", created.Concept.Key);

        // The whole word and nothing shorter: a prefix is not a claim, which is what separates an alias from typing
        // rules that match a prefix.
        session.Select(Selection.At(CellAddress.OfSlot(root.Id, "right")));

        Assert.Equal(EditOutcome.Unchanged, session.Apply(KeyRouter.Type("bin")).Outcome);
        Assert.Empty(root.GetChildren("right"));
    }

    [Fact]
    public void TheSampleTypingRulesMakeATypedDigitBecomeALiteral()
    {
        // The aspect is only worth having if the editor asks it: a digit typed into an empty operand slot has to
        // produce a `Decl.Int` whose `value` holds the text, from the sample's own `typing/` documents and its own
        // notation - not from a C# registry the test supplies.
        var language = Language;

        Assert.True(language.HasTyping);

        var model = language.NewProject().NewModel("typed");
        var binary = model.NewNode("Decl.Binary");
        binary.Set("operator", "+");
        model.AddRoot(binary);

        using var session = new EditorSession(binary, language.Notations, typing: language.Typing);

        session.Select(Selection.At(CellAddress.OfSlot(binary.Id, "left")));
        session.Apply(KeyRouter.Type("7"));

        var literal = Assert.Single(binary.GetChildren("left"));

        Assert.Equal("Decl.Int", literal.Concept.Key);
        Assert.Equal("7", literal.GetOrNull("value"));
    }

    [Fact]
    public void TheSampleTypingRulesClaimTheWordsItsBooleanUnderstands()
    {
        var language = Language;

        var model = language.NewProject().NewModel("typed");
        var binary = model.NewNode("Decl.Binary");
        binary.Set("operator", "==");
        model.AddRoot(binary);

        using var session = new EditorSession(binary, language.Notations, typing: language.Typing);

        session.Select(Selection.At(CellAddress.OfSlot(binary.Id, "right")));
        session.Apply(KeyRouter.Type("false"));

        var literal = Assert.Single(binary.GetChildren("right"));

        Assert.Equal("Decl.Bool", literal.Concept.Key);
        Assert.Equal("false", literal.GetOrNull("value"));
    }

    [Fact]
    public void TheSampleImporterReadsTheSpellingItDeclares()
    {
        // The sample ships one declared importer, so the vocabulary is exercised by a language an author can open
        // rather than only by the test suite. Its spelling is a second one on purpose: the notation draws
        // `if 1 < 2 then 3 else 4`, and this reads `(if (binary < (int 1) (int 2)) (int 3) (int 4))` - a form no
        // generator writes, which is why it is a rule and not the inverse of a template.
        var language = Language;

        Assert.True(language.TryCreateImporter("sexp", out var importer, out var why), why);
        Assert.IsType<DeclaredImporter>(importer);

        var model = language.NewProject().NewModel("imported");
        var result = importer!.Import(model, "(if (binary < (int 1) (int 2)) (int 3) (int 4))");

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        var root = Assert.Single(result.Roots);

        Assert.Equal("Decl.If", root.Concept.Key);
        Assert.Equal("3", root.GetChildren("then").Single().GetOrNull("value"));

        var condition = root.GetChildren("condition").Single();

        Assert.Equal("Decl.Binary", condition.Concept.Key);
        Assert.Equal("<", condition.GetOrNull("operator"));
        Assert.Equal("1", condition.GetChildren("left").Single().GetOrNull("value"));
        Assert.Equal("4", root.GetChildren("else").Single().GetOrNull("value"));
    }

    private static IReadOnlyList<Diagnostic> Check(Model model) =>
        new ConstraintEngine(Language.Constraints, types: Language.Types).Check(model);

    private static Model NewModel() => Language.NewProject().NewModel("expressions");

    private static Node Binary(Model model, string op)
    {
        var binary = model.NewNode("Decl.Binary");

        binary.Set("operator", op);
        model.NewChild(binary, "left", "Decl.Int");
        model.NewChild(binary, "right", "Decl.Int");

        return binary;
    }

    private static LanguageProject Open()
    {
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Expressions"), out var error);

        Assert.Null(error);

        return language!;
    }
}
