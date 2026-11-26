using System.Globalization;

namespace Projectional.Benchmarks;

/// <summary>Renders measurements as one fixed-width row per document.</summary>
/// <remarks>
/// Times are printed with one decimal and in the invariant culture, because a table read by a person comparing two
/// runs must not change its shape when the machine's culture changes. <see cref="CultureInfo.InvariantCulture"/> is
/// used rather than the current culture on purpose: a decimal comma would widen a column on one machine only.
/// </remarks>
internal static class Table
{
    public static void Write(IEnumerable<Measurement> rows)
    {
        Console.WriteLine(
            $"{"units",7}  {"nodes",7}  {"tree",8}  {"arrange",8}  {"layout",9}  {"rebuild",9}  {"constraints",12}  {"findings",8}  {"checks",8}  {"fixes",5}  {"together",9}  {"subtree",8}");

        foreach (var row in rows)
        {
            Console.WriteLine(
                $"{row.Units,7}  {row.Nodes,7}  {Ms(row.Tree),8}  {Ms(row.Arrange),8}  {Ms(row.Layout),9}  {Ms(row.Rebuild),9}  {Ms(row.Constraints),12}  {row.Findings,8}  {Ms(row.Checks),8}  {row.Fixes,5}  {Ms(row.Together),9}  {Ms(row.Subtree),8}");
        }
    }

    private static string Ms(double milliseconds) =>
        milliseconds.ToString("0.000", CultureInfo.InvariantCulture);
}
