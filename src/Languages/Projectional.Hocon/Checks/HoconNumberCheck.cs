using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Projectional.Hocon;

namespace Projectional.Hocon.Checks;

/// <summary>A number cell whose text is no longer a HOCON number, with the declared default as its repair.</summary>
/// <remarks>
/// The concept holds the literal rather than a parsed value, so nothing stops a user from typing <c>abc</c> into it - and
/// the file would then say something else entirely, because an unquoted run is a string. The repair writes the default
/// the concept declares: inventing a number from a broken one would be a guess dressed up as a fix.
/// </remarks>
public sealed class HoconNumberCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "HOCON002";

    string ICheck.Code => Code;

    string ICheck.Title => "A number cell whose text is no longer a HOCON number, with the declared default as its repair.";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "hocon.number.reset";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            if (!string.Equals(node.Concept.Key, HoconConceptKeys.Number, StringComparison.Ordinal))
            {
                continue;
            }

            var literal = node.GetOrNull(HoconLanguage.LiteralSlot);

            if (literal is null || HoconSpelling.IsNumber(literal))
            {
                continue;
            }

            var fallback = node.Concept.FindProperty(HoconLanguage.LiteralSlot)?.DefaultValue ?? "0";

            var diagnostic = new Diagnostic(
                DiagnosticSeverity.Error,
                node.Id,
                $"\"{literal}\" is not a HOCON number, so the document would not read back as one.",
                role: HoconLanguage.LiteralSlot,
                code: Code);

            findings.Add(new DiagnosticFix(
                diagnostic,
                FixId,
                $"Replace with \"{fallback}\"",
                _ => new SetNodePropertyAction(node.Id, HoconLanguage.LiteralSlot, fallback),
                "Puts back the number this concept starts out with, which parses again."));
        }

        return findings;
    }
}
