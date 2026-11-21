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
        // closing brace that only the block form has is gone.
        Assert.StartsWith("namespace Sample;\npublic class Greeter\n{\n  private string message", scoped, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Sample\n{", scoped, StringComparison.Ordinal);
        Assert.EndsWith("  }\n}", scoped, StringComparison.Ordinal);

        // And the generator says the same thing as the notation, which is the property the whole language rests on.
        Assert.StartsWith("namespace Sample;\npublic class Greeter\n{\n  private string message", generated, StringComparison.Ordinal);
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
