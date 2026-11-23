using Projectional.Avalonia;
using Projectional.Core;
using Projectional.CSharp;
using Projectional.CSharp.Generation;
using Projectional.CSharp.Notation;
using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>What the shell's output panel shows for a generated document.</summary>
/// <remarks>
/// The panel is the consumer of the generator's traceability map: one row per generated line, and the row carries the
/// address of whatever wrote the beginning of that line, which is what makes clicking it move the caret back to the
/// model. The map and its lookup are <c>GeneratorTests</c>' business; these tests are about the answer reaching a
/// caller, because an unused map is not a feature.
/// </remarks>
public sealed class DocumentGenerateTests
{
    [Fact]
    public void EveryGeneratedLinePointsAtTheNodeThatWroteIt()
    {
        // A behaviour class is two levels deep - file, namespace, class - so this is also the test that the lookup
        // answers with the *innermost* node rather than the outermost: the generator appends a node's own text after
        // its children's, so the regions come out deepest-first and the first match is the closest writer.
        var model = new Project(CSharpLanguage.Concepts).NewModel("generated");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var behaviour = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.ClassConcept);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        behaviour.Set(CSharpConceptKeys.ConceptKeySlot, "Rules.Rule");
        behaviour.Set(CSharpLanguage.NameSlot, "RuleBehaviour");

        var report = DocumentGenerate.Report(
            DocumentGenerate.Generate(file, CSharpTextGenerators.Generator),
            CSharpTextGenerators.Generator);

        Assert.True(report.Succeeded, report.Headline);

        // The line numbers are the generated file's, not the model's: row 2 is the class and row 1 is its comment,
        // and both were written by the behaviour node.
        Assert.Equal("  // behaviour Rules.Rule", report.Rows[2].Text);
        Assert.Equal("  partial class RuleBehaviour", report.Rows[3].Text);
        Assert.Equal(behaviour.Id, report.Rows[2].Place!.Value.Owner);
        Assert.Equal(behaviour.Id, report.Rows[3].Place!.Value.Owner);

        // The namespace's own word is the namespace's, so the answer is not "always the class".
        Assert.Equal("namespace Sample", report.Rows[0].Text);
        Assert.Equal(ns.Id, report.Rows[0].Place!.Value.Owner);
    }

    [Fact]
    public void AGeneratedLineNobodyWroteHasNoAddress()
    {
        // The text ends with a newline, so the report has one trailing row with nothing in it, and the offset past the
        // last character belongs to no region. A row with no address is the honest answer: the panel shows the line and
        // has nothing to jump to, rather than jumping to a guess.
        var model = new Project(CSharpLanguage.Concepts).NewModel("generated");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);

        ns.Set(CSharpLanguage.NameSlot, "Sample");

        var report = DocumentGenerate.Report(
            DocumentGenerate.Generate(file, CSharpTextGenerators.Generator),
            CSharpTextGenerators.Generator);

        var last = report.Rows[^1];

        Assert.Equal(string.Empty, last.Text);
        Assert.Null(last.Place);
    }
}
