using Projectional.Checks;
using Projectional.CSharp;
using Projectional.CSharp.Checks;
using Projectional.CSharp.Generation;
using Projectional.CSharp.Intentions;
using Projectional.CSharp.Notation;
using Projectional.CSharp.Scopes;
using Projectional.CSharp.Types;
using Projectional.Language;
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

        // The field comes first, which is where the convention puts it: a method before a field would be a style
        // finding of its own and would hide the three this test is about.
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);

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
    public void TheStyleRulesReportTheConventionsTheyState()
    {
        // Three style rules, each the .NET convention stated as a check: imports go System first and then
        // alphabetically, a class's members go fields before methods, and an interface's name starts with I. Each is
        // a warning rather than an error, because the code would still mean the same thing either way.
        var model = new Project(CSharpLanguage.Concepts).NewModel("style");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        ns.Set(CSharpLanguage.NameSlot, "Sample");

        // System.Text before System: out of order.
        var text = model.NewChild(file, CSharpConceptKeys.UsingsSlot, CSharpConceptKeys.Using);
        var system = model.NewChild(file, CSharpConceptKeys.UsingsSlot, CSharpConceptKeys.Using);
        text.Set(CSharpLanguage.NameSlot, "System.Text");
        system.Set(CSharpLanguage.NameSlot, "System");

        // A method before a field: out of order.
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        cls.Set(CSharpLanguage.NameSlot, "Broken");
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        method.Set(CSharpLanguage.NameSlot, "Run");
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        field.Set(CSharpLanguage.NameSlot, "value");

        // An interface without its I.
        var shape = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Interface);
        shape.Set(CSharpLanguage.NameSlot, "Shape");

        var codes = new ConstraintEngine(CSharpConstraints.All).Check(model)
            .Select(finding => finding.Code)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(CSharpConstraints.UsingOrderCode, codes);
        Assert.Contains(CSharpConstraints.MemberOrderCode, codes);
        Assert.Contains(CSharpConstraints.InterfaceNameCode, codes);
    }

    [Fact]
    public void AStyleRuleIsSilentWhenTheConventionIsMet()
    {
        var model = new Project(CSharpLanguage.Concepts).NewModel("style");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        ns.Set(CSharpLanguage.NameSlot, "Sample");

        // System first, then System.Text: in order. A field, then a method: in order. INamed: named as asked.
        var system = model.NewChild(file, CSharpConceptKeys.UsingsSlot, CSharpConceptKeys.Using);
        var text = model.NewChild(file, CSharpConceptKeys.UsingsSlot, CSharpConceptKeys.Using);
        system.Set(CSharpLanguage.NameSlot, "System");
        text.Set(CSharpLanguage.NameSlot, "System.Text");

        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        cls.Set(CSharpLanguage.NameSlot, "Ordered");
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        field.Set(CSharpLanguage.NameSlot, "value");
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        method.Set(CSharpLanguage.NameSlot, "Run");

        var named = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Interface);
        named.Set(CSharpLanguage.NameSlot, "INamed");

        var codes = new ConstraintEngine(CSharpConstraints.All).Check(model)
            .Select(finding => finding.Code)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(CSharpConstraints.UsingOrderCode, codes);
        Assert.DoesNotContain(CSharpConstraints.MemberOrderCode, codes);
        Assert.DoesNotContain(CSharpConstraints.InterfaceNameCode, codes);
    }

    [Fact]
    public void AConstructorIsNamedAfterTheClassThatHoldsIt()
    {
        // C# names a constructor after its type, and here the name is a property of the constructor, so the two can
        // disagree - and the generator writes the constructor's own name, which turns the disagreement into
        // `public Grete()` inside `class Greeter`. Nothing else reports it: "Grete" is a legal identifier, it is
        // PascalCase as the concept asks, and no sibling shares it. An empty name is the same fault with the other
        // spelling, and the likelier one, because a new constructor starts empty and the concept declares no
        // placeholder for the language's own placeholder rule to see.
        var model = new Project(CSharpLanguage.Concepts).NewModel("ctors");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.NameSlot, "Greeter");

        var named = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Constructor);
        named.Set(CSharpLanguage.NameSlot, "Greeter");

        var renamed = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Constructor);
        renamed.Set(CSharpLanguage.NameSlot, "Grete");

        _ = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Constructor);

        var findings = new ConstraintEngine(CSharpConstraints.All)
            .Check(model)
            .Where(finding => string.Equals(finding.Code, CSharpConstraints.ConstructorNameCode, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, findings.Count);

        // Both messages name the class, because "the name is wrong" without saying what it should be is a puzzle.
        Assert.All(findings, finding => Assert.Contains("\"Greeter\"", finding.Message, StringComparison.Ordinal));
        Assert.Contains(findings, finding => finding.Message.Contains("\"Grete\"", StringComparison.Ordinal));
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

    [Fact]
    public void AForLoopAndAPostfixOperatorDrawAndGenerateTheSameShape()
    {
        // The two that belong together: `for (int i = 0; i < 3; i++)` is only writable because the increment is an
        // expression. The header draws both separators itself, so the initializer is a `ForInitializer` - a declaration
        // with no `;` of its own - and the counter still resolves from the condition, the increment and the body.
        var model = new Project(CSharpLanguage.Concepts).NewModel("counting");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var loop = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.For);
        var init = model.NewChild(loop, CSharpConceptKeys.InitializerSlot, CSharpConceptKeys.ForInitializer);
        var initType = model.NewChild(init, "type", CSharpConceptKeys.TypeReference);
        var start = model.NewChild(init, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.IntLiteral);
        var condition = model.NewChild(loop, CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.Binary);
        var left = model.NewChild(condition, "left", CSharpConceptKeys.Name);
        var right = model.NewChild(condition, "right", CSharpConceptKeys.IntLiteral);
        var increment = model.NewChild(loop, CSharpConceptKeys.IncrementSlot, CSharpConceptKeys.Unary);
        var counter = model.NewChild(increment, CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Name);
        var loopBody = model.NewChild(loop, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        model.NewChild(loopBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Break);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Counting");
        method.Set(CSharpLanguage.NameSlot, "Count");
        returnType.Set(CSharpLanguage.NameSlot, "void");
        init.Set(CSharpLanguage.NameSlot, "i");
        initType.Set(CSharpLanguage.NameSlot, "int");
        start.Set(CSharpLanguage.ValueProperty, "0");
        condition.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.LessThan);
        left.SetReference(CSharpConceptKeys.TargetSlot, init);
        right.Set(CSharpLanguage.ValueProperty, "3");
        increment.Set(CSharpLanguage.UnaryOperatorSlot, CSharpUnaryOperator.Increment);
        increment.Set(CSharpLanguage.IsPostfixSlot, "true");
        counter.SetReference(CSharpConceptKeys.TargetSlot, init);

        // The counter is a declaration a `Name` resolves to, from the condition and from the increment - its siblings
        // under the header - and not only from the body, which is what implementing `NamedDeclaration` buys.
        Assert.Contains(
            CSharpScopes.Registry.ScopeOf(left, CSharpConceptKeys.TargetSlot).Entries,
            entry => entry.Name == "i");
        Assert.Contains(
            CSharpScopes.Registry.ScopeOf(counter, CSharpConceptKeys.TargetSlot).Entries,
            entry => entry.Name == "i");

        const string Expected =
            """
            namespace Sample
            {
              public class Counting
              {
                public void Count()
                {
                  for (int i = 0; i < 3; i++)
                  {
                    break;
                  }
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void OnlyADeclarationCanBeOfferedForAName()
    {
        // The last step between the scope and the completion list: an entry is offered only when it fits the slot's
        // target concept. A name may point at a declaration, so a method - which is a member and a named node, but not
        // a NamedDeclaration - must not appear in the list, and a field must.
        var model = new Project(CSharpLanguage.Concepts).NewModel("offers");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var statement = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var name = model.NewChild(statement, "expression", CSharpConceptKeys.Name);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.NameSlot, "Offers");
        field.Set(CSharpLanguage.NameSlot, "count");
        method.Set(CSharpLanguage.NameSlot, "Run");

        var entries = CSharpScopes.Registry.ScopeOf(name, CSharpConceptKeys.TargetSlot).Entries;

        Assert.Contains(entries, entry => entry.Name == "count" && entry.Fits(CSharpConceptKeys.NamedDeclaration, CSharpLanguage.Concepts));
        Assert.DoesNotContain(entries, entry => entry.Name == "Run" && entry.Fits(CSharpConceptKeys.NamedDeclaration, CSharpLanguage.Concepts));
    }

    [Fact]
    public void AClassExtendsABaseAndImplementsAnInterface()
    {
        // Inheritance is two references on the class, and an interface is a class whose members have no bodies - which
        // is why the method's body became optional and why the rule about a missing body skips an interface.
        var model = new Project(CSharpLanguage.Concepts).NewModel("shapes");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var baseClass = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var shape = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Interface);
        var contract = model.NewChild(shape, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Signature);
        var contractReturn = model.NewChild(contract, "returnType", CSharpConceptKeys.TypeReference);
        var derived = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        baseClass.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        baseClass.Set(CSharpLanguage.NameSlot, "Shape");
        shape.Set(CSharpLanguage.NameSlot, "INamed");
        contract.Set(CSharpLanguage.NameSlot, "Describe");
        contractReturn.Set(CSharpLanguage.NameSlot, "string");
        derived.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        derived.Set(CSharpLanguage.NameSlot, "Circle");
        var inherits = model.NewChild(derived, CSharpConceptKeys.InheritanceSlot, CSharpConceptKeys.Inheritance);

        model.NewChild(inherits, CSharpConceptKeys.EntriesSlot, CSharpConceptKeys.InheritanceEntry)
            .SetReference(CSharpConceptKeys.TargetSlot, baseClass);

        model.NewChild(inherits, CSharpConceptKeys.EntriesSlot, CSharpConceptKeys.InheritanceEntry)
            .SetReference(CSharpConceptKeys.TargetSlot, shape);

        const string Expected =
            """
            namespace Sample
            {
              public class Shape
              {
              }

              public interface INamed
              {
                public string Describe();
              }

              public class Circle : Shape, INamed
              {
              }
            }
            """;

        // The editor shows a placeholder where a member list is empty, because a slot the user has to fill has to be
        // somewhere the caret can stand; the generator writes nothing there, because a placeholder is not code. That
        // is the one place the two deliberately differ, and asserting it says so instead of hiding it.
        const string Drawn =
            """
            namespace Sample
            {
              public class Shape
              {
                <no members>
              }

              public interface INamed
              {
                public string Describe();
              }

              public class Circle : Shape, INamed
              {
                <no members>
              }
            }
            """;

        Assert.Equal(Drawn, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void TheNewerExpressionFormsDrawAndGenerateTheSameShape()
    {
        // The three that make a condition out of a value: a negation, a choice and an accumulating assignment. Each is
        // one concept with one property, so the drawing and the generator stay one step apart rather than two.
        var model = new Project(CSharpLanguage.Concepts).NewModel("expressions");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var fieldType = model.NewChild(field, "type", CSharpConceptKeys.TypeReference);

        // int next = !ready ? 0 : count;  - a negation inside the condition of a ternary.
        var local = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);
        var localType = model.NewChild(local, "type", CSharpConceptKeys.TypeReference);
        var choice = model.NewChild(local, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Conditional);
        var condition = model.NewChild(choice, CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.Unary);
        var ready = model.NewChild(condition, CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Name);
        var zero = model.NewChild(choice, CSharpLanguage.WhenTrueSlot, CSharpConceptKeys.IntLiteral);
        var other = model.NewChild(choice, CSharpLanguage.WhenFalseSlot, CSharpConceptKeys.Name);

        // count += next;
        var accumulate = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Assignment);
        var target = model.NewChild(accumulate, "target", CSharpConceptKeys.Name);
        var increment = model.NewChild(accumulate, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Name);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Forms");
        method.Set(CSharpLanguage.NameSlot, "Pick");
        returnType.Set(CSharpLanguage.NameSlot, "void");
        field.Set(CSharpLanguage.NameSlot, "count");
        fieldType.Set(CSharpLanguage.NameSlot, "int");
        local.Set(CSharpLanguage.NameSlot, "next");
        localType.Set(CSharpLanguage.NameSlot, "int");
        condition.Set(CSharpLanguage.UnaryOperatorSlot, CSharpUnaryOperator.Not);
        ready.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "ready");
        zero.Set(CSharpLanguage.ValueProperty, "0");
        other.SetReference(CSharpConceptKeys.TargetSlot, field);
        target.SetReference(CSharpConceptKeys.TargetSlot, field);
        accumulate.Set(CSharpLanguage.AssignmentOperatorSlot, CSharpAssignmentOperator.Add);
        increment.SetReference(CSharpConceptKeys.TargetSlot, local);

        const string Expected =
            """
            namespace Sample
            {
              public class Forms
              {
                public void Pick()
                {
                  int next = !ready ? 0 : count;
                  count += next;
                }

                private int count;
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void AConstructorAndAnAutoPropertyDrawAndGenerateTheSameShape()
    {
        // What makes a class one you can instantiate: a constructor, and a property that keeps a value. The two forms
        // of the property are one boolean apart, which is the same bargain the file-scoped namespace makes.
        var model = new Project(CSharpLanguage.Concepts).NewModel("shape");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var ctor = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Constructor);
        var parameter = model.NewChild(ctor, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
        var parameterType = model.NewChild(parameter, "type", CSharpConceptKeys.TypeReference);
        var ctorBody = model.NewChild(ctor, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var assign = model.NewChild(ctorBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Assignment);
        var target = model.NewChild(assign, "target", CSharpConceptKeys.Name);
        var value = model.NewChild(assign, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Name);
        var readWrite = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Property);
        var propertyType = model.NewChild(readWrite, "type", CSharpConceptKeys.TypeReference);
        var readOnly = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Property);
        var otherType = model.NewChild(readOnly, "type", CSharpConceptKeys.TypeReference);
        var name = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var nameType = model.NewChild(name, "type", CSharpConceptKeys.TypeReference);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Greeter");
        ctor.Set(CSharpLanguage.NameSlot, "Greeter");
        parameter.Set(CSharpLanguage.NameSlot, "message");
        parameterType.Set(CSharpLanguage.NameSlot, "string");
        target.SetReference(CSharpConceptKeys.TargetSlot, name);
        value.SetReference(CSharpConceptKeys.TargetSlot, parameter);
        readWrite.Set(CSharpLanguage.NameSlot, "Message");
        propertyType.Set(CSharpLanguage.NameSlot, "string");
        readOnly.Set(CSharpLanguage.NameSlot, "Created");
        readOnly.Set(CSharpConceptKeys.HasSetterSlot, "false");
        otherType.Set(CSharpLanguage.NameSlot, "int");
        name.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Private);
        name.Set(CSharpLanguage.NameSlot, "message");
        nameType.Set(CSharpLanguage.NameSlot, "string");

        const string Expected =
            """
            namespace Sample
            {
              public class Greeter
              {
                public Greeter(string message)
                {
                  message = message;
                }

                public string Message { get; set; }

                public int Created { get; }

                private string message;
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void APropertyWithAccessorBodiesDrawsAndGeneratesTheSameShape()
    {
        // The third shape of a property: the accessors are written out. It is the same concept as the auto-property,
        // two booleans apart, so one declaration never needs a second name - and a property's name now takes the
        // property's type, which is what a reader of the getter expects.
        var model = new Project(CSharpLanguage.Concepts).NewModel("accessors");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var fieldType = model.NewChild(field, "type", CSharpConceptKeys.TypeReference);
        var property = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Property);
        var propertyType = model.NewChild(property, "type", CSharpConceptKeys.TypeReference);
        var getter = model.NewChild(property, CSharpConceptKeys.GetterSlot, CSharpConceptKeys.Block);
        var setter = model.NewChild(property, CSharpConceptKeys.SetterSlot, CSharpConceptKeys.Block);

        var read = model.NewChild(getter, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
        var readValue = model.NewChild(read, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Name);
        var write = model.NewChild(setter, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Assignment);
        var writeTarget = model.NewChild(write, "target", CSharpConceptKeys.Name);
        var writeValue = model.NewChild(write, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Name);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Person");
        field.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Private);
        field.Set(CSharpLanguage.NameSlot, "age");
        fieldType.Set(CSharpLanguage.NameSlot, "int");
        property.Set(CSharpLanguage.NameSlot, "Age");
        property.Set(CSharpConceptKeys.HasBodiesSlot, "true");
        propertyType.Set(CSharpLanguage.NameSlot, "int");
        readValue.SetReference(CSharpConceptKeys.TargetSlot, field);
        writeTarget.SetReference(CSharpConceptKeys.TargetSlot, field);
        writeValue.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "value");

        const string Expected =
            """
            namespace Sample
            {
              public class Person
              {
                private int age;

                public int Age
                {
                  get
                  {
                    return age;
                  }
                  set
                  {
                    age = value;
                  }
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);

        // A property's name takes the property's type, so a reader of `Age` gets an `int` without a second rule.
        Assert.Equal("int", CSharpTypes.Registry.TypeOf(property).Name);
    }

    [Fact]
    public void AnIndexerDrawsAndGeneratesTheSameShape()
    {
        // An indexer is its own concept rather than a property with a parameter list, because it has no name at all:
        // an empty name slot is a row the layout keeps, so a property would draw a placeholder where a name belongs.
        // It always has bodies, so it needs no `hasBodies` boolean, and its accessors are the same `Block`s a
        // property's are - so the notation and the generator delegate to the shape they already had.
        var model = new Project(CSharpLanguage.Concepts).NewModel("indexers");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var indexer = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Indexer);
        var indexerType = model.NewChild(indexer, "type", CSharpConceptKeys.TypeReference);
        var parameter = model.NewChild(indexer, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
        var parameterType = model.NewChild(parameter, "type", CSharpConceptKeys.TypeReference);
        var getter = model.NewChild(indexer, CSharpConceptKeys.GetterSlot, CSharpConceptKeys.Block);
        var setter = model.NewChild(indexer, CSharpConceptKeys.SetterSlot, CSharpConceptKeys.Block);

        var read = model.NewChild(getter, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
        var readValue = model.NewChild(read, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Name);
        var write = model.NewChild(setter, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Assignment);
        var writeTarget = model.NewChild(write, "target", CSharpConceptKeys.Name);
        var writeValue = model.NewChild(write, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Name);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Scores");
        indexerType.Set(CSharpLanguage.NameSlot, "int");
        parameter.Set(CSharpLanguage.NameSlot, "index");
        parameterType.Set(CSharpLanguage.NameSlot, "int");
        readValue.SetReference(CSharpConceptKeys.TargetSlot, parameter);
        writeTarget.SetReference(CSharpConceptKeys.TargetSlot, parameter);
        writeValue.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "value");

        const string Expected =
            """
            namespace Sample
            {
              public class Scores
              {
                public int this[int index]
                {
                  get
                  {
                    return index;
                  }
                  set
                  {
                    index = value;
                  }
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void ASwitchDrawsAndGeneratesTheSameShape()
    {
        // A switch is a statement of its own: the value it chooses on and the sections that answer it. A section is
        // its own concept because it only exists inside a switch, and `isDefault` picks the keyword - a notation
        // cannot ask "is this slot empty" and choose a word from the answer.
        var model = new Project(CSharpLanguage.Concepts).NewModel("switches");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var @class = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var field = model.NewChild(@class, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var fieldType = model.NewChild(field, "type", CSharpConceptKeys.TypeReference);
        var method = model.NewChild(@class, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);

        var choice = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Switch);
        var subject = model.NewChild(choice, CSharpConceptKeys.SubjectSlot, CSharpConceptKeys.Name);
        var first = model.NewChild(choice, CSharpConceptKeys.SectionsSlot, CSharpConceptKeys.SwitchSection);
        var label = model.NewChild(first, CSharpConceptKeys.LabelSlot, CSharpConceptKeys.IntLiteral);
        var second = model.NewChild(choice, CSharpConceptKeys.SectionsSlot, CSharpConceptKeys.SwitchSection);

        model.NewChild(first, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Break);
        model.NewChild(second, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Break);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        @class.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        @class.Set(CSharpLanguage.NameSlot, "Choices");
        field.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        field.Set(CSharpLanguage.NameSlot, "count");
        fieldType.Set(CSharpLanguage.NameSlot, "int");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "Choose");
        returnType.Set(CSharpLanguage.NameSlot, "void");
        subject.SetReference(CSharpConceptKeys.TargetSlot, field);
        label.Set(CSharpLanguage.ValueProperty, "0");
        second.Set(CSharpConceptKeys.IsDefaultSlot, "true");

        const string Expected =
            """
            namespace Sample
            {
              public class Choices
              {
                public int count;

                public void Choose()
                {
                  switch (count)
                  {
                    case 0:
                      break;
                    default:
                      break;
                  }
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void AFileWithUsingsDrawsAndGeneratesTheSameShape()
    {
        // A file imports the namespaces it names without qualifying them. The imports are a concept of their own
        // because a using names a namespace, not a type: there is no declaration for it to resolve to, so its name
        // is a property. They matter beyond tidiness - generated C# does not compile without them, because `Console`
        // is not in scope without `using System;`.
        var model = new Project(CSharpLanguage.Concepts).NewModel("imports");
        var file = model.NewNode(CSharpConceptKeys.File);
        var system = model.NewChild(file, CSharpConceptKeys.UsingsSlot, CSharpConceptKeys.Using);
        var collections = model.NewChild(file, CSharpConceptKeys.UsingsSlot, CSharpConceptKeys.Using);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var @class = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var field = model.NewChild(@class, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var fieldType = model.NewChild(field, "type", CSharpConceptKeys.TypeReference);

        system.Set(CSharpLanguage.NameSlot, "System");
        collections.Set(CSharpLanguage.NameSlot, "System.Collections.Generic");
        ns.Set(CSharpLanguage.NameSlot, "Sample");
        @class.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        @class.Set(CSharpLanguage.NameSlot, "Greeter");
        field.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Private);
        field.Set(CSharpLanguage.NameSlot, "message");
        fieldType.Set(CSharpLanguage.NameSlot, "string");

        const string Expected =
            """
            using System;
            using System.Collections.Generic;

            namespace Sample
            {
              public class Greeter
              {
                private string message;
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void ABlankLineSeparatesTheImportsFromTheNamespace()
    {
        // The one gap a group boundary cannot describe: the blank line separates the last directive from the namespace
        // declaration, not two directives from each other. It belongs to the when-filled block around the imports, so
        // a file without them must not open with a blank row - which is the half that would break if the gap hung off
        // the file instead of off the list. A class is in the model so nothing here is drawn as a placeholder, which
        // keeps the comparison against the generated text exact.
        const string Imported =
            """
            using System;
            using System.Text;

            namespace Sample;

            public class Greeter
            {
              private string message;
            }
            """;

        const string Bare =
            """
            namespace Sample;

            public class Greeter
            {
              private string message;
            }
            """;

        Assert.Equal(Imported, Document(withImports: true));
        Assert.Equal(Bare, Document(withImports: false));

        static string Document(bool withImports)
        {
            var model = new Project(CSharpLanguage.Concepts).NewModel("imports");
            var file = model.NewNode(CSharpConceptKeys.File);

            if (withImports)
            {
                foreach (var name in new[] { "System", "System.Text" })
                {
                    model.NewChild(file, CSharpConceptKeys.UsingsSlot, CSharpConceptKeys.Using)
                        .Set(CSharpLanguage.NameSlot, name);
                }
            }

            var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
            var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
            var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
            var fieldType = model.NewChild(field, "type", CSharpConceptKeys.TypeReference);

            ns.Set(CSharpLanguage.NameSlot, "Sample");
            ns.Set(CSharpLanguage.IsFileScopedSlot, true);
            cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
            cls.Set(CSharpLanguage.NameSlot, "Greeter");
            field.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Private);
            field.Set(CSharpLanguage.NameSlot, "message");
            fieldType.Set(CSharpLanguage.NameSlot, "string");

            var drawn = LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120);
            var generated = new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text;

            // The notation and the generator declare the gap separately, so agreeing here is what keeps the two from
            // drifting into a file that draws one blank line and writes another.
            Assert.Equal(drawn + "\n", generated);

            return drawn;
        }
    }

    [Fact]
    public void AnAsAndANullDrawAndGenerateTheSameShape()
    {
        // `as` asks `is`'s question and hands the value back - the two in one word. `null` is the one literal with no
        // slot and no type of its own: in C# it takes the type of whatever it is assigned to, so a rule that named
        // one would be wrong wherever the target was something else.
        var model = new Project(CSharpLanguage.Concepts).NewModel("operands");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var @class = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(@class, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);

        var read = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);
        var readType = model.NewChild(read, "type", CSharpConceptKeys.TypeReference);
        var asExpression = model.NewChild(read, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.As);
        var asOperand = model.NewChild(asExpression, CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Name);
        var asType = model.NewChild(asExpression, "type", CSharpConceptKeys.TypeReference);

        var clear = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);
        var clearType = model.NewChild(clear, "type", CSharpConceptKeys.TypeReference);
        var nothing = model.NewChild(clear, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.NullLiteral);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        @class.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        @class.Set(CSharpLanguage.NameSlot, "Reads");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "Read");
        returnType.Set(CSharpLanguage.NameSlot, "void");
        readType.Set(CSharpLanguage.NameSlot, "string");
        clearType.Set(CSharpLanguage.NameSlot, "string");
        read.Set(CSharpLanguage.NameSlot, "text");
        clear.Set(CSharpLanguage.NameSlot, "empty");
        asOperand.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "value");
        asType.Set(CSharpLanguage.NameSlot, "string");

        const string Expected =
            """
            namespace Sample
            {
              public class Reads
              {
                public void Read()
                {
                  string text = value as string;
                  string empty = null;
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);

        // `as` claims the type it names; `null` deliberately claims none, so the type system says nothing about it.
        Assert.Equal("string", CSharpTypes.Registry.TypeOf(asExpression).Name);
        Assert.Equal(UnknownType.Instance.Name, CSharpTypes.Registry.TypeOf(nothing).Name);
    }

    [Fact]
    public void AnEnumDrawsAndGeneratesTheSameShape()
    {
        // An enum is a type declaration whose values are a closed set of names. The values are a collection, not a
        // children list, because the comma between them is punctuation that belongs to the list rather than to the
        // value - and the value draws nothing but its name.
        var model = new Project(CSharpLanguage.Concepts).NewModel("enums");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var colour = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Enum);

        foreach (var name in new[] { "Red", "Green", "Blue" })
        {
            model.NewChild(colour, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.EnumMember)
                .Set(CSharpLanguage.NameSlot, name);
        }

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        colour.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        colour.Set(CSharpLanguage.NameSlot, "Colour");

        const string Expected =
            """
            namespace Sample
            {
              public enum Colour
              {
                Red, Green, Blue
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void AValueOfAnEnumerationOfTheLanguageIsNamedInTheModelAndTextInTheCode()
    {
        // MPS' spelling for an enumeration value, in both of its forms - with one difference that is the whole point of
        // the concepts: the enumeration is one of the *language being defined*, not one of C#. `structure/` declares it,
        // the registry carries it only as a property's allowed symbols, and at run time a symbol is text, because
        // `Node`'s property answer is a `string`. So the model says which enumeration and which symbol - which is what
        // the definition layer closes and what completion offers - and the generated code is the symbol's name.
        var model = new Project(CSharpLanguage.Concepts).NewModel("enum-values");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var fieldType = model.NewChild(field, "type", CSharpConceptKeys.EnumMemberType);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var parameter = model.NewChild(method, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
        var parameterType = model.NewChild(parameter, "type", CSharpConceptKeys.EnumMemberType);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var @return = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
        var value = model.NewChild(@return, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.EnumValue);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Calendar");
        field.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Private);
        field.Set(CSharpLanguage.NameSlot, "rest");
        fieldType.Set(CSharpLanguage.NameSlot, "WeekDays");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "Same");
        returnType.Set(CSharpLanguage.NameSlot, "bool");
        parameter.Set(CSharpLanguage.NameSlot, "day");
        parameterType.Set(CSharpLanguage.NameSlot, "WeekDays");
        value.Set(CSharpLanguage.EnumSlot, "WeekDays");
        value.Set(CSharpLanguage.MemberSlot, "Thu");

        // Drawn: the two forms of the model's own vocabulary.
        const string Drawn =
            """
            namespace Sample
            {
              public class Calendar
              {
                private enummember<WeekDays> rest;

                public bool Same(enummember<WeekDays> day)
                {
                  return enum/WeekDays/.Thu;
                }
              }
            }
            """;

        // Generated: what C# accepts. A symbol is text, so the type is `string` and the value is its name.
        const string Generated =
            """
            namespace Sample
            {
              public class Calendar
              {
                private string rest;

                public bool Same(string day)
                {
                  return "Thu";
                }
              }
            }
            """;

        Assert.Equal(Drawn, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Generated + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);

        // Both forms are the same type, which is what makes `day == enum/WeekDays/.Thu` a comparison the rules can see
        // - and that type is text, because a property value is stored as the text it was written with.
        Assert.Equal("string", CSharpTypes.Registry.TypeOf(value).Name);
        Assert.Equal("string", CSharpTypes.Registry.TypeOf(fieldType).Name);
    }

    [Fact]
    public void ALambdaIsCodeHandedToACallWhichIsWhatLinqNeeds()
    {
        // The expression form the subset was missing, and the one whose absence made "LINQ is sugar over method calls"
        // true only on paper: `values.Where(item => item > 0)` is a call whose argument is *code*, and there was no code
        // to pass. The parameters are ordinary `Parameter` nodes, which is what makes the body see them - the same
        // scope that answers a method's parameters answers a lambda's.
        var model = new Project(CSharpLanguage.Concepts).NewModel("linq");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var parameters = model.NewChild(method, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
        var valuesType = model.NewChild(parameters, "type", CSharpConceptKeys.TypeReference);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var @return = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
        var call = model.NewChild(@return, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Call);
        var callee = model.NewChild(call, "callee", CSharpConceptKeys.MemberAccess);
        var values = model.NewChild(callee, "target", CSharpConceptKeys.Name);
        var lambda = model.NewChild(call, CSharpConceptKeys.ArgumentsSlot, CSharpConceptKeys.Lambda);
        var item = model.NewChild(lambda, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.LambdaParameter);
        var comparison = model.NewChild(lambda, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Binary);
        var use = model.NewChild(comparison, "left", CSharpConceptKeys.Name);
        var zero = model.NewChild(comparison, "right", CSharpConceptKeys.IntLiteral);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Numbers");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "Positive");
        method.Set(CSharpLanguage.IsStaticSlot, true);
        parameters.Set(CSharpLanguage.NameSlot, "values");
        valuesType.Set(CSharpLanguage.NameSlot, "IEnumerable<int>");
        returnType.Set(CSharpLanguage.NameSlot, "IEnumerable<int>");
        callee.Set(CSharpLanguage.MemberSlot, "Where");
        item.Set(CSharpLanguage.NameSlot, "item");
        comparison.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.GreaterThan);
        zero.Set(CSharpLanguage.ValueProperty, "0");

        // `values` is the method's parameter and `item` the lambda's, and both resolve through the one rule the language
        // declares - a name sees the declarations in the nodes that enclose it, nearest first.
        values.SetReference(CSharpConceptKeys.TargetSlot, parameters);

        var inLambda = CSharpScopes.Registry.ScopeOf(use, CSharpConceptKeys.TargetSlot).Entries;

        Assert.Contains(inLambda, entry => entry.Name == "item" && entry.Fits(CSharpConceptKeys.NamedDeclaration, CSharpLanguage.Concepts));

        use.SetReference(CSharpConceptKeys.TargetSlot, item);

        const string Expected =
            """
            namespace Sample
            {
              public class Numbers
              {
                public static IEnumerable<int> Positive(IEnumerable<int> values)
                {
                  return values.Where((item) => item > 0);
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);

        // A lambda's type is the delegate the target asks for, which this language does not model - so the honest answer
        // is unknown rather than a name for something the type system cannot describe.
        Assert.IsType<UnknownType>(CSharpTypes.Registry.TypeOf(lambda));
    }

    [Fact]
    public void ALambdaWithoutParametersDrawsItsEmptyPairOfParentheses()
    {
        // `() => 5`: the parentheses are always drawn, so the one-parameter shortening is the only thing given up - and
        // an empty list draws nothing between them rather than a placeholder, because an empty pair is a real argument
        // list in C#.
        var model = new Project(CSharpLanguage.Concepts).NewModel("no-parameters");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var @return = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
        var call = model.NewChild(@return, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Call);
        var callee = model.NewChild(call, "callee", CSharpConceptKeys.Name);
        var lambda = model.NewChild(call, CSharpConceptKeys.ArgumentsSlot, CSharpConceptKeys.Lambda);
        var five = model.NewChild(lambda, CSharpConceptKeys.BodySlot, CSharpConceptKeys.IntLiteral);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Clock");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "Run");
        method.Set(CSharpLanguage.IsStaticSlot, true);
        returnType.Set(CSharpLanguage.NameSlot, "void");
        five.Set(CSharpLanguage.ValueProperty, "5");

        callee.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "Later");

        const string Expected =
            """
            namespace Sample
            {
              public class Clock
              {
                public static void Run()
                {
                  return Later(() => 5);
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void AQueryDrawsAndGeneratesTheQuerySyntax()
    {
        // The query syntax the lambda made writable: `from item in values where item > 0 select item` is the same tree
        // as `values.Where(item => item > 0)`, written the way C# lets an author write it. The range variable is the
        // query's own name, so the query is a `NamedDeclaration` and a name inside a clause resolves through the one
        // scope rule the language states - no second rule for query scope.
        var model = new Project(CSharpLanguage.Concepts).NewModel("query");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var parameters = model.NewChild(method, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
        var valuesType = model.NewChild(parameters, "type", CSharpConceptKeys.TypeReference);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var @return = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
        var query = model.NewChild(@return, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.QueryExpression);
        var source = model.NewChild(query, CSharpConceptKeys.SourceSlot, CSharpConceptKeys.Name);
        var where = model.NewChild(query, CSharpConceptKeys.ClausesSlot, CSharpConceptKeys.WhereClause);
        var comparison = model.NewChild(where, CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.Binary);
        var use = model.NewChild(comparison, "left", CSharpConceptKeys.Name);
        var zero = model.NewChild(comparison, "right", CSharpConceptKeys.IntLiteral);
        var orderBy = model.NewChild(query, CSharpConceptKeys.ClausesSlot, CSharpConceptKeys.OrderByClause);
        var ordered = model.NewChild(orderBy, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Name);
        var select = model.NewChild(query, CSharpConceptKeys.SelectSlot, CSharpConceptKeys.Name);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Numbers");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "Positive");
        method.Set(CSharpLanguage.IsStaticSlot, true);
        parameters.Set(CSharpLanguage.NameSlot, "values");
        valuesType.Set(CSharpLanguage.NameSlot, "IEnumerable<int>");
        returnType.Set(CSharpLanguage.NameSlot, "IEnumerable<int>");
        query.Set(CSharpLanguage.NameSlot, "item");
        comparison.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.GreaterThan);
        zero.Set(CSharpLanguage.ValueProperty, "0");
        orderBy.Set(CSharpConceptKeys.DescendingSlot, true);

        source.SetReference(CSharpConceptKeys.TargetSlot, parameters);
        use.SetReference(CSharpConceptKeys.TargetSlot, query);
        ordered.SetReference(CSharpConceptKeys.TargetSlot, query);
        select.SetReference(CSharpConceptKeys.TargetSlot, query);

        var inWhere = CSharpScopes.Registry.ScopeOf(use, CSharpConceptKeys.TargetSlot).Entries;

        Assert.Contains(inWhere, entry => entry.Name == "item" && entry.Fits(CSharpConceptKeys.NamedDeclaration, CSharpLanguage.Concepts));

        // Drawn: every clause hangs under the `from` it follows, and the `;` the return owns stands after the block -
        // which the layout pads to the widest row, because the caret could stand on any of them.
        const string Drawn =
            """
            namespace Sample
            {
              public class Numbers
              {
                public static IEnumerable<int> Positive(IEnumerable<int> values)
                {
                  return from item in values
                         where item > 0
                         orderby item descending
                         select item            ;
                }
              }
            }
            """;

        // Generated: the clauses take one indent level, which is what the template says and what C# accepts - a query
        // may break between its clauses, so the text is the query the drawing shows.
        const string Generated =
            """
            namespace Sample
            {
              public class Numbers
              {
                public static IEnumerable<int> Positive(IEnumerable<int> values)
                {
                  return from item in values
                    where item > 0
                    orderby item descending
                    select item;
                }
              }
            }
            """;

        Assert.Equal(Drawn, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Generated + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void ARecordDrawsItsThreeFormsAndGeneratesThem()
    {
        // A record is a class with two things a class does not have: a positional parameter list that becomes its
        // properties, and a body that is optional. `record Point(int X, int Y);` ends in a semicolon; the same record with
        // members ends in braces; and one keyword apart from the first is a `record struct`. The first two are one
        // property (`hasBody`) because they hold the same slots and differ in one symbol - unlike a signature beside a
        // method, which hold different slots and are therefore two concepts.
        var model = new Project(CSharpLanguage.Concepts).NewModel("records");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);

        // public record Point(int X, int Y);
        var point = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Record);
        Coordinates(model, point, "Point");

        // public record pair(int X, int Y) { public int Sum() { return X + Y; } }
        var named = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Record);
        Coordinates(model, named, "Pair");
        named.Set(CSharpLanguage.HasBodySlot, true);
        var sum = model.NewChild(named, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(sum, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(sum, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var @return = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
        var add = model.NewChild(@return, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Binary);
        var left = model.NewChild(add, "left", CSharpConceptKeys.Name);
        var right = model.NewChild(add, "right", CSharpConceptKeys.Name);

        sum.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        sum.Set(CSharpLanguage.NameSlot, "Sum");
        returnType.Set(CSharpLanguage.NameSlot, "int");
        add.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.Add);

        // The record's own parameters are declarations, so the body reads them through the one scope rule the language
        // states - a record adds no second rule for its primary constructor.
        var primary = named.GetChildren(CSharpConceptKeys.ParametersSlot);

        left.SetReference(CSharpConceptKeys.TargetSlot, primary[0]);
        right.SetReference(CSharpConceptKeys.TargetSlot, primary[1]);

        // public record struct Money(int Amount);
        var money = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Record);
        var amount = model.NewChild(money, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
        var amountType = model.NewChild(amount, "type", CSharpConceptKeys.TypeReference);

        money.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        money.Set(CSharpLanguage.IsStructSlot, true);
        money.Set(CSharpLanguage.NameSlot, "Money");
        amount.Set(CSharpLanguage.NameSlot, "Amount");
        amountType.Set(CSharpLanguage.NameSlot, "int");

        ns.Set(CSharpLanguage.NameSlot, "Sample");

        const string Expected =
            """
            namespace Sample
            {
              public record Point(int X, int Y);

              public record Pair(int X, int Y)
              {
                public int Sum()
                {
                  return X + Y;
                }
              }

              public record struct Money(int Amount);
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    /// <summary>A positional record's two parameters, which is what makes it a record rather than a class.</summary>
    private static void Coordinates(Model model, Node record, string name)
    {
        foreach (var coordinate in new[] { "X", "Y" })
        {
            var parameter = model.NewChild(record, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
            var type = model.NewChild(parameter, "type", CSharpConceptKeys.TypeReference);

            parameter.Set(CSharpLanguage.NameSlot, coordinate);
            type.Set(CSharpLanguage.NameSlot, "int");
        }

        record.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        record.Set(CSharpLanguage.NameSlot, name);
    }

    [Fact]
    public void ALoopDrawsAndGeneratesTheSameShape()
    {
        // A loop is a header and a body, like `if`, so the notation and the generator agree without either knowing
        // about the other - which is the property the whole language rests on.
        var model = new Project(CSharpLanguage.Concepts).NewModel("loops");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var loop = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.While);
        var condition = model.NewChild(loop, CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.BoolLiteral);
        var loopBody = model.NewChild(loop, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var stop = model.NewChild(loopBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Break);
        var each = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ForEach);
        var elementType = model.NewChild(each, "type", CSharpConceptKeys.TypeReference);
        var source = model.NewChild(each, CSharpConceptKeys.SourceSlot, CSharpConceptKeys.Name);
        var eachBody = model.NewChild(each, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var next = model.NewChild(eachBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Continue);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Loops");
        method.Set(CSharpLanguage.NameSlot, "Run");
        returnType.Set(CSharpLanguage.NameSlot, "void");
        condition.Set(CSharpLanguage.ValueProperty, "true");
        elementType.Set(CSharpLanguage.NameSlot, "string");
        each.Set(CSharpLanguage.NameSlot, "line");
        source.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "lines");

        _ = stop;
        _ = next;

        const string Expected =
            """
            namespace Sample
            {
              public class Loops
              {
                public void Run()
                {
                  while (true)
                  {
                    break;
                  }
                  foreach (string line in lines)
                  {
                    continue;
                  }
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void ATypesAreWhatTheOperandsAndTheDeclarationSay()
    {
        // Local propagation and nothing further: a literal knows its own type, a name borrows the type of the
        // declaration it resolves to, and the operator decides what its result is.
        var model = Sample();
        var file = model.Roots[0];
        var ns = file.GetChild(CSharpConceptKeys.NamespaceSlot)!;
        var cls = ns.GetChild(CSharpConceptKeys.ClassesSlot)!;
        var method = cls.GetChildren(CSharpConceptKeys.MembersSlot)
            .First(member => member.Concept.Key == CSharpConceptKeys.Method);
        var body = method.GetChild(CSharpConceptKeys.BodySlot)!;
        var statement = body.GetChild(CSharpConceptKeys.StatementsSlot)!;
        var call = statement.GetChild("expression")!;
        var argument = call.GetChild(CSharpConceptKeys.ArgumentsSlot)!;

        Assert.Equal("string", CSharpTypes.Registry.TypeOf(argument).Name);
        // A comparison is a truth value whatever its operands are, and arithmetic is as wide as its left operand - the
        // operator decides, which is what makes a condition checkable at all.
        var extra = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var comparison = model.NewChild(extra, "expression", CSharpConceptKeys.Binary);
        var left = model.NewChild(comparison, "left", CSharpConceptKeys.IntLiteral);
        var right = model.NewChild(comparison, "right", CSharpConceptKeys.IntLiteral);

        comparison.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.LessThan);
        left.Set(CSharpLanguage.ValueProperty, "1");
        right.Set(CSharpLanguage.ValueProperty, "2");

        Assert.Equal("bool", CSharpTypes.Registry.TypeOf(comparison).Name);

        comparison.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.Add);

        Assert.Equal("int", CSharpTypes.Registry.TypeOf(comparison).Name);

        // A call's return type is not modelled, and the honest answer is that it is not known - not a guess that
        // would make every expression around it look fine.
        Assert.IsType<UnknownType>(CSharpTypes.Registry.TypeOf(call));
    }

    [Fact]
    public void AnIfOverSomethingThatIsNotABooleanIsReported()
    {
        // The first rule the language could not state before it had types: `count` is an int, so `if (count)` is a
        // mistake the model can see. A condition whose type is unknown is *not* reported - that discipline is what
        // keeps a checker usable.
        var model = new Project(CSharpLanguage.Concepts).NewModel("guarded");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var fieldType = model.NewChild(field, "type", CSharpConceptKeys.TypeReference);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var branch = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.If);
        var condition = model.NewChild(branch, CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.Name);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.NameSlot, "Guarded");
        field.Set(CSharpLanguage.NameSlot, "count");
        fieldType.Set(CSharpLanguage.NameSlot, "int");
        method.Set(CSharpLanguage.NameSlot, "Run");
        condition.SetReference(CSharpConceptKeys.TargetSlot, field);

        var engine = new ConstraintEngine(CSharpConstraints.All, scopes: CSharpScopes.Registry, types: CSharpTypes.Registry);

        Assert.Contains(engine.Check(model), finding => finding.Code == CSharpConstraints.ConditionNotBooleanCode);

        // The same condition as a comparison is a bool, so the finding goes away - which is the rule reading the type
        // the operator gives rather than the type of the name.
        var guarded = new Project(CSharpLanguage.Concepts).NewModel("comparison");
        var root = guarded.NewNode(CSharpConceptKeys.File);
        var space = guarded.NewChild(root, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var type = guarded.NewChild(space, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var number = guarded.NewChild(type, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var numberType = guarded.NewChild(number, "type", CSharpConceptKeys.TypeReference);
        var run = guarded.NewChild(type, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var runBody = guarded.NewChild(run, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var comparison = guarded.NewChild(runBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.If);
        var binary = guarded.NewChild(comparison, CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.Binary);
        var left = guarded.NewChild(binary, "left", CSharpConceptKeys.Name);
        var right = guarded.NewChild(binary, "right", CSharpConceptKeys.IntLiteral);

        space.Set(CSharpLanguage.NameSlot, "Sample");
        type.Set(CSharpLanguage.NameSlot, "Guarded");
        number.Set(CSharpLanguage.NameSlot, "count");
        numberType.Set(CSharpLanguage.NameSlot, "int");
        run.Set(CSharpLanguage.NameSlot, "Run");
        binary.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.Equal);
        left.SetReference(CSharpConceptKeys.TargetSlot, number);
        right.Set(CSharpLanguage.ValueProperty, "0");

        Assert.DoesNotContain(
            engine.Check(guarded),
            finding => finding.Code == CSharpConstraints.ConditionNotBooleanCode);
    }

    [Fact]
    public void ANameSeesTheDeclarationsThatEncloseItNearestFirst()
    {
        // The resolution rule, in one place: a name sees what the nodes around it declare. A local in the block that
        // encloses the use, a parameter in the method that encloses the body, a field in the class that encloses both -
        // and the nearest declaration wins without a second rule for shadowing.
        var model = new Project(CSharpLanguage.Concepts).NewModel("scopes");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var parameter = model.NewChild(method, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var statement = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var name = model.NewChild(statement, "expression", CSharpConceptKeys.Name);

        field.Set(CSharpLanguage.NameSlot, "total");
        method.Set(CSharpLanguage.NameSlot, "Compute");
        parameter.Set(CSharpLanguage.NameSlot, "factor");

        var scope = CSharpScopes.Registry.ScopeOf(name, CSharpConceptKeys.TargetSlot);
        var visible = scope.Entries.Select(entry => entry.Name).ToList();

        // The parameter and the field are both visible, and the parameter is nearer: it is declared in the method
        // that encloses the body, while the field is declared one level further out.
        Assert.Contains("factor", visible);
        Assert.Contains("total", visible);
        Assert.True(
            visible.IndexOf("factor") < visible.IndexOf("total"),
            "the nearer declaration has to come first: " + string.Join(", ", visible));
    }

    [Fact]
    public void ANamespaceIsDrawnInTheFormTheModelChose()
    {
        // C# 10's file-scoped form, as one property rather than a second concept: the same node, drawn with a
        // semicolon and everything after it in the namespace, or with a brace and everything between them in it.
        var model = Sample();

        var braced = LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120);

        Assert.StartsWith("namespace Sample\n{\n", braced, StringComparison.Ordinal);
        Assert.EndsWith("}\n}", braced, StringComparison.Ordinal);

        var ns = model.Roots[0].GetChild(CSharpConceptKeys.NamespaceSlot)!;

        ns.Set(CSharpLanguage.IsFileScopedSlot, "true");

        var scoped = LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120);
        var generated = new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text;

        // The header ends the declaration, the class stands at the file's own level instead of one in, and the
        // closing brace that only the block form has is gone. The empty line under the header is the convention's
        // own: a file-scoped namespace declaration is followed by a blank line before the first type.
        Assert.StartsWith("namespace Sample;\n\npublic class Greeter\n{\n  private string message", scoped, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Sample\n{", scoped, StringComparison.Ordinal);
        Assert.EndsWith("  }\n}", scoped, StringComparison.Ordinal);

        // And the generator says the same thing as the notation, which is the property the whole language rests on.
        Assert.StartsWith("namespace Sample;\n\npublic class Greeter\n{\n  private string message", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Sample\n{", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void ACollectionExpressionDrawsAndGeneratesItsElements()
    {
        // C# 12 syntax, and the one construct whose empty form is a value rather than an unfinished node: `[]` is a
        // collection of nothing, so the brackets stand alone instead of showing a placeholder.
        var model = new Project(CSharpLanguage.Concepts).NewModel("collections");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var fieldType = model.NewChild(field, "type", CSharpConceptKeys.TypeReference);
        var empty = model.NewChild(field, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.CollectionExpression);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Collections");
        field.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Private);
        field.Set(CSharpLanguage.NameSlot, "none");
        fieldType.Set(CSharpLanguage.NameSlot, "int[]");

        var notations = CSharpNotations.CreateNotations(CSharpLanguage.Concepts);
        var generator = CSharpTextGenerators.Generator;

        Assert.Contains("private int[] none = [];", LayoutSnapshot.Render(model, notations, width: 120), StringComparison.Ordinal);
        Assert.Equal("[]", new Projectional.Semantics.Generator().Generate(empty, generator).Text);

        var one = model.NewChild(empty, CSharpConceptKeys.ElementsSlot, CSharpConceptKeys.IntLiteral);
        var two = model.NewChild(empty, CSharpConceptKeys.ElementsSlot, CSharpConceptKeys.IntLiteral);

        one.Set(CSharpLanguage.ValueProperty, "1");
        two.Set(CSharpLanguage.ValueProperty, "2");

        Assert.Contains("private int[] none = [1, 2];", LayoutSnapshot.Render(model, notations, width: 120), StringComparison.Ordinal);
        Assert.Equal("[1, 2]", new Projectional.Semantics.Generator().Generate(empty, generator).Text);
    }

    [Fact]
    public void ACastAnIsAndAnIndexDrawAndGenerateTheSameShape()
    {
        // The three expressions whose subject is a type. Each holds its type in a child slot rather than a property,
        // so the type is a node the caret can enter and the type system can read - the same reason a field does.
        var model = new Project(CSharpLanguage.Concepts).NewModel("types");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);

        // int count = (int)value;
        var cast = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);
        var castType = model.NewChild(cast, "type", CSharpConceptKeys.TypeReference);
        var castValue = model.NewChild(cast, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Cast);
        var castTargetType = model.NewChild(castValue, "type", CSharpConceptKeys.TypeReference);
        var castOperand = model.NewChild(castValue, CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Name);

        // bool ready = value is string;
        var test = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);
        var testType = model.NewChild(test, "type", CSharpConceptKeys.TypeReference);
        var testValue = model.NewChild(test, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Is);
        var testOperand = model.NewChild(testValue, CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Name);
        var testTargetType = model.NewChild(testValue, "type", CSharpConceptKeys.TypeReference);

        // int first = value[0];
        var index = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);
        var indexType = model.NewChild(index, "type", CSharpConceptKeys.TypeReference);
        var indexValue = model.NewChild(index, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Index);
        var indexTarget = model.NewChild(indexValue, CSharpConceptKeys.TargetSlot, CSharpConceptKeys.Name);
        var position = model.NewChild(indexValue, CSharpConceptKeys.IndexSlot, CSharpConceptKeys.IntLiteral);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Reads");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "Read");
        returnType.Set(CSharpLanguage.NameSlot, "void");

        cast.Set(CSharpLanguage.NameSlot, "count");
        castType.Set(CSharpLanguage.NameSlot, "int");
        castTargetType.Set(CSharpLanguage.NameSlot, "int");
        test.Set(CSharpLanguage.NameSlot, "ready");
        testType.Set(CSharpLanguage.NameSlot, "bool");
        testTargetType.Set(CSharpLanguage.NameSlot, "string");
        index.Set(CSharpLanguage.NameSlot, "first");
        indexType.Set(CSharpLanguage.NameSlot, "int");
        position.Set(CSharpLanguage.ValueProperty, "0");

        // `value` means nothing this file declares, so all three point at nothing and keep the text they were given -
        // one reference state, three expressions, and the drawing and the text agree on all of them.
        castOperand.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "value");
        testOperand.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "value");
        indexTarget.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "value");

        const string Expected =
            """
            namespace Sample
            {
              public class Reads
              {
                public void Read()
                {
                  int count = (int)value;
                  bool ready = value is string;
                  int first = value[0];
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);

        // A cast is the type it names and an `is` is a truth value; an index is deliberately unknown, because what
        // `target[index]` returns is a fact about the target's type, which this language does not model.
        Assert.Equal("int", CSharpTypes.Registry.TypeOf(castValue).Name);
        Assert.Equal("bool", CSharpTypes.Registry.TypeOf(testValue).Name);
        Assert.IsType<UnknownType>(CSharpTypes.Registry.TypeOf(indexValue));
    }

    [Fact]
    public void APostfixOperatorNeedsNoStatementOfItsOwn()
    {
        // `count++;` is an expression statement whose expression is the postfix form, so the statement concept the
        // language already has carries it. The backlog said "a statement form for ++/--"; the answer is that there is
        // nothing to add, and this test is the evidence rather than an assertion in a document.
        var model = new Project(CSharpLanguage.Concepts).NewModel("postfix");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var statement = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var increment = model.NewChild(statement, "expression", CSharpConceptKeys.Unary);
        var operand = model.NewChild(increment, CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Name);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Counter");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "Step");
        returnType.Set(CSharpLanguage.NameSlot, "void");
        increment.Set(CSharpLanguage.UnaryOperatorSlot, CSharpUnaryOperator.Increment);
        increment.Set(CSharpLanguage.IsPostfixSlot, "true");
        operand.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "count");

        const string Expected =
            """
            namespace Sample
            {
              public class Counter
              {
                public void Step()
                {
                  count++;
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void ATryWithACatchAndAFinallyDrawsAndGeneratesTheSameShape()
    {
        // The statement the subset was missing: what a body does when something goes wrong. The handler is a concept
        // of its own rather than a second block slot, so a try may have several and each binds the exception it
        // catches - and because a handler is a declaration, a name in its body resolves to it.
        var model = new Project(CSharpLanguage.Concepts).NewModel("errors");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);

        var attempt = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Try);
        var tryBody = model.NewChild(attempt, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var handler = model.NewChild(attempt, CSharpConceptKeys.CatchesSlot, CSharpConceptKeys.Catch);
        var handlerType = model.NewChild(handler, "type", CSharpConceptKeys.TypeReference);
        var handlerBody = model.NewChild(handler, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var closing = model.NewChild(attempt, CSharpConceptKeys.FinallySlot, CSharpConceptKeys.Block);

        // The try and the final block give up; the handler writes what it caught.
        model.NewChild(tryBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
        model.NewChild(closing, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);

        var statement = model.NewChild(handlerBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var call = model.NewChild(statement, "expression", CSharpConceptKeys.Call);
        var callee = model.NewChild(call, "callee", CSharpConceptKeys.MemberAccess);
        var console = model.NewChild(callee, "target", CSharpConceptKeys.Name);
        var argument = model.NewChild(call, CSharpConceptKeys.ArgumentsSlot, CSharpConceptKeys.Name);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Attempts");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "Run");
        returnType.Set(CSharpLanguage.NameSlot, "void");
        handlerType.Set(CSharpLanguage.NameSlot, "Exception");
        handler.Set(CSharpLanguage.NameSlot, "error");
        callee.Set(CSharpLanguage.MemberSlot, "WriteLine");
        console.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "Console");

        // The argument means the caught exception, so it is a reference that resolves - which is what the handler
        // being a declaration buys.
        argument.SetReference(CSharpConceptKeys.TargetSlot, handler);

        const string Expected =
            """
            namespace Sample
            {
              public class Attempts
              {
                public void Run()
                {
                  try
                  {
                    return;
                  }
                  catch (Exception error)
                  {
                    Console.WriteLine(error);
                  }
                  finally
                  {
                    return;
                  }
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);

        // The handler's name is what the argument resolves to, so the scope provider sees the catch.
        var scope = CSharpScopes.Registry.ScopeOf(argument, CSharpConceptKeys.TargetSlot);

        Assert.Contains("error", scope.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public void ABlankLineSeparatesAMemberKindFromTheNext()
    {
        // The "blank line after the block of fields" convention, in the two places it can go wrong: a run of fields
        // stays tight, and every other member starts a group. Fields are the one tight kind, so two fields and two
        // methods draw as four rows and three blank lines - and the generator agrees, because both ask the same set
        // where a group ends.
        var model = new Project(CSharpLanguage.Concepts).NewModel("groups");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Groups");

        foreach (var name in new[] { "first", "second" })
        {
            var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
            var type = model.NewChild(field, "type", CSharpConceptKeys.TypeReference);

            field.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Private);
            field.Set(CSharpLanguage.NameSlot, name);
            type.Set(CSharpLanguage.NameSlot, "int");
        }

        foreach (var name in new[] { "One", "Two" })
        {
            var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
            var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
            var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);

            _ = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);

            method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
            method.Set(CSharpLanguage.NameSlot, name);
            returnType.Set(CSharpLanguage.NameSlot, "void");
        }

        const string Expected =
            """
            namespace Sample
            {
              public class Groups
              {
                private int first;
                private int second;

                public void One()
                {
                  return;
                }

                public void Two()
                {
                  return;
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void ABlankLineSeparatesTwoTypesInANamespace()
    {
        // The member list's tight set has fields in it, because members of one kind belong together. The type list
        // has nothing in its set, because types never do - two classes are as separate from each other as a class is
        // from an interface. That is what makes this an empty set rather than a second entry in the same one.
        var model = new Project(CSharpLanguage.Concepts).NewModel("types");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);

        ns.Set(CSharpLanguage.NameSlot, "Sample");

        foreach (var name in new[] { "First", "Second" })
        {
            var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);

            cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
            cls.Set(CSharpLanguage.NameSlot, name);
        }

        const string Expected =
            """
            namespace Sample
            {
              public class First
              {
                <no members>
              }

              public class Second
              {
                <no members>
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
    }

    [Fact]
    public void ABlankLineSeparatesTwoTypesUnderAFileScopedNamespace()
    {
        // The file-scoped form puts the types at the same level as the header, so the gap it draws is the same one -
        // and the generated text has it too, which is the pair that would drift if the two read different rules.
        var model = new Project(CSharpLanguage.Concepts).NewModel("file-scoped");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        ns.Set(CSharpLanguage.IsFileScopedSlot, true);

        foreach (var name in new[] { "First", "Second" })
        {
            var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);

            cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
            cls.Set(CSharpLanguage.NameSlot, name);
        }

        const string Drawn =
            """
            namespace Sample;

            public class First
            {
              <no members>
            }

            public class Second
            {
              <no members>
            }
            """;

        const string Generated =
            """
            namespace Sample;

            public class First
            {
            }

            public class Second
            {
            }

            """;

        Assert.Equal(Drawn, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Generated, new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void ALongAndADoubleLiteralDrawAndGenerateTheSameShape()
    {
        // The two literals the subset was missing, and the two spellings are not the same. A long carries a suffix -
        // `10` alone is an int, so the suffix is what makes the node the concept it claims to be - and the value stays
        // a number, because the suffix is notation rather than data. A double needs nothing: `1.5` already says what it
        // is, and its property type refuses anything that is not a number.
        var model = new Project(CSharpLanguage.Concepts).NewModel("numbers");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var ratio = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);
        var ratioType = model.NewChild(ratio, "type", CSharpConceptKeys.TypeReference);
        var fraction = model.NewChild(ratio, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.DoubleLiteral);
        var total = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);
        var totalType = model.NewChild(total, "type", CSharpConceptKeys.TypeReference);
        var amount = model.NewChild(total, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.LongLiteral);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Numbers");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "Show");
        returnType.Set(CSharpLanguage.NameSlot, "void");
        ratio.Set(CSharpLanguage.NameSlot, "ratio");
        ratioType.Set(CSharpLanguage.NameSlot, "double");
        fraction.Set(CSharpLanguage.ValueProperty, "1.5");
        total.Set(CSharpLanguage.NameSlot, "total");
        totalType.Set(CSharpLanguage.NameSlot, "long");
        amount.Set(CSharpLanguage.ValueProperty, "10");

        const string Expected =
            """
            namespace Sample
            {
              public class Numbers
              {
                public void Show()
                {
                  double ratio = 1.5;
                  long total = 10L;
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void AVarLocalDrawsTheWordAndTakesItsTypeFromTheInitializer()
    {
        // `var` is not a type, it is the decision not to write one, so three things have to agree: the notation draws
        // the word instead of the type cell, the generator writes the word, and the type rule reads the slot that does
        // hold something. The second local is the regression guard - a type left behind by switching to `var` must be
        // kept in the model (switching back should show what the author wrote) and must not also be drawn, which is
        // the mistake an either/or conditional made here first.
        var model = new Project(CSharpLanguage.Concepts).NewModel("variants");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var ratio = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);
        var fraction = model.NewChild(ratio, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.DoubleLiteral);
        var kept = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);
        var keptType = model.NewChild(kept, "type", CSharpConceptKeys.TypeReference);
        var count = model.NewChild(kept, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.IntLiteral);
        var empty = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Numbers");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "Show");
        returnType.Set(CSharpLanguage.NameSlot, "void");
        ratio.Set(CSharpLanguage.IsVarSlot, true);
        ratio.Set(CSharpLanguage.NameSlot, "ratio");
        fraction.Set(CSharpLanguage.ValueProperty, "1.5");
        kept.Set(CSharpLanguage.IsVarSlot, true);
        kept.Set(CSharpLanguage.NameSlot, "kept");
        keptType.Set(CSharpLanguage.NameSlot, "int");
        count.Set(CSharpLanguage.ValueProperty, "2");
        empty.Set(CSharpLanguage.IsVarSlot, true);
        empty.Set(CSharpLanguage.NameSlot, "nothing");

        const string Expected =
            """
            namespace Sample
            {
              public class Numbers
              {
                public void Show()
                {
                  var ratio = 1.5;
                  var kept = 2;
                  var nothing;
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);

        // The initializer decides, and with no initializer there is nothing to decide from - unknown rather than a
        // guess, which is the same answer this language gives wherever it cannot know.
        Assert.Equal("float", CSharpTypes.Registry.TypeOf(ratio).Name);
        Assert.Equal(UnknownType.Instance.Name, CSharpTypes.Registry.TypeOf(empty).Name);
    }

    [Fact]
    public void ThisLetsAConstructorStoreAParameterIntoTheFieldOfTheSameName()
    {
        // The reason `this` has to exist rather than being a nicety: C# resolves the bare name `message` to the
        // parameter, so `message = message` assigns the parameter to itself. `this.message = message` is the only
        // spelling that reaches the field, and without this node the language could not write the most ordinary
        // constructor body there is - even though constructors, parameters and fields were all already here.
        var model = new Project(CSharpLanguage.Concepts).NewModel("ctor");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        var fieldType = model.NewChild(field, "type", CSharpConceptKeys.TypeReference);
        var ctor = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Constructor);
        var parameter = model.NewChild(ctor, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
        var parameterType = model.NewChild(parameter, "type", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(ctor, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var assign = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Assignment);
        var target = model.NewChild(assign, "target", CSharpConceptKeys.MemberAccess);
        var self = model.NewChild(target, "target", CSharpConceptKeys.This);
        var value = model.NewChild(assign, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Name);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Greeter");
        field.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Private);
        field.Set(CSharpLanguage.NameSlot, "message");
        fieldType.Set(CSharpLanguage.NameSlot, "string");
        ctor.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        ctor.Set(CSharpLanguage.NameSlot, "Greeter");
        parameter.Set(CSharpLanguage.NameSlot, "message");
        parameterType.Set(CSharpLanguage.NameSlot, "string");
        target.Set(CSharpLanguage.MemberSlot, "message");
        value.SetReference(CSharpConceptKeys.TargetSlot, parameter);

        const string Expected =
            """
            namespace Sample
            {
              public class Greeter
              {
                private string message;

                public Greeter(string message)
                {
                  this.message = message;
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);

        // `this` claims no type, because it is the enclosing class and a class is not a type this language describes.
        Assert.Equal(UnknownType.Instance.Name, CSharpTypes.Registry.TypeOf(self).Name);
    }

    [Fact]
    public void ABehaviourClassHoldsTheMethodsOfOneConcept()
    {
        // The behaviour aspect in the only form this framework can honour: the model declares the methods that belong
        // to one concept and the generator emits them into a partial class, whose other half the host writes by hand.
        // That partial is the escape hatch for what a notation cannot express - an attribute, an unsafe block - which
        // is why "attributes are metadata, not logic" does not mean "attributes have no home".
        //
        // It is a type in a namespace rather than a document of its own: a partial class merges with the hand-written
        // half by name *and* namespace, and the language keeps its one root - the document.
        //
        // The concept key is drawn and generated as a comment, because the hand-written half says nothing about the
        // model and the generated half is where "which concept is this for" has to survive.
        var model = new Project(CSharpLanguage.Concepts).NewModel("behaviour");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var behaviour = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.ClassConcept);
        var method = model.NewChild(behaviour, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var @return = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
        var value = model.NewChild(@return, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.BoolLiteral);

        ns.Set(CSharpLanguage.NameSlot, "Rules.Runtime");
        behaviour.Set(CSharpConceptKeys.ConceptKeySlot, "Rules.Rule");
        behaviour.Set(CSharpLanguage.NameSlot, "RuleBehaviour");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "IsSatisfied");
        returnType.Set(CSharpLanguage.NameSlot, "bool");
        value.Set(CSharpLanguage.ValueProperty, "true");

        const string Expected =
            """
            namespace Rules.Runtime
            {
              // behaviour Rules.Rule
              partial class RuleBehaviour
              {
                public bool IsSatisfied()
                {
                  return true;
                }
              }
            }
            """;

        Assert.Equal(Expected, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Expected + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void AFreshBehaviourDrawsItsPlaceholdersAndNoTrailingSpace()
    {
        // A new behaviour has no concept and no name yet, and both slots carry a placeholder rather than starting empty.
        // That is the framework's own answer to "draw the gap only while there is something to put in it": there is no
        // conditional for "this property is filled" - the conditional compares a value, and WhenFilled is a child slot's
        // cell, not a property's - so the value is never empty and the space before it is always earned. An empty
        // property would have generated trailing whitespace inside a comment, the defect the empty member block fix
        // removed.
        var model = new Project(CSharpLanguage.Concepts).NewModel("unattached");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);

        _ = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.ClassConcept);

        ns.Set(CSharpLanguage.NameSlot, "Sample");

        const string Drawn =
            """
            namespace Sample
            {
              // behaviour Concept.Key
              partial class ConceptBehaviour
              {
                <no methods>
              }
            }
            """;

        Assert.Equal(Drawn, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));

        // The placeholder is drawn but is not code to be generated: the class keeps its braces and emits no method.
        const string Generated =
            """
            namespace Sample
            {
              // behaviour Concept.Key
              partial class ConceptBehaviour
              {
              }
            }

            """;

        Assert.Equal(Generated, new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void TheChecksReportTheOneDanglingNameTheSampleHolds()
    {
        // The sample is well-formed C# but for one thing on purpose: `Console` is a name reference nobody declares,
        // which is how a base language sees the standard library. The catalogue reports it and nothing else, which is
        // the half of this test that matters - a check set that fires on well-formed code is noise an author learns to
        // ignore, and this language had no check set at all before.
        var fix = Assert.Single(CSharpChecks.All.Find(Sample()));

        Assert.Contains(
            fix.Diagnostic.Code,
            (string?[])[UnresolvedReferenceCheck.Code, UnresolvedReferenceCheck.ClearCode]);
        Assert.Contains("Console", fix.Diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheChecksReportTwoMembersOfOneClassWithOneName()
    {
        // Not a style judgement: C# refuses the second one. Nothing in the language said so before, and this rule is
        // taken from the name group on its own because it is the one member of that group C# does not state itself.
        var model = new Project(CSharpLanguage.Concepts).NewModel("duplicate");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.NameSlot, "Greeter");

        for (var index = 0; index < 2; index++)
        {
            var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
            method.Set(CSharpLanguage.NameSlot, "Greet");
        }

        Assert.Contains(
            CSharpChecks.All.Find(model),
            fix => fix.Diagnostic.Code == DuplicateNameCheck.Code);
    }

    [Fact]
    public void TheChecksReportAnOperandThatIsNotThere()
    {
        // A binary with no right operand draws and generates `1 +`, which no compiler accepts. The structural group is
        // what says so, and it needs nothing from the language but the declaration of a slot that must hold a child.
        var model = new Project(CSharpLanguage.Concepts).NewModel("empty");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var statement = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var sum = model.NewChild(statement, "expression", CSharpConceptKeys.Binary);
        var left = model.NewChild(sum, "left", CSharpConceptKeys.IntLiteral);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        cls.Set(CSharpLanguage.NameSlot, "Greeter");
        method.Set(CSharpLanguage.NameSlot, "Count");
        sum.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.Add);
        left.Set(CSharpLanguage.ValueProperty, "1");

        Assert.Contains(
            CSharpChecks.All.Find(model),
            fix => fix.Diagnostic.Code == EmptyChildSlotCheck.Code && fix.Diagnostic.Target.Equals(sum.Id));
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

        // The argument means the field, so it is a reference that resolves - which is what a scope provider answers.
        // `Console` means nothing this file declares, so it is a dangling reference that keeps the text it was given:
        // one kind of node, two states, exactly as a reference cell draws them.
        argument.SetReference(CSharpConceptKeys.TargetSlot, field);
        console.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "Console");

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
