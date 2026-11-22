using System.Globalization;
using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Projectional.Toml;

namespace Projectional.Toml.Checks;

/// <summary>One table naming the same key twice, with a rename to the first free suffix as its repair.</summary>
/// <remarks>
/// <para>
/// The importer refuses a file with a duplicate key, because TOML makes one a syntax error. Once the document is open,
/// the same state is reachable by editing - and the repair is a rename rather than a delete, because the two entries may
/// well be two things the user meant to keep.
/// </para>
/// <para>
/// <b>An array of tables may repeat its name.</b> <c>[[a]]</c> twice is two elements of one array, which is exactly what
/// the double bracket means, so only pairs and single-bracket headers share a namespace here.
/// </para>
/// <para>
/// Keys are compared as the text they were written as: <c>a</c> and <c>"a"</c> are one key to a TOML reader, and the
/// check treats them as two. That is the permissive direction, and it is deliberate - a false finding on a legal file
/// would be worse than a missing one on a file the importer already refused.
/// </para>
/// </remarks>
public sealed class TomlDuplicateKeyCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "TOML001";

    string ICheck.Code => Code;

    string ICheck.Title => "One table naming the same key twice, with a rename to the first free suffix as its repair.";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "toml.duplicate-key.rename";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            switch (node.Concept.Key)
            {
                case TomlConceptKeys.Document or TomlConceptKeys.Table or TomlConceptKeys.ArrayOfTables:
                    Collect(node.GetChildren(TomlLanguage.EntriesSlot), findings);
                    break;

                case TomlConceptKeys.InlineTable:
                    Collect(node.GetChildren(TomlLanguage.MembersSlot), findings);
                    break;
            }
        }

        return findings;
    }

    /// <summary>Reports every entry of <paramref name="entries"/> that repeats an earlier key.</summary>
    /// <remarks>
    /// The names already taken grow as proposals are made, so three entries named <c>a</c> propose <c>a2</c> and
    /// <c>a3</c> rather than both proposing <c>a2</c> - a repair that would only work if the user applied the first one.
    /// </remarks>
    private static void Collect(IReadOnlyList<Node> entries, List<DiagnosticFix> findings)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            var isArray = string.Equals(entry.Concept.Key, TomlConceptKeys.ArrayOfTables, StringComparison.Ordinal);

            if (isArray || entry.GetOrNull(TomlLanguage.KeySlot) is not { Length: > 0 } key)
            {
                continue;
            }

            if (taken.Add(key))
            {
                continue;
            }

            findings.Add(FixFor(entry, key, FirstFree(key, taken)));
        }
    }

    private static DiagnosticFix FixFor(Node entry, string key, string proposal)
    {
        var diagnostic = new Diagnostic(
            DiagnosticSeverity.Error,
            entry.Id,
            $"this table defines \"{key}\" a second time, which no TOML reader accepts.",
            role: TomlLanguage.KeySlot,
            code: Code);

        return new DiagnosticFix(
            diagnostic,
            FixId,
            $"Rename to \"{proposal}\"",
            _ => new SetNodePropertyAction(entry.Id, TomlLanguage.KeySlot, proposal),
            "Gives the entry a key the other entries of this table do not use.");
    }

    /// <summary><paramref name="key"/> with the first suffix that is not in <paramref name="taken"/>.</summary>
    private static string FirstFree(string key, HashSet<string> taken)
    {
        for (var suffix = 2; ; suffix++)
        {
            var candidate = string.Concat(key, suffix.ToString(CultureInfo.InvariantCulture));

            if (taken.Add(candidate))
            {
                return candidate;
            }
        }
    }
}
