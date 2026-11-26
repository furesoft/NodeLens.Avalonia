using System.Globalization;
using System.Runtime.InteropServices;

namespace Projectional.Benchmarks;

/// <summary>
/// Measures what a document costs to lay out, to check and to recheck one piece of.
/// </summary>
/// <remarks>
/// <para>
/// The question this tool answers is the one the incrementality design leaves open: whether rechecking the whole
/// document after every change is expensive enough to be worth the dependency sets that a partial recheck needs. The
/// answer has to be measured rather than argued, because the two candidates fail differently - a full recheck is
/// always correct and costs the document, a partial one is cheaper and can silently miss a node that reads a name it
/// does not own.
/// </para>
/// <para>
/// Pass the number of timed runs as the first argument; five is the default. Run it in Release: a Debug build reports
/// numbers that are true of a Debug build.
/// </para>
/// </remarks>
internal static class Program
{
    public static int Main(string[] args)
    {
#if DEBUG
        Console.WriteLine("This is a Debug build. Run with -c Release for numbers that mean something.");
        Console.WriteLine();
#endif

        var repetitions = Repetitions(args);

        Console.WriteLine(
            $"best of {repetitions} runs each, {Environment.ProcessorCount} cores, {RuntimeInformation.FrameworkDescription}");

        foreach (var scenario in Scenarios())
        {
            Console.WriteLine();
            Console.WriteLine($"# {scenario.Name}");
            Table.Write(scenario.Sizes.Select(size => Benchmark.Run(scenario, size, repetitions)));
        }

        Console.WriteLine();
        Console.WriteLine("layout and every time are milliseconds; findings are the constraints' answers, fixes the checks'.");
        Console.WriteLine("tree is the projection's cells, arrange is measuring and placing them, layout is both inside a session.");
        Console.WriteLine("rebuild is the tree built again after a change to one node - the projection's share of a keystroke.");
        Console.WriteLine("subtree is one node rechecked alone - the seam a partial recheck would use.");

        return 0;
    }

    private static Scenario[] Scenarios() => [new RuleScenario(), new FeatureScenario()];

    private static int Repetitions(string[] args) =>
        args.Length > 0
        && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
        && parsed > 0
            ? parsed
            : 5;
}
