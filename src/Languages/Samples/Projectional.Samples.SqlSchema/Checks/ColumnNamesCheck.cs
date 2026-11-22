using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Samples.SqlSchema;
using Projectional.Semantics;

namespace Projectional.Samples.SqlSchema.Checks;

/// <summary>Two columns of one table under one name.</summary>
/// <remarks>
/// <para>
/// A table may not repeat a column name: a key names a column by name in the DDL, a reader looks one up by name, and
/// the generated `create table` would declare the same column twice - which no database accepts. A duplicate across
/// two *different* tables is ordinary and stays quiet, which is why the check walks tables rather than the document.
/// </para>
/// <para>
/// A check, because the repair is obvious: the later column of each duplicate group is renamed to the first free
/// suffix, the rule <see cref="PlaceholderNameCheck"/> uses for an unnamed node and the feature model for a repeated
/// feature. Not deleting it, even though the framework cannot delete a named node anyway.
/// </para>
/// </remarks>
public sealed class ColumnNamesCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "SQL001";

    string ICheck.Code => Code;

    string ICheck.Title => "Two columns of one table under one name.";

    /// <summary>The stable identifier of the repair that renames the later column.</summary>
    public const string FixId = "sql.column-name.rename";

    /// <summary>Every column whose name an earlier column of the same table already has.</summary>
    /// <param name="model">One document of a schema; every table in it is walked.</param>
    /// <returns>One fix per duplicate, in document order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> fixes = [];

        foreach (var table in model.Nodes.Where(node => node.Concept.Key == SqlConceptKeys.Table))
        {
            List<string> seen = [];

            foreach (var column in table.GetChildren(SqlSchemaLanguage.ColumnsSlot))
            {
                if (column.GetOrNull("name") is not { Length: > 0 } name)
                {
                    continue;
                }

                if (!seen.Contains(name))
                {
                    seen.Add(name);
                    continue;
                }

                var free = FreeName(seen, name);
                var id = column.Id;

                seen.Add(free);

                fixes.Add(new DiagnosticFix(
                    new Diagnostic(
                        DiagnosticSeverity.Error,
                        id,
                        $"\"{name}\" is already a column of this table, so a key could not tell the two apart.",
                        role: "name",
                        code: Code),
                    FixId,
                    $"Rename to \"{free}\"",
                    context => context.Node is null ? null : new SetNodePropertyAction(id, "name", free),
                    "Renames the later column, because the first one a reader meets keeps the name."));
            }
        }

        return fixes;
    }

    /// <summary>The first <c>name2</c>, <c>name3</c>, ... that no earlier column of the table uses.</summary>
    private static string FreeName(IReadOnlyList<string> seen, string name)
    {
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{name}{suffix}";

            if (!seen.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
