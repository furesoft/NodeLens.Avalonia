using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The solver, against the equations the cookbook's rule shapes produce.</summary>
/// <remarks>
/// What makes this layer trustworthy is not that it solves - a substitution would - but what it refuses: it does
/// not invent the missing side of a relation between two unknowns, it reports a contradiction instead of picking a
/// winner, and it stops on a budget instead of hanging. Most of these tests are about those three.
/// </remarks>
public sealed class TypeSolverTests
{
    private static readonly PrimitiveType Int = PrimitiveType.Of(PropertyType.Integer);
    private static readonly PrimitiveType Text = PrimitiveType.Of(PropertyType.String);

    [Fact]
    public void AnEqualityGivesAVariableItsType()
    {
        var element = new TypeVariable("elementType");

        var solution = new TypeSolver().Solve([TypeEquation.Equal(element, Int)]);

        Assert.True(solution.Succeeded);
        Assert.True(solution.IsSolved(element));
        Assert.Same(Int, solution.Of(element));
    }

    [Fact]
    public void ASolvedVariableResolvesInsideAStructure()
    {
        // The cookbook's `var elementType` case: the rule builds a list of the variable, and the answer is a list
        // of the type the equations demanded.
        var element = new TypeVariable("elementType");

        var solution = new TypeSolver().Solve([TypeEquation.Equal(element, Int)]);

        Assert.Equal("[int]", solution.Resolve(new ListType(element)).Name);
        Assert.Equal("int?", solution.Resolve(new OptionalType(element)).Name);
    }

    [Fact]
    public void AnEqualityOfTwoListsEquatesTheirElements()
    {
        var element = new TypeVariable("elementType");

        var solution = new TypeSolver().Solve([TypeEquation.Equal(new ListType(element), new ListType(Int))]);

        Assert.True(solution.Succeeded);
        Assert.Same(Int, solution.Of(element));
    }

    [Fact]
    public void AContradictionIsReportedRatherThanResolved()
    {
        var element = new TypeVariable("elementType");

        var solution = new TypeSolver().Solve(
        [
            TypeEquation.Equal(element, Int),
            TypeEquation.Equal(element, Text),
        ]);

        Assert.False(solution.Succeeded);
        Assert.Contains("cannot be one type", solution.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AVariableNothingDeterminesStaysAVariable()
    {
        // The line between this layer and a guess: `?a == ?b` is a relation, not an answer, and the solution says
        // so by handing the variable back under its own name.
        var a = new TypeVariable("a");
        var b = new TypeVariable("b");

        var solution = new TypeSolver().Solve([TypeEquation.Equal(a, b)]);

        Assert.True(solution.Succeeded);
        Assert.False(solution.IsSolved(a));
        Assert.Equal("?a", solution.Of(a).Name);
    }

    [Fact]
    public void ABoundNarrowsWithoutSolving()
    {
        var variable = new TypeVariable("result");

        var solution = new TypeSolver().Solve([TypeEquation.Subtype(variable, Int)]);

        Assert.True(solution.Succeeded);
        Assert.False(solution.IsSolved(variable));
    }

    [Fact]
    public void ABoundThatTheOnlyCandidateViolatesIsAContradiction()
    {
        // `?result` may only stand where a string is required, and the only thing that could be its type is an int.
        var variable = new TypeVariable("result");

        var solution = new TypeSolver().Solve(
        [
            TypeEquation.Subtype(Int, variable),
            TypeEquation.Subtype(variable, Text),
        ]);

        Assert.False(solution.Succeeded);
        Assert.Contains("can only be int", solution.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void ASupertypeEquationIsItsSubtypeMirror()
    {
        // `:>=:` written the way the cookbook writes it: int may stand where ?x is required.
        var variable = new TypeVariable("x");

        var solution = new TypeSolver().Solve([TypeEquation.Supertype(Int, variable)]);

        Assert.True(solution.Succeeded);
        Assert.False(solution.IsSolved(variable));
    }

    [Fact]
    public void AConcreteEdgeTheRelationRefusesIsAContradiction()
    {
        var solution = new TypeSolver().Solve([TypeEquation.Subtype(Text, Int)]);

        Assert.False(solution.Succeeded);
        Assert.Contains("may not stand where", solution.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclaredSubtypeSatisfiesTheRelation()
    {
        // The point of solving against a relation rather than against equality: an edge the language declared
        // makes an equation hold that would otherwise be a contradiction.
        var relation = SubtypeRelation.Primitives.Add(Int, Text);

        Assert.False(new TypeSolver().Solve([TypeEquation.Subtype(Int, Text)]).Succeeded);
        Assert.True(new TypeSolver(relation).Solve([TypeEquation.Subtype(Int, Text)]).Succeeded);
    }

    [Fact]
    public void ASolveThatNeedsMorePassesThanTheBudgetStopsOnTheBudget()
    {
        // An equality between two unknowns is the one equation the first pass cannot apply: it is carried to the
        // next pass, and only there does the pinned side make it applicable. So this table needs two passes, and a
        // budget of one is exactly one pass short.
        var a = new TypeVariable("a");
        var b = new TypeVariable("b");

        var solution = new TypeSolver(budget: 1).Solve(
        [
            TypeEquation.Equal(a, b),
            TypeEquation.Equal(b, Int),
        ]);

        Assert.False(solution.Succeeded);
        Assert.Contains("did not settle", solution.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public void NoEquationSolvesTrivially()
    {
        var solution = new TypeSolver().Solve([]);

        Assert.True(solution.Succeeded);
        Assert.Equal(0, solution.Solved);
    }
}
