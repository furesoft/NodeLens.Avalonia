using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Samples.FeatureModel;
using Projectional.Semantics;

namespace Projectional.Samples.FeatureModel.Checks;

/// <summary>Two features of one model under one name.</summary>
/// <remarks>
/// <para>
/// A feature model is read by name: a constraint says "this feature requires that one", a reader says "select
/// Core", and the configuration generator writes the name into a file a build consumes. Two features called
/// <c>Core</c> therefore break all three at once - and the generated configuration would carry two
/// <c>Core=</c> lines, which is not a configuration but a file one of whose keys silently wins.
/// </para>
/// <para>
/// A <b>check</b> rather than a constraint, because there is a repair a language may choose: the later feature of
/// each duplicate group is renamed to the first free suffix, the same rule
/// <see cref="PlaceholderNameCheck"/> uses for an unnamed node. Which of the two was meant to keep the name is
/// not knowable, so the one the reader met first keeps it - the document order the user sees.
/// </para>
/// </remarks>
public sealed class FeatureNamesCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "FM001";

    string ICheck.Code => Code;

    string ICheck.Title => "Two features of one model under one name.";

    /// <summary>The stable identifier of the repair that renames the later feature.</summary>
    public const string FixId = "fm.feature-name.rename";

    /// <summary>Every feature whose name an earlier feature of the same model already has.</summary>
    /// <param name="model">The document to inspect; every root is walked.</param>
    /// <returns>One fix per duplicate, in document order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<string> seen = [];
        List<DiagnosticFix> fixes = [];

        foreach (var node in model.Nodes)
        {
            if (node.Concept.Key != FmConceptKeys.Feature || node.GetOrNull("name") is not { Length: > 0 } name)
            {
                continue;
            }

            if (!seen.Contains(name))
            {
                seen.Add(name);
                continue;
            }

            var free = FreeName(seen, name);
            var id = node.Id;

            seen.Add(free);

            fixes.Add(new DiagnosticFix(
                new Diagnostic(
                    DiagnosticSeverity.Warning,
                    id,
                    $"\"{name}\" is already the name of another feature, so nothing can refer to this one.",
                    role: "name",
                    code: Code),
                FixId,
                $"Rename to \"{free}\"",
                context => context.Node is null ? null : new SetNodePropertyAction(id, "name", free),
                "Renames the later feature, because the first one a reader meets keeps the name."));
        }

        return fixes;
    }

    /// <summary>The first <c>name2</c>, <c>name3</c>, ... that no earlier feature uses.</summary>
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
