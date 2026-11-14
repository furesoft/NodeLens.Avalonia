using Projectional.Core;

namespace Projectional.Projection;

/// <summary>
/// Renders a model into a text grid, which is how notation tests are meant to assert on layout.
/// </summary>
/// <remarks>
/// <para>
/// This is the harness stub from the M0 roadmap, not the projection engine. Until projections
/// exist there is no notation to draw, so the body walks the tree and prints one indented line per
/// node. That is small, but it is real: it fixes the signature the engine will keep, and it gives
/// notation tests something to compare against before any cell is placed.
/// </para>
/// <para>
/// When the engine arrives, only this body changes. The contract stays: same input, same text, on
/// every machine, with no window and no font.
/// </para>
/// </remarks>
internal static class LayoutSnapshot
{
    private const int IndentPerLevel = 2;

    /// <summary>Renders <paramref name="model"/> as at most <paramref name="width"/> columns of text.</summary>
    internal static string Render(Model model, int width)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        var lines = new List<string>();
        foreach (var root in model.Roots)
        {
            Outline(lines, root, depth: 0);
        }

        // A grid needs at least one row, so an empty model renders through a blank one and comes
        // out of ToString as the empty string.
        var grid = new SnapshotGrid(width, Math.Max(1, lines.Count));

        for (var row = 0; row < lines.Count; row++)
        {
            // A line wider than the snapshot is cut, not wrapped: the width was asked for.
            var line = lines[row].Length > width ? lines[row][..width] : lines[row];
            grid.Write(row, 0, line);
        }

        return grid.ToString();
    }

    private static void Outline(List<string> lines, Node node, int depth)
    {
        var label = node.Name is { Length: > 0 } name ? $"{node.Concept.Key} {name}" : $"{node.Concept.Key}";
        lines.Add(new string(' ', depth * IndentPerLevel) + label);

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            Outline(lines, child, depth + 1);
        }
    }
}
