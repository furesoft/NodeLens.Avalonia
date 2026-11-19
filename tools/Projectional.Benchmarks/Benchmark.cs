using System.Diagnostics;
using Projectional.Editing;
using Projectional.Semantics;

namespace Projectional.Benchmarks;

/// <summary>Measures one document of one scenario, phase by phase.</summary>
/// <remarks>
/// Every phase runs once before it is timed, because the first call pays for JIT, for lazily built registries and for
/// whatever the garbage collector does on a cold heap - none of which a second keystroke pays. The reported number is
/// the best of several runs rather than the mean: the machine is not idle, so the fastest run is the one least
/// disturbed, and a benchmark that averaged would report the noise of the environment as a property of the code.
/// </remarks>
internal static class Benchmark
{
    public static Measurement Run(Scenario scenario, int units, int repetitions)
    {
        var (model, root) = scenario.Build(units);
        var representative = scenario.Representative(root);
        var engine = new ConstraintEngine(scenario.Constraints, DefaultScopeProvider.Instance, scenario.Types);
        var checks = scenario.Checks;

        return new Measurement
        {
            Units = units,
            Nodes = model.Nodes.Count(),
            Findings = engine.Check(model).Count,
            Fixes = checks.Find(model).Count,
            Layout = Best(repetitions, () =>
            {
                using var session = new EditorSession(root, scenario.Notations);
            }),
            Constraints = Best(repetitions, () => engine.Check(model)),
            Checks = Best(repetitions, () => checks.Find(model)),
            Together = Best(repetitions, () =>
            {
                engine.Check(model);
                checks.Find(model);
            }),
            Subtree = Best(repetitions, () => engine.Check(representative)),
        };
    }

    private static double Best(int repetitions, Action body)
    {
        body();

        var best = double.MaxValue;

        for (var i = 0; i < repetitions; i++)
        {
            var watch = Stopwatch.StartNew();
            body();
            watch.Stop();
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }

        return best;
    }
}
