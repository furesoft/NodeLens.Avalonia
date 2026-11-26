using System.Diagnostics;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
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

        // The same inputs `EditorSession` builds its own builder and layouter from - no emphasis, no types - so the two
        // extra phases add up to the end-to-end number beside them instead of measuring a different projection.
        var builder = new EditorTreeBuilder(scenario.Notations);
        var tree = builder.Build(root);
        var layouter = new CellLayouter(MonospaceTextMeasure.Instance);
        layouter.Layout(tree);

        return new Measurement
        {
            Units = units,
            Nodes = model.Nodes.Count(),
            Findings = engine.Check(model).Count,
            Fixes = checks.Find(model).Count,
            Tree = Best(repetitions, () => builder.Build(root)),
            Arrange = Best(repetitions, () => layouter.Layout(tree)),
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
            Rebuild = RebuildAfterChange(repetitions, builder, root, scenario.EditTarget(root)),
        };
    }

    /// <summary>
    /// Milliseconds to build the tree again after a change to one node - the projection's share of a keystroke.
    /// </summary>
    /// <remarks>
    /// The builder's cache is already warm from the <see cref="Measurement.Tree"/> phase, so this is the number the
    /// cache changes: the changed node and its ancestors are dirty and everything beside them is reused. It is
    /// deliberately not the end-to-end cost of a keystroke, because the session does work outside the rebuild that the
    /// projection does not own - resolving the caret's position, for one - and folding that in would credit the cache
    /// with a saving it does not make.
    /// </remarks>
    private static double RebuildAfterChange(int repetitions, EditorTreeBuilder builder, Node root, Node changed)
    {
        var dirty = new HashSet<NodeId>();

        for (var node = changed; node is not null; node = node.Parent)
        {
            dirty.Add(node.Id);
        }

        return Best(repetitions, () => builder.Build(root, dirty));
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
