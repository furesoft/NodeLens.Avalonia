using System.Reflection;

using Projectional.Core;
using Projectional.CSharp;
using Projectional.CSharp.Generation;
using Projectional.CSharp.Notation;
using Projectional.Language;
using Projectional.LanguageExport;
using Projectional.Semantics;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The behaviour half the model generates, compiled against the half a host writes by hand.</summary>
/// <remarks>
/// <para>
/// The point of a behaviour class is that it is *two* halves: the methods the model declares, and whatever the host
/// needs that a notation cannot express. That contract is a C# contract - two <c>partial</c> declarations merge when
/// their names and namespaces agree - so the only honest way to test it is to compile both and look at the type that
/// comes out. Comparing the generated text against a string would pin the spelling without ever asking whether the
/// two halves are one class.
/// </para>
/// <para>
/// The compiler is the repository's own (<see cref="AssemblyCompiler"/>), which is the same one the language export
/// uses, and it reports warnings as errors: a generated behaviour that compiles only with a warning would fail the
/// build of whoever references it, so it fails here first.
/// </para>
/// </remarks>
public sealed class CSharpBehaviourCompilationTests
{
    [Fact]
    public void TheGeneratedHalfAndTheHandWrittenHalfAreOneClass()
    {
        var model = new Project(CSharpLanguage.Concepts).NewModel("behaviour");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var behaviour = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Behaviour);
        var method = model.NewChild(behaviour, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var @return = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
        var value = model.NewChild(@return, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.BoolLiteral);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        behaviour.Set(CSharpConceptKeys.ConceptKeySlot, "Rules.Rule");
        behaviour.Set(CSharpLanguage.NameSlot, "RuleBehaviour");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, "IsSatisfied");
        returnType.Set(CSharpLanguage.NameSlot, "bool");
        value.Set(CSharpLanguage.ValueProperty, "true");

        var generated = new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator).Text;

        // What a host writes beside the generated file: anything the model cannot say. Here one more method, which is
        // enough to prove the merge - a field would be the realistic example, but the compiler treats warnings as
        // errors and a generated method that cannot yet name the node would never read it.
        const string HandWritten =
            """
            namespace Sample
            {
              partial class RuleBehaviour
              {
                public string Describe()
                {
                  return "a rule";
                }
              }
            }
            """;

        var assembly = AssemblyCompiler.Compile("BehaviourSample", generated + HandWritten);

        var type = Assembly.Load(assembly.Bytes).GetType("Sample.RuleBehaviour")
            ?? throw new InvalidOperationException("The compiled assembly does not hold Sample.RuleBehaviour.");

        // Both halves are on the type: the one the model declared and the one the host wrote. Two files, one class.
        Assert.NotNull(type.GetMethod("IsSatisfied"));
        Assert.NotNull(type.GetMethod("Describe"));
    }
}
