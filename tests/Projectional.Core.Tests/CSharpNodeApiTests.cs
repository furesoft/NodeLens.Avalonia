using System.Reflection;

using Projectional.Core;
using Projectional.CSharp;
using Projectional.CSharp.Generation;
using Projectional.CSharp.Notation;
using Projectional.CSharp.Types;
using Projectional.Language;
using Projectional.LanguageExport;
using Projectional.Projection;
using Projectional.Semantics;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The node API: what a node is read as, and how what is inside one is read.</summary>
/// <remarks>
/// <para>
/// A behaviour is C#, and C# has no idea what a node is - so the base language has to offer the two things a behaviour
/// needs and a general-purpose language cannot state: a type that says which concept a node is read as, and expressions
/// that read a node's parts. The spelling is MPS's, because that is where the idea comes from: the concept goes in
/// angle brackets, a thing the node has of its own is a dotted word, and a part addressed by the name its concept gave
/// it uses a slash on either side.
/// </para>
/// <para>
/// The two halves are checked the way every other C# concept is: drawn and generated, then compiled and run. The
/// drawing and the generator deliberately disagree in one place - <c>node&lt;Atlas.Stop&gt;</c> against
/// <c>Projectional.Core.Node</c> - and that is the whole point of the concept being in brackets: which concept a node
/// is read as is a fact the model holds, not one a compiled assembly answers, so it stays in the editor.
/// </para>
/// </remarks>
public sealed class CSharpNodeApiTests
{
    /// <summary>The concept the sample behaviour belongs to.</summary>
    private const string BehaviourOf = "Atlas.Stop";

    [Fact]
    public void TheNodeApiDrawsItsMpsSpellingAndGeneratesTheFrameworksCalls()
    {
        // The drawing is the vocabulary the user writes; the text is the vocabulary that compiles. One model, both
        // halves, so the two cannot drift apart: every read the notation offers has a call behind it.
        var model = Sample();

        Assert.Equal(Drawn, LayoutSnapshot.Render(model, CSharpNotations.CreateNotations(CSharpLanguage.Concepts), width: 120));
        Assert.Equal(Generated + "\n", new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);
    }

