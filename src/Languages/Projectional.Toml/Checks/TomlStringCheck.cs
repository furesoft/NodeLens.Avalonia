using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Projectional.Toml;

namespace Projectional.Toml.Checks;

/// <summary>A string whose text is not spelled as the kind it declares, with the escape as its repair.</summary>
/// <remarks>
/// <para>
/// The language stores the file's spelling, so a cell the user edits is a cell where the spelling can go wrong: a raw
/// quote inside a basic string ends it, a backslash that starts nothing is not an escape, and a newline cannot stand in a
/// single-line string at all. The repair re-spells the text as a basic string with everything that needs it escaped - and
/// changes the kind to <c>basic</c>, because a literal string has no escapes to offer.
/// </para>
/// <para>
/// The text is taken literally, which means a backslash the user already wrote becomes two. That is the honest reading of
/// a value that does not parse: the characters on screen are the characters it keeps.
/// </para>
/// </remarks>
public sealed class TomlStringCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "TOML004";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "toml.string.escape";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            if (!string.Equals(node.Concept.Key, TomlConceptKeys.String, StringComparison.Ordinal))
            {
                continue;
            }

            var text = node.GetOrNull(TomlLanguage.TextSlot);

            if (text is null)
            {
                continue;
            }

            var kind = node.GetOrNull(TomlLanguage.KindSlot) ?? TomlLanguage.BasicString;

            if (TomlSpelling.IsStringContent(text, kind))
            {
                continue;
            }

            var escaped = TomlSpelling.Escape(text);

            var diagnostic = new Diagnostic(
                DiagnosticSeverity.Error,
                node.Id,
                "this string is not spelled as TOML requires, so the document would not read back.",
                role: TomlLanguage.TextSlot,
                code: Code);

            // Two writes need one edit and one undo step; a kind that is already basic is left out, because a step that
            // changes nothing would make the repair report "unchanged" although the text did change.
            EditAction repair = string.Equals(kind, TomlLanguage.BasicString, StringComparison.Ordinal)
                ? new SetNodePropertyAction(node.Id, TomlLanguage.TextSlot, escaped)
                : EditProgram.Of(
                    new SetNodePropertyAction(node.Id, TomlLanguage.KindSlot, TomlLanguage.BasicString),
                    new SetNodePropertyAction(node.Id, TomlLanguage.TextSlot, escaped));

            findings.Add(new DiagnosticFix(
                diagnostic,
                FixId,
                "Escape it",
                _ => repair,
                "Writes the value as a basic string with the quotes, backslashes and line breaks escaped."));
        }

        return findings;
    }
}
