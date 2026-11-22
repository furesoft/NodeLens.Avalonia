using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Projectional.Toml;

namespace Projectional.Toml.Checks;

/// <summary>A date or time cell whose text no longer parses, with the declared default as its repair.</summary>
/// <remarks>
/// <para>
/// TOML has four kinds of date and time - an offset date-time, a local date-time, a local date and a local time - and the
/// model keeps the text and the kind beside it. Editing the text can turn it into something no reader accepts, or into a
/// different kind, so the repair writes the default text back <em>and</em> the kind that text spells: two writes, one
/// edit, one undo step.
/// </para>
/// <para>
/// A text that still parses but as another kind is left alone here. The file is legal either way, and the kind is what
/// the file said - rewriting it because a later edit moved the value is exactly the silent normalisation this language
/// avoids.
/// </para>
/// </remarks>
public sealed class TomlDateTimeCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "TOML003";

    string ICheck.Code => Code;

    string ICheck.Title => "A date or time cell whose text no longer parses, with the declared default as its repair.";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "toml.datetime.reset";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            if (!string.Equals(node.Concept.Key, TomlConceptKeys.DateTime, StringComparison.Ordinal))
            {
                continue;
            }

            var text = node.GetOrNull(TomlLanguage.TextSlot);

            if (text is null || TomlSpelling.IsDateTime(text))
            {
                continue;
            }

            var fallback = node.Concept.FindProperty(TomlLanguage.TextSlot)?.DefaultValue ?? "1979-05-27T07:32:00Z";
            var kind = TomlSpelling.DateTimeKindOf(fallback) ?? TomlLanguage.OffsetDateTime;

            var diagnostic = new Diagnostic(
                DiagnosticSeverity.Error,
                node.Id,
                $"\"{text}\" is not a TOML date or time, so the document would not read back.",
                role: TomlLanguage.TextSlot,
                code: Code);

            findings.Add(new DiagnosticFix(
                diagnostic,
                FixId,
                $"Replace with \"{fallback}\"",
                _ => EditProgram.Of(
                    new SetNodePropertyAction(node.Id, TomlLanguage.TextSlot, fallback),
                    new SetNodePropertyAction(node.Id, TomlLanguage.KindSlot, kind)),
                "Puts back the date this concept starts out with, and the kind that text spells."));
        }

        return findings;
    }
}
