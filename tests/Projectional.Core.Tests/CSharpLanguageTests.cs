using Projectional.CSharp;
using Projectional.CSharp.Checks;
using Projectional.CSharp.Generation;
using Projectional.CSharp.Intentions;
using Projectional.CSharp.Notation;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The C# base language: the concepts a document is made of, the notation that draws them, the rules it states, the
/// intentions it offers, and the generator that turns the tree back into code a compiler accepts.
/// </summary>
/// <remarks>
/// It is a base language, not a sample: it ships in the shell's own registry, so a DSL declares a slot over
/// <see cref="CSharpConceptKeys.Expression"/> and gets C# there. These tests are the language's own, which is what
/// makes it one - a language whose drawing and generation disagree is a language whose two halves are both guesses.
/// </remarks>
public sealed class CSharpLanguageTests
{
    [Fact]
    public void EveryConceptCanBeDrawnAndTheRootsAreAbstract()
    {
        var notations = CSharpNotations.CreateNotations(CSharpLanguage.Concepts);

        foreach (var concept in CSharpLanguage.Concepts.Concepts.Where(concept => !concept.IsAbstract))
        {
            Assert.True(notations.TryGet(concept.Key, out _), concept.Key);
        }

        // An expression is never a node of its own kind, so a slot declared over it names a set.
        Assert.True(CSharpLanguage.Concepts.Get(CSharpConceptKeys.Expression).IsAbstract);
        Assert.True(CSharpLanguage.Concepts.Get(CSharpConceptKeys.Statement).IsAbstract);
        Assert.True(CSharpLanguage.Concepts.Get(CSharpConceptKeys.Member).IsAbstract);

        // One root, and it is the document: a base language that offered several would make "new document" a question.
        // The framework's own concepts are injected into every registry and are not this language's answer.
        Assert.Equal(
            [CSharpConceptKeys.File],
            CSharpLanguage.Concepts.Concepts
                .Where(concept => concept.IsRoot && concept.Key.StartsWith("CSharp.", StringComparison.Ordinal))
                .Select(concept => concept.Key));
    }

    [Fact]
    public void TheNotationDrawsTheSameCSharpTheGeneratorEmits()
    {
        // The claim a projectional editor makes, checked rather than asserted: what the user sees is what the
        // generator writes, character for character. One test holds both halves so they cannot drift apart.
        var model = Sample();

        var drawn = LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120);
        var generated = new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text;

        Assert.Equal(Expected, drawn);
        Assert.Equal(Expected + "\n", generated);
    }

    [Fact]
    public void TheRulesAreReportedOnTheNodesTheyAreAbout()
    {
        var model = new Project(CSharpLanguage.Concepts).NewModel("broken");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.NameSlot, "Broken");
        field.Set(CSharpLanguage.NameSlot, " padded ");

        // The method has no body and still carries the name its declaration gave it.
        var findings = new ConstraintEngine(CSharpConstraints.All).Check(model);

        Assert.Equal(
            [
                CSharpConstraints.PaddedNameCode,
                CSharpConstraints.MissingBodyCode,
                CSharpConstraints.PlaceholderNameCode,
            ],
            findings.Select(finding => finding.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnIntentionIsOfferedOnlyOnTheConceptItEdits()
    {
        var model = new Project(CSharpLanguage.Concepts).NewModel("intentions");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);

        Assert.Equal(
            [CSharpIntentions.MakeClassPublicId, CSharpIntentions.MakeClassStaticId],
            Offered(cls));

        Assert.Equal(
            [CSharpIntentions.AddParameterId, CSharpIntentions.MakeMethodStaticId],
            Offered(method));

        // A namespace is neither a class nor a method, so the source has nothing to say there.
        Assert.Empty(Offered(ns));

        // Asked the way the editor asks: a session with the provider registered, a caret on the node, and the list
        // that comes back - so the test drives the same path a keystroke does.
        string[] Offered(Node node)
        {
            using var session = new EditorSession(model.Roots[0], CSharpNotations.CreateNotations(CSharpLanguage.Concepts));

            session.AddIntentions(CSharpIntentions.Provider, IntentionSource.Language);
            session.Select(Selection.At(new CellAddress(node.Id)));

            return [.. IntentionList.For(session).Select(intention => intention.Id).Order(StringComparer.Ordinal)];
        }
    }

    [Fact]
    public void AddingAParameterFillsTheHiddenList()
    {
        // The notation hides an empty parameter list so that `Greet()` reads as C#, and this is the way back in: the
        // pair is the bargain a hidden slot makes, and it is what makes the intention part of the language rather
        // than a convenience.
        var project = new Project(CSharpLanguage.Concepts);
        var model = project.NewModel("insert");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);

        Assert.Empty(method.GetChildren(CSharpConceptKeys.ParametersSlot));

        // The action edits the live model through a session, so it is driven the way the editor drives it.
        using var session = new EditorSession(model.Roots[0], CSharpNotations.CreateNotations(CSharpLanguage.Concepts));

        session.Select(Selection.At(new CellAddress(method.Id)));
        session.Apply(new InsertChildAction(method.Id, CSharpConceptKeys.ParametersSlot));

        var parameter = Assert.Single(method.GetChildren(CSharpConceptKeys.ParametersSlot));

        Assert.Equal(CSharpConceptKeys.Parameter, parameter.Concept.Key);
    }

    private static Model Sample()
    {
        var model = new Project(CSharpLanguage.Concepts).NewModel("hello");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Greeter");

        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var fieldType = model.NewChild(field, "type", CSharpConceptKeys.TypeReference);
        var greeting = model.NewChild(field, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.StringLiteral);

        field.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Private);
        field.Set(CSharpLanguage.NameSlot, "message");
        fieldType.Set(CSharpLanguage.NameSlot, "string");
        greeting.Set(CSharpLanguage.ValueProperty, "Hello");

        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var statement = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var call = model.NewChild(statement, "expression", CSharpConceptKeys.Call);
        var callee = model.NewChild(call, "callee", CSharpConceptKeys.MemberAccess);
        var console = model.NewChild(callee, "target", CSharpConceptKeys.Name);
        var argument = model.NewChild(call, CSharpConceptKeys.ArgumentsSlot, CSharpConceptKeys.Name);

        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.IsStaticSlot, "true");
        method.Set(CSharpLanguage.NameSlot, "Greet");
        returnType.Set(CSharpLanguage.NameSlot, "void");
        callee.Set(CSharpLanguage.MemberSlot, "WriteLine");
        console.Set(CSharpLanguage.NameSlot, "Console");
        argument.Set(CSharpLanguage.NameSlot, "message");

        return model;
    }

    private const string Expected =
        """
        namespace Sample
        {
          public class Greeter
          {
            private string message = "Hello";
            public static void Greet()
            {
              Console.WriteLine(message);
            }
          }
        }
        """;
}
