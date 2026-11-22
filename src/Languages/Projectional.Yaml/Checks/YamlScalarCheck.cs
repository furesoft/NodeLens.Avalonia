using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Projectional.Yaml;

namespace Projectional.Yaml.Checks;

/// <summary>A scalar whose text is no longer one scalar, with quoting it as its repair.</summary>
/// <remarks>
/// The language stores the file's whole spelling, so a cell the user edits is a cell where that spelling can stop being a
/// scalar: an unterminated quote, a colon that turns the text into a mapping. The repair wraps the text literally in
/// double quotes, which is the one spelling that can hold every character.
/// </remarks>
public sealed class YamlScalarCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "YAML002";

    string ICheck.Code => Code;

    string ICheck.Title => "A scalar whose text is no longer one scalar, with quoting it as its repair.";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "yaml.scalar.quote";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            if (!string.Equals(node.Concept.Key, YamlConceptKeys.Scalar, StringComparison.Ordinal))
            {
                continue;
            }

            var text = node.GetOrNull(YamlLanguage.TextSlot);

            if (text is null || YamlSpelling.IsLegalScalar(text))
            {
                continue;
            }

            var quoted = YamlSpelling.Quote(text);

            var diagnostic = new Diagnostic(
                DiagnosticSeverity.Error,
                node.Id,
                "this value is not one scalar as it stands, so the document would not read back.",
                role: YamlLanguage.TextSlot,
                code: Code);

            findings.Add(new DiagnosticFix(
                diagnostic,
                FixId,
                "Quote it",
                _ => new SetNodePropertyAction(node.Id, YamlLanguage.TextSlot, quoted),
                "Writes the value between double quotes, which can hold every character."));
        }

        return findings;
    }
}
