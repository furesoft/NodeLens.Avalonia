using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Projectional.Yaml;

namespace Projectional.Yaml.Checks;

/// <summary>A pair whose value stands on the key's line although it cannot, with moving it down as the repair.</summary>
/// <remarks>
/// A block collection has to start on the line below its key, and a template cannot ask what kind of node its child is -
/// so <c>Yaml.Pair.layout</c> records what the file did. Replacing a scalar value with a sequence is what breaks it, and
/// the repair writes the placement the child now needs.
/// </remarks>
public sealed class YamlPlacementCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "YAML001";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "yaml.placement.nest";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            if (!string.Equals(node.Concept.Key, YamlConceptKeys.Pair, StringComparison.Ordinal))
            {
                continue;
            }

            var value = node.GetChild(YamlLanguage.ValueSlot);

            if (value is null
                || node.GetOrNull(YamlLanguage.LayoutSlot) is not { } layout
                || string.Equals(layout, YamlLanguage.Nested, StringComparison.Ordinal))
            {
                continue;
            }

            if (value.Concept.Key is not (YamlConceptKeys.Mapping or YamlConceptKeys.Sequence))
            {
                continue;
            }

            var diagnostic = new Diagnostic(
                DiagnosticSeverity.Error,
                node.Id,
                "a block collection cannot be written on its key's line, so the document would not read back.",
                role: YamlLanguage.LayoutSlot,
                code: Code);

            findings.Add(new DiagnosticFix(
                diagnostic,
                FixId,
                "Write the value on the next line",
                _ => new SetNodePropertyAction(node.Id, YamlLanguage.LayoutSlot, YamlLanguage.Nested),
                "Records the placement the value needs, so the block starts on its own line."));
        }

        return findings;
    }
}
