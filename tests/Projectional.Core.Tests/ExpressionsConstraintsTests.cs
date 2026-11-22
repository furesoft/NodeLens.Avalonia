using Projectional.Core;
using Projectional.Expressions;
using Projectional.Expressions.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The rules the expression vocabulary states about names only a run would otherwise resolve.</summary>
/// <remarks>
/// They exist because the interpreter is late: it reports a duplicated function, an unknown callee and a wrong argument
/// count only when someone runs the document, and by then the author has written the rest of it. Both layers say the
/// same thing on purpose - the check is the editor's answer, the interpreter's is the answer for a model nobody opened.
/// </remarks>
public sealed class ExpressionsConstraintsTests
{
    [Fact]
    public void TwoFunctionsOfOneNameAreReportedOnTheLaterOne()
    {
        var model = NewModel();
        Function(model, "square", "x");
        var second = Function(model, "square", "y");

        var finding = Assert.Single(Findings(model));

        // One finding for the pair, on the later definition, because the first is the one a call resolves to and the one
        // the author meant to keep.
        Assert.Equal(ExpressionsConstraints.DuplicateFunctionCode, finding.Code);
        Assert.Equal(second.Id, finding.Target);
    }

    [Fact]
    public void OneFunctionDeclaringAParameterTwiceIsReported()
    {
        var model = NewModel();
        Function(model, "f", "x", "x");

        var finding = Assert.Single(Findings(model));

        Assert.Equal(ExpressionsConstraints.DuplicateParameterCode, finding.Code);
    }

    [Fact]
    public void ACallToNothingIsReported()
    {
        var model = NewModel();
        Call(model, "cube", 2);

        var finding = Assert.Single(Findings(model));

        Assert.Equal(ExpressionsConstraints.UnknownFunctionCode, finding.Code);
    }

    [Fact]
    public void ACallWithTheWrongArgumentCountIsReported()
    {
        var model = NewModel();
        Function(model, "square", "x");
        Call(model, "square", 2, 3);

        // The function itself is clean, so the one finding is the arity.
        var finding = Assert.Single(Findings(model));

        Assert.Equal(ExpressionsConstraints.ArityCode, finding.Code);
    }

    [Fact]
    public void ACallThatMatchesItsFunctionIsLeftAlone()
    {
        var model = NewModel();
        Function(model, "square", "x");
        Call(model, "square", 4);

        Assert.Empty(Findings(model));
    }

    private static Model NewModel() => ExpressionsLanguage.NewProject().NewModel("expressions");

    private static IReadOnlyList<Diagnostic> Findings(Model model) =>
        [.. new ConstraintEngine(ExpressionsLanguage.Constraints).Check(model)];

    private static Node Function(Model model, string name, params string[] parameters)
    {
        var function = model.NewNode(ExpressionsConceptKeys.Function);
        function.Set("name", name);

        foreach (var each in parameters)
        {
            model.NewChild(function, ExpressionsLanguage.ParametersSlot, ExpressionsConceptKeys.Parameter).Set("name", each);
        }

        model.NewChild(function, "body", ExpressionsConceptKeys.IntLiteral).Set("value", 1);
        model.AddRoot(function);

        return function;
    }

    private static Node Call(Model model, string callee, params int[] arguments)
    {
        var call = model.NewNode(ExpressionsConceptKeys.Call);
        call.Set("callee", callee);

        foreach (var each in arguments)
        {
            model.NewChild(call, ExpressionsLanguage.ArgumentsSlot, ExpressionsConceptKeys.IntLiteral).Set("value", each);
        }

        model.AddRoot(call);

        return call;
    }
}