    [Fact]
    public void ACompiledBehaviourReadsTheNodeItIsGiven()
    {
        // The claim the vocabulary makes: a behaviour the model declares is C# that runs on a real node. Compiling it and
        // handing it nodes is the only way to ask whether the calls the generator emits mean what the words in the editor
        // say - comparing the text against a string would pin the spelling and prove nothing.
        var model = Sample();

        var assembly = AssemblyCompiler.Compile(
            "NodeApiSample",
            new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text);

        var behaviour = Assembly.Load(assembly.Bytes).GetType("Sample.StopBehaviour")
            ?? throw new InvalidOperationException("The compiled assembly does not hold Sample.StopBehaviour.");

        // The behaviour node itself is the node under test: it has a name, a member list, a position and a concept, so
        // every read that the node offers something for can be asked the same node.
        var subject = model.Roots[0].GetChildren(CSharpConceptKeys.NamespaceSlot)[0]
            .GetChildren(CSharpConceptKeys.ClassesSlot)[0];

        Assert.Equal("StopBehaviour", subject.Name);
        Assert.Equal("CSharp.ClassConcept", Call(behaviour, "Concept", subject));
        Assert.Equal("stop.0", Call(behaviour, "Position", subject));
        Assert.Equal(true, Call(behaviour, "Named", subject));
        Assert.Equal(true, Call(behaviour, "HasParent", subject));

        // The one node with no parent is a root, which is the file - so the same read answers the other way there.
        Assert.Equal(false, Call(behaviour, "HasParent", model.Roots[0]));

        // Nine methods, one per read: the member list the model declared is what the children read counts.
        Assert.Equal(9, Call(behaviour, "Members", subject));

        // A node that has the slots the remaining three reads address, one concept each: a parenthesis has an `inner`
        // child, and a name has a `target` reference that was written with the text it fell back to. Both are built
        // where an expression can stand, because only a document can start a model and neither of these is one.
        var scratch = new Project(CSharpLanguage.Concepts).NewModel("scratch");
        var scratchFile = scratch.NewNode(CSharpConceptKeys.File);
        var scratchSpace = scratch.NewChild(scratchFile, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var scratchClass = scratch.NewChild(scratchSpace, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);
        var scratchMethod = scratch.NewChild(scratchClass, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var scratchBody = scratch.NewChild(scratchMethod, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);

        var held = scratch.NewChild(scratchBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var parenthesis = scratch.NewChild(held, "expression", CSharpConceptKeys.Parenthesized);
        scratch.NewChild(parenthesis, "inner", CSharpConceptKeys.IntLiteral).Set(CSharpLanguage.ValueProperty, "1");

        var written = scratch.NewChild(scratchBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var unresolved = scratch.NewChild(written, "expression", CSharpConceptKeys.Name);
        unresolved.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "value");

        Assert.Equal(true, Call(behaviour, "HasChild", parenthesis));
        Assert.Equal("1", parenthesis.GetChild("inner")!.Get(CSharpLanguage.ValueProperty));

        Assert.Equal(false, Call(behaviour, "Resolves", unresolved));
        Assert.Equal("value", Call(behaviour, "Fallback", unresolved));
    }

    [Fact]
    public void AReadOfANodeHasTheTypeTheFrameworkAnswersWith()
    {
        // A name, a concept key and a property's text are text, and a position is a whole number - so a behaviour that
        // compares `node.index` to a string is a finding rather than a silent `+`. What is a node stays unclaimed: the
        // language has no type for "a node of some concept", and `node<Atlas.Stop>` is drawn rather than evaluated.
        var model = Sample();

        var reads = model.Roots[0]
            .SelfAndDescendants()
            .Where(node => node.Concept.IsKindOf(CSharpConceptKeys.NodeRead))
            .ToLookup(node => node.Concept.Key, node => CSharpTypes.Registry.TypeOf(node).Name);

        Assert.Equal(["string"], reads[CSharpConceptKeys.NodeName].Distinct());
        Assert.Equal(["string"], reads[CSharpConceptKeys.NodeConcept].Distinct());
        Assert.Equal(["string"], reads[CSharpConceptKeys.NodeProperty].Distinct());
        Assert.Equal(["string"], reads[CSharpConceptKeys.NodeResolveInfo].Distinct());
        Assert.Equal(["int"], reads[CSharpConceptKeys.NodeIndex].Distinct());
        Assert.All(reads[CSharpConceptKeys.NodeParent], name => Assert.Equal("unknown", name));
    }

    /// <summary>Calls one method of the compiled behaviour with one node.</summary>
    private static object? Call(Type behaviour, string name, Node node)
    {
        var method = behaviour.GetMethod(name) ?? throw new InvalidOperationException($"{name} was not generated.");

        return method.Invoke(null, [node]);
    }

    /// <summary>
    /// The sample: one behaviour with a method per read, so the drawing and the generated text cover the whole
    /// vocabulary in one model.
    /// </summary>
    private static Model Sample()
    {
        var model = new Project(CSharpLanguage.Concepts).NewModel("node-api");
        var file = model.NewNode(CSharpConceptKeys.File);
        var space = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var behaviour = model.NewChild(space, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.ClassConcept);

        space.Set(CSharpLanguage.NameSlot, "Sample");
        behaviour.Set(CSharpConceptKeys.ConceptKeySlot, BehaviourOf);
        behaviour.Set(CSharpLanguage.NameSlot, "StopBehaviour");

        // return "stop." + node.index;
        var position = Method(model, behaviour, "string", "Position", out var subject);

        var sum = model.NewChild(position, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Binary);
        sum.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.Add);
        model.NewChild(sum, "left", CSharpConceptKeys.StringLiteral).Set(CSharpLanguage.ValueProperty, "stop.");
        Read(model, sum, "right", CSharpConceptKeys.NodeIndex, subject);

        // return node.name != null;
        var named = Method(model, behaviour, "bool", "Named", out subject);
        var hasName = model.NewChild(named, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Binary);
        hasName.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.NotEqual);
        Read(model, hasName, "left", CSharpConceptKeys.NodeName, subject);
        model.NewChild(hasName, "right", CSharpConceptKeys.NullLiteral);

        // return node.concept;
        Read(model, Method(model, behaviour, "string", "Concept", out subject), CSharpConceptKeys.ValueSlot,
            CSharpConceptKeys.NodeConcept, subject);

        // return node/property/name;  - nullable, because a node the concept gave no name slot to has none.
        Read(model, Method(model, behaviour, "string?", "Written", out subject), CSharpConceptKeys.ValueSlot,
            CSharpConceptKeys.NodeProperty, subject).Set(CSharpLanguage.SlotNameSlot, "name");

        // return node/children/members.Count;
        var members = Method(model, behaviour, "int", "Members", out subject);
        var count = model.NewChild(members, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.MemberAccess);
        count.Set(CSharpLanguage.MemberSlot, "Count");
        Read(model, count, "target", CSharpConceptKeys.NodeChildren, subject).Set(CSharpLanguage.SlotNameSlot, "members");

        // return node.parent != null;
        var hasParent = Method(model, behaviour, "bool", "HasParent", out subject);
        var parentTest = model.NewChild(hasParent, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Binary);
        parentTest.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.NotEqual);
        Read(model, parentTest, "left", CSharpConceptKeys.NodeParent, subject);
        model.NewChild(parentTest, "right", CSharpConceptKeys.NullLiteral);

        // return node/child/inner != null;
        var hasChild = Method(model, behaviour, "bool", "HasChild", out subject);
        var childTest = model.NewChild(hasChild, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Binary);
        childTest.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.NotEqual);
        Read(model, childTest, "left", CSharpConceptKeys.NodeChild, subject).Set(CSharpLanguage.SlotNameSlot, "inner");
        model.NewChild(childTest, "right", CSharpConceptKeys.NullLiteral);

