using Projectional.Core;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The rule that closes the scope model: a reference whose name no longer identifies the element it points
/// at, because something else took the same name.
/// </summary>
public sealed class AmbiguousReferenceConstraintTests
{
    [Fact]
    public void ANameThatIdentifiesOneElementSaysNothing()
    {
        var model = TestLanguage.NewModel();
        var target = Named(model, "Approved");
        Named(model, "Rejected");
        var user = Referring(model, "User", target);

        Assert.Empty(Check(model));
    }

    [Fact]
    public void AReferenceWhoseNameBecameAmbiguousIsReportedOnTheReferenceSlot()
    {
        var model = TestLanguage.NewModel();
        var target = Named(model, "Approved");

        // A second element takes the same name, so "Approved" no longer identifies one of them.
        Named(model, "Approved");
        var user = Referring(model, "User", target);

        var finding = Assert.Single(Check(model));

        Assert.Equal(user.Id, finding.Target);
        Assert.Equal("inherits", finding.Role);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
        Assert.Equal(AmbiguousReferenceConstraint.Code, finding.Code);
        Assert.Contains("\"Approved\"", finding.Message, StringComparison.Ordinal);
        Assert.Contains("2 elements", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoReferencesToTheAmbiguousNameAreTwoFindings()
    {
        var model = TestLanguage.NewModel();
        var target = Named(model, "Approved");
        Named(model, "Approved");

        Referring(model, "First", target);
        Referring(model, "Second", target);

        Assert.Equal(2, Check(model).Count);
    }

    [Fact]
    public void AnUnsetReferenceSaysNothing()
    {
        // There is no name to resolve, and nothing was ever claimed.
        var model = TestLanguage.NewModel();
        Named(model, "Approved");
        Named(model, "Approved");

        Assert.Empty(Check(model));
    }

    [Fact]
    public void AnUnresolvedReferenceSaysNothingAboutAmbiguity()
    {
        // It is already visibly wrong in the drawing, and the text it fell back to may be stale - so the
        // ambiguity of that text would blame the wrong problem.
        var model = TestLanguage.NewModel();
        var target = Named(model, "Approved");
        var user = Referring(model, "User", target);

        Named(model, "Approved");
        model.DeleteSubtree(target);

        Assert.Empty(Check(model));
    }

    [Fact]
    public void TheTargetsCurrentNameDecidesNotTheNameItWasWrittenWith()
    {
        // resolveInfo is not rewritten when a target is renamed, so resolving it would report names no user
        // ever sees: the two "Old" elements are a collision about a name this reference no longer carries.
        var model = TestLanguage.NewModel();
        var target = Named(model, "Old");
        var user = Referring(model, "User", target);

        target.Set("name", "Renamed");
        Named(model, "Old");
        Named(model, "Old");

        Assert.Empty(Check(model));
    }

    [Fact]
    public void TheLanguagesOwnScopeProviderDecides()
    {
        // The finding has to be about the scope the editor uses, so the rule asks the context and the engine
        // carries whatever provider the language replaced the chain with.
        var model = TestLanguage.NewModel();
        var target = Named(model, "Approved");
        Named(model, "Approved");
        Referring(model, "User", target);

        var findings = new ConstraintEngine(ScopeConstraints.All, scopes: new BlindScopes()).Check(model);

        Assert.Empty(findings);
    }

    /// <summary>Every rule the framework ships for scopes, run with the default chain.</summary>
    private static IReadOnlyList<Diagnostic> Check(Model model) =>
        new ConstraintEngine(ScopeConstraints.All).Check(model);

    private static Node Named(Model model, string name)
    {
        var rule = model.NewNode(TestLanguage.Rule);

        rule.Set("name", name);

        return rule;
    }

    private static Node Referring(Model model, string name, Node target)
    {
        var rule = Named(model, name);

        rule.SetReference("inherits", target);

        return rule;
    }

    /// <summary>A language that made its references see nothing at all.</summary>
    private sealed class BlindScopes : IScopeProvider
    {
        public IScope ScopeOf(Node node, string referenceSlot) => Scope.Empty;
    }
}
