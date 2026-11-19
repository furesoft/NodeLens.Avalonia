using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Projectional.Hocon;

namespace Projectional.Hocon.Checks;

/// <summary>An unquoted value or key that is no longer a legal run, with quoting it as its repair.</summary>
/// <remarks>
/// The language stores the file's spelling, so a cell the user edits is a cell where the spelling can go wrong: a space
/// ends an unquoted run, a <c>{</c> starts an object, and a <c>#</c> begins a note - so a value holding one of them
/// would print a file that says something else. The repair quotes the text and escapes what a quoted string needs.
/// </remarks>
public sealed class HoconUnquotedCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "HOCON001";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "hocon.unquoted.quote";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            if (!string.Equals(node.Concept.Key, HoconConceptKeys.String, StringComparison.Ordinal)
                || !string.Equals(node.GetOrNull(HoconLanguage.QuotedSlot) ?? HoconLanguage.Unquoted, HoconLanguage.Unquoted, StringComparison.Ordinal))
            {
                continue;
            }

            var text = node.GetOrNull(HoconLanguage.TextSlot);

            if (text is null || HoconSpelling.IsLegalRun(text))
            {
                continue;
            }

            var escaped = HoconSpelling.Escape(text);

            var diagnostic = new Diagnostic(
                DiagnosticSeverity.Error,
                node.Id,
                "this unquoted value holds a character that ends a token, so the document would not read back.",
                role: HoconLanguage.TextSlot,
                code: Code);

            // Two writes are one edit, one undo step - and the one that changes nothing is left out, because a step that
            // changes nothing would make the repair report "unchanged" although the kind did change.
            EditAction repair = string.Equals(escaped, text, StringComparison.Ordinal)
                ? new SetNodePropertyAction(node.Id, HoconLanguage.QuotedSlot, HoconLanguage.Quoted)
                : EditProgram.Of(
                    new SetNodePropertyAction(node.Id, HoconLanguage.QuotedSlot, HoconLanguage.Quoted),
                    new SetNodePropertyAction(node.Id, HoconLanguage.TextSlot, escaped));

            findings.Add(new DiagnosticFix(
                diagnostic,
                FixId,
                "Quote it",
                _ => repair,
                "Writes the value between quotes, with the quotes and backslashes escaped."));
        }

        return findings;
    }
}
