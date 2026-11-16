using Projectional.Checks;
using Projectional.Core;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The same language written twice - once in C# and once as a model - has to be the same language.
/// </summary>
/// <remarks>
/// These are the three tests of the parity rule ([ADR 0014](../../../docs/adr/0014-both-authoring-paths-are-permanent.md)),
/// run over <see cref="TestLanguage"/> and its declared twin. They exist so the rule is executable: without them
/// the declared path would degrade into "whatever it does is correct", which is how a second implementation
/// drifts.
/// </remarks>
public sealed class LanguageParityTests
{
    [Fact]
    public void BothDefinitionsDeclareTheSameLanguageInTheSameOrder()
    {
        // Order is part of the contract - declaration order decides what an empty slot is filled with and which
        // completion entry comes first - so this compares sequences, not sets.
        Assert.Equal(
            ParityHarness.Dump(TestLanguage.Registry),
            ParityHarness.Dump(DeclaredTestLanguage.Concepts));
    }

    [Fact]
    public void BothNotationsDrawTheSameDocumentTheSameWay()
    {
        var document = Sample(TestLanguage.NewModel());

        Assert.Equal(
            ParityHarness.Render(document, TestNotations.Registry),
            ParityHarness.Render(document, DeclaredTestLanguage.Notations));
    }

    [Fact]
    public void BothLanguagesProduceTheSameFindings()
    {
        // The document is deliberately incomplete, so a check has something to find and the comparison is about
        // the finding rather than about two empty lists.
        var written = Sample(TestLanguage.NewModel());
        var declared = Sample(DeclaredTestLanguage.NewProject().NewModel("Test"));
        var checks = new CheckSet([new EmptyChildSlotCheck()]);

        var expected = ParityHarness.Findings(checks, written.Model);

        Assert.NotEmpty(expected);
        Assert.Equal(expected, ParityHarness.Findings(checks, declared.Model));
    }

    /// <summary>A small document of the language, with one body left empty so a check has something to report.</summary>
    private static Node Sample(Model model)
    {
        var rule = model.NewNode(TestLanguage.Rule);

        model.AddRoot(rule);

        rule.Set("name", "Approved");

        var tag = model.NewNode(TestLanguage.Tag);

        tag.Set("label", "green");
        rule.SetReference("tag", tag);

        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);

        step.Set("index", "1");
        step.Set("text", "check the amount");

        var note = model.NewChild(body, "notes", TestLanguage.Note);

        note.Set("text", "agreed with finance");

        return rule;
    }
}
