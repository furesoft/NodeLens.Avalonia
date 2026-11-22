using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Projectional.Toml;

namespace Projectional.Toml.Checks;

/// <summary>A number cell whose text is no longer a TOML number, with the declared default as its repair.</summary>
/// <remarks>
/// <para>
/// The concept holds the literal rather than a parsed value, so nothing stops a user from typing <c>abc</c> into it - and
/// the file would then say something a TOML reader refuses. The check is the price of that decision, and it is the right
/// price: holding a <c>long</c> would silently rewrite <c>0x2A</c>, <c>1_000</c> and <c>1e3</c>, which are three
/// different files.
/// </para>
/// <para>
/// The repair writes back the default the concept declares, which is the only value the language has an opinion about;
/// inventing a number from a broken one would be a guess dressed up as a fix.
/// </para>
/// </remarks>
public sealed class TomlNumberCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "TOML002";

    string ICheck.Code => Code;

    string ICheck.Title => "A number cell whose text is no longer a TOML number, with the declared default as its repair.";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "toml.number.reset";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            var isInteger = string.Equals(node.Concept.Key, TomlConceptKeys.Integer, StringComparison.Ordinal);
            var isFloat = string.Equals(node.Concept.Key, TomlConceptKeys.Float, StringComparison.Ordinal);

            if (!isInteger && !isFloat)
            {
                continue;
            }

            var literal = node.GetOrNull(TomlLanguage.TextSlot);

            if (literal is null || (isInteger ? TomlSpelling.IsInteger(literal) : TomlSpelling.IsFloat(literal)))
            {
                continue;
            }

            var fallback = node.Concept.FindProperty(TomlLanguage.TextSlot)?.DefaultValue ?? "0";

            var diagnostic = new Diagnostic(
                DiagnosticSeverity.Error,
                node.Id,
                $"\"{literal}\" is not a TOML {(isInteger ? "integer" : "float")}, so the document would not read back.",
                role: TomlLanguage.TextSlot,
                code: Code);

            findings.Add(new DiagnosticFix(
                diagnostic,
                FixId,
                $"Replace with \"{fallback}\"",
                _ => new SetNodePropertyAction(node.Id, TomlLanguage.TextSlot, fallback),
                "Puts back the number this concept starts out with, which parses again."));
        }

        return findings;
    }
}
