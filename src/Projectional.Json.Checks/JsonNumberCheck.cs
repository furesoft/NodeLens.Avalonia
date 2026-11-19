using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Json;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Json.Checks;

/// <summary>A number cell whose text is no longer a JSON number, with the declared default as its repair.</summary>
/// <remarks>
/// <para>
/// The concept holds the literal rather than a parsed value, so nothing stops a user from typing <c>abc</c> into it -
/// and the file would then say something a JSON reader refuses. The check is the price of that decision, and it is the
/// right price: holding a <c>double</c> instead would silently rewrite <c>1.0</c>, <c>1e3</c> and
/// <c>0.1000000000000000055511151231257827</c>.
/// </para>
/// <para>
/// The repair writes back the default the concept declares, which is the only value the language has an opinion about;
/// inventing a number from a broken one would be a guess dressed up as a fix.
/// </para>
/// </remarks>
public sealed class JsonNumberCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "JSON002";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "json.number.reset";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            if (!string.Equals(node.Concept.Key, JsonConceptKeys.Number, StringComparison.Ordinal))
            {
                continue;
            }

            var literal = node.GetOrNull(JsonLanguage.TextSlot);

            if (literal is null || JsonSpelling.IsLegalNumber(literal))
            {
                continue;
            }

            var fallback = node.Concept.FindProperty(JsonLanguage.TextSlot)?.DefaultValue ?? "0";

            var diagnostic = new Diagnostic(
                DiagnosticSeverity.Error,
                node.Id,
                $"\"{literal}\" is not a JSON number, so the document would not read back.",
                role: JsonLanguage.TextSlot,
                code: Code);

            findings.Add(new DiagnosticFix(
                diagnostic,
                FixId,
                $"Replace with \"{fallback}\"",
                _ => new SetPropertyAction(CellAddress.OfSlot(node.Id, JsonLanguage.TextSlot), fallback),
                "Puts back the number this concept starts out with, which parses again."));
        }

        return findings;
    }
}