        // return node/reference/target != null;
        var resolves = Method(model, behaviour, "bool", "Resolves", out subject);
        var resolveTest = model.NewChild(resolves, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Binary);
        resolveTest.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.NotEqual);
        Read(model, resolveTest, "left", CSharpConceptKeys.NodeReference, subject).Set(CSharpLanguage.SlotNameSlot, "target");
        model.NewChild(resolveTest, "right", CSharpConceptKeys.NullLiteral);

        // return node/resolveInfo/target;
        Read(model, Method(model, behaviour, "string?", "Fallback", out subject), CSharpConceptKeys.ValueSlot,
            CSharpConceptKeys.NodeResolveInfo, subject).Set(CSharpLanguage.SlotNameSlot, "target");

        model.AddRoot(file);

        return model;
    }

    /// <summary>A public static method of the behaviour, its one parameter a node, and where its return value goes.</summary>
    private static Node Method(Model model, Node behaviour, string returnType, string name, out Node parameter)
    {
        var method = model.NewChild(behaviour, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var type = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);

        parameter = model.NewChild(method, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);

        var nodeType = model.NewChild(parameter, "type", CSharpConceptKeys.NodeType);
        nodeType.Set(CSharpLanguage.NameSlot, BehaviourOf);
        parameter.Set(CSharpLanguage.NameSlot, "node");

        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);

        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        // Static because the node is a parameter: a behaviour that held its node would need a field and a constructor the
        // model cannot draw, and the model is the only source of truth - a member nobody can see must not be generated.
        method.Set(CSharpLanguage.IsStaticSlot, true);
        method.Set(CSharpLanguage.NameSlot, name);
        type.Set(CSharpLanguage.NameSlot, returnType);

        return model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
    }

    /// <summary>A read of the node the method was given, in <paramref name="slot"/> of <paramref name="owner"/>.</summary>
    private static Node Read(Model model, Node owner, string slot, string concept, Node parameter)
    {
        var read = model.NewChild(owner, slot, concept);
        var node = model.NewChild(read, CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Name);

        node.SetReference(CSharpConceptKeys.TargetSlot, parameter);

        return read;
    }

    private const string Drawn =
        """
        namespace Sample
        {
          // behaviour Atlas.Stop
          partial class StopBehaviour
          {
            public static string Position(node<Atlas.Stop> node)
            {
              return "stop." + node.index;
            }

            public static bool Named(node<Atlas.Stop> node)
            {
              return node.name != null;
            }

            public static string Concept(node<Atlas.Stop> node)
            {
              return node.concept;
            }

            public static string? Written(node<Atlas.Stop> node)
            {
              return node/property/name;
            }

            public static int Members(node<Atlas.Stop> node)
            {
              return node/children/members.Count;
            }

            public static bool HasParent(node<Atlas.Stop> node)
            {
              return node.parent != null;
            }

            public static bool HasChild(node<Atlas.Stop> node)
            {
              return node/child/inner != null;
            }

            public static bool Resolves(node<Atlas.Stop> node)
            {
              return node/reference/target != null;
            }

            public static string? Fallback(node<Atlas.Stop> node)
            {
              return node/resolveInfo/target;
            }
          }
        }
        """;

    private const string Generated =
        """
        namespace Sample
        {
          // behaviour Atlas.Stop
          partial class StopBehaviour
          {
            public static string Position(Projectional.Core.Node node)
            {
              return "stop." + node.IndexInParent;
            }

            public static bool Named(Projectional.Core.Node node)
            {
              return node.Name != null;
            }

            public static string Concept(Projectional.Core.Node node)
            {
              return node.Concept.Key;
            }

            public static string? Written(Projectional.Core.Node node)
            {
              return node.GetOrNull("name");
            }

            public static int Members(Projectional.Core.Node node)
            {
              return node.GetChildren("members").Count;
            }

            public static bool HasParent(Projectional.Core.Node node)
            {
              return node.Parent != null;
            }

            public static bool HasChild(Projectional.Core.Node node)
            {
              return node.GetChild("inner") != null;
            }

            public static bool Resolves(Projectional.Core.Node node)
            {
              return node.GetReferenceTarget("target") != null;
            }

            public static string? Fallback(Projectional.Core.Node node)
            {
              return node.GetReference("target")?.ResolveInfo;
            }
          }
        }
        """;
}
