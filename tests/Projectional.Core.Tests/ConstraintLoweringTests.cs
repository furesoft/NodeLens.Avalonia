using Projectional.Checks;
using Projectional.Core;
using Projectional.ConstraintRules;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>What a written rule lowers to, and what the lowering refuses.</summary>
/// <remarks>
/// The parity test is the one that matters. ADR 0014 says the declared form expresses nothing the code form cannot,
/// so a rule written as a model must produce the findings the hand-written catalogue produces on the same
/// document - compared by place in the document, because a finding promises <em>where</em> and two documents built
/// the same way have different node identities.
/// </remarks>
public sealed class ConstraintLoweringTests
{
    [Fact]
    public void EveryBodyKindLowersToTheConstraintItStandsFor()
    {
        var set = Declare(
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "body", ConstraintConceptKeys.EmptySlot),
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "name", ConstraintConceptKeys.DefaultValue),
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "name", ConstraintConceptKeys.SurroundingWhitespace),
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "tag", ConstraintConceptKeys.EmptyReference));

        var forRule = set.For(TestLanguage.Registry.Get(TestLanguage.Rule));

        Assert.Equal(4, set.Count);
        Assert.Collection(
            forRule,
            constraint => Assert.IsType<EmptySlotConstraint>(constraint),
            constraint => Assert.IsType<DefaultValueConstraint>(constraint),
            constraint => Assert.IsType<SurroundingWhitespaceConstraint>(constraint),
            constraint => Assert.IsType<EmptyReferenceConstraint>(constraint));
    }

    [Fact]
    public void ADeclaredRuleReportsWhatTheCatalogueReportsOnTheSameDocument()
    {
        var model = TestLanguage.NewModel();

        model.NewNode(TestLanguage.Rule);

        var padded = model.NewNode(TestLanguage.Rule);

        padded.Set("name", " Padded ");

        var catalogue = new CheckSet([.. StandardChecks.Names, .. StandardChecks.Structure])
            .Find(model)
            .Select(fix => fix.Diagnostic);

        var declared = new ConstraintEngine(Declare(
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "name", ConstraintConceptKeys.DefaultValue, ConstraintSeverities.Hint, "give it a name"),
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "name", ConstraintConceptKeys.SurroundingWhitespace, ConstraintSeverities.Warning, "the name has whitespace around it"),
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "body", ConstraintConceptKeys.EmptySlot, ConstraintSeverities.Hint, "the rule has no body"),
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "extends", ConstraintConceptKeys.EmptySlot, ConstraintSeverities.Hint, "the rule extends nothing")))
            .Check(model);

        // Two rules, each with two empty single-child slots, and one of the two names in each state: six findings.
        Assert.NotEmpty(catalogue);
        Assert.Equal(Places(catalogue), Places(declared));

        // The sentence is the declaration's, which is the whole point of writing the rule down.
        Assert.Contains(declared, finding => string.Equals(finding.Message, "the rule has no body", StringComparison.Ordinal));
        Assert.Contains(declared, finding => string.Equals(finding.Role, "name", StringComparison.Ordinal)
            && finding.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ARuleAboutEveryNodeIsAskedOfEveryNode()
    {
        var set = Declare(Declare(ConstraintLanguage.NewModel(), conceptKey: null, "detail", ConstraintConceptKeys.EmptySlot));

        Assert.Single(set.EverywhereRules);

        // "For every node" is asked of every concept, which is why the engine finds it without a concept key.
        Assert.Single(set.For(TestLanguage.Registry.Get(TestLanguage.Rule)));

        // The rule names no concept, so it applies to whichever node declares the slot - here a step inside a rule.
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var steps = model.NewChild(body, "steps", TestLanguage.Step);

        var findings = new ConstraintEngine(set).Check(model);

        var finding = Assert.Single(findings);

        Assert.Equal(steps.Id, finding.Target);
        Assert.Equal("detail", finding.Role);
        Assert.Equal(DiagnosticSeverity.Error, finding.Severity);
    }

    [Fact]
    public void ACodedRuleReportsItsCodeAndAnUncodedOneReportsNone()
    {
        var document = TestLanguage.NewModel();

        document.NewNode(TestLanguage.Rule);

        var coded = new ConstraintEngine(Declare(
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "body", ConstraintConceptKeys.EmptySlot, code: "rules.body.required")))
            .Check(document);

        var uncoded = new ConstraintEngine(Declare(
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "body", ConstraintConceptKeys.EmptySlot)))
            .Check(document);

        Assert.Equal("rules.body.required", Assert.Single(coded).Code);
        Assert.Null(Assert.Single(uncoded).Code);
    }

    [Fact]
    public void ARuleAboutAConceptTheLanguageDoesNotHaveIsRefused()
    {
        var error = Assert.Throws<ConceptDefinitionException>(() => Declare(
            Declare(ConstraintLanguage.NewModel(), "Rules.Nothing", "body", ConstraintConceptKeys.EmptySlot)));

        Assert.Contains("Rules.Nothing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleWithoutAMessageIsRefused()
    {
        var model = ConstraintLanguage.NewModel();

        model.Roots[0].Set(ConstraintLanguage.SeveritySlot, ConstraintSeverities.Error);
        model.NewChild(model.Roots[0], ConstraintLanguage.BodySlot, ConstraintConceptKeys.EmptySlot)
            .Set(ConstraintLanguage.TargetSlot, "body");

        var error = Assert.Throws<ConceptDefinitionException>(() => Declare(model.Roots[0]));

        Assert.Contains("what is wrong", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleWithoutABodyIsRefused()
    {
        // A rule that does not say what it checks is a rule that can never fire, so it is a load error rather than a
        // rule that silently says nothing.
        var model = ConstraintLanguage.NewModel();

        model.Roots[0].Set(ConstraintLanguage.MessageSlot, "something is wrong");

        var error = Assert.Throws<ConceptDefinitionException>(() => Declare(model.Roots[0]));

        Assert.Contains("body", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnEmptySlotBodyOnAPropertyIsRefused()
    {
        var error = Assert.Throws<ConceptDefinitionException>(() => Declare(
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "name", ConstraintConceptKeys.EmptySlot)));

        Assert.Contains("single-child", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADefaultValueBodyOnASlotWithNoDeclaredDefaultIsRefused()
    {
        // The rule could never fire: a placeholder that was never declared is not a placeholder.
        var error = Assert.Throws<ConceptDefinitionException>(() => Declare(
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Tag, "label", ConstraintConceptKeys.DefaultValue)));

        Assert.Contains("declared default", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASurroundingWhitespaceBodyOnANonTextPropertyIsRefused()
    {
        var error = Assert.Throws<ConceptDefinitionException>(() => Declare(
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "enabled", ConstraintConceptKeys.SurroundingWhitespace)));

        Assert.Contains("text property", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyReferenceBodyOnAChildSlotIsRefused()
    {
        var error = Assert.Throws<ConceptDefinitionException>(() => Declare(
            Declare(ConstraintLanguage.NewModel(), TestLanguage.Rule, "body", ConstraintConceptKeys.EmptyReference)));

        Assert.Contains("not a reference", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Writes one rule into <paramref name="model"/> and answers it.</summary>
    private static Node Declare(
        Model model,
        string? conceptKey,
        string slot,
        string kind,
        string severity = ConstraintSeverities.Error,
        string message = "something is wrong",
        string? code = null)
    {
        var rule = model.Roots[0];

        if (conceptKey is not null)
        {
            rule.Set(ConstraintLanguage.ConceptKeySlot, conceptKey);
        }

        rule.Set(ConstraintLanguage.SeveritySlot, severity);
        rule.Set(ConstraintLanguage.MessageSlot, message);

        if (code is not null)
        {
            rule.Set(ConstraintLanguage.CodeSlot, code);
        }

        model.NewChild(rule, ConstraintLanguage.BodySlot, kind).Set(ConstraintLanguage.TargetSlot, slot);

        return rule;
    }

    private static ConstraintSet Declare(params Node[] rules) =>
        ConstraintLowering.CreateConstraints(rules, TestLanguage.Registry);

    /// <summary>Where the findings are, in the order a comparison can use.</summary>
    private static string[] Places(IEnumerable<Diagnostic> findings) =>
        [.. findings.Select(finding => $"{finding.Target}:{finding.Role}:{finding.Severity}").Order(StringComparer.Ordinal)];
}
