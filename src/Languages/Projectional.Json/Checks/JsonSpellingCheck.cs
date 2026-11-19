using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Json;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Json.Checks;

/// <summary>A name or a string whose text is not spelled as JSON requires, with the escape as its repair.</summary>
/// <remarks>
/// <para>
/// The language stores the file's spelling, so a cell the user edits is a cell where the spelling can go wrong: a raw
/// quote inside a name ends it, a backslash that starts nothing is not an escape, and a raw control character is not
/// allowed at all. The document would then no longer parse, which is the one thing a reference language must not
/// produce, so the check reports it and the repair escapes exactly the characters that need it.
/// </para>
/// <para>
/// It reports the later spelling of a value that is already legal - <c>\u0061</c> for an <c>a</c> - as nothing, because
/// that is a legal file and this language keeps spellings rather than normalising them.
/// </para>
/// </remarks>
public sealed class JsonSpellingCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "JSON001";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "json.spelling.escape";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            if (string.Equals(node.Concept.Key, JsonConceptKeys.Property, StringComparison.Ordinal))
            {
                Inspect(node, JsonLanguage.NameSlot, "name", findings);
            }
            else if (string.Equals(node.Concept.Key, JsonConceptKeys.Text, StringComparison.Ordinal))
            {
                Inspect(node, JsonLanguage.TextSlot, "string", findings);
            }
        }

        return findings;
    }

    private static void Inspect(Node node, string slot, string what, List<DiagnosticFix> findings)
    {
        var spelled = node.GetOrNull(slot);

        if (spelled is null || JsonSpelling.IsLegalString(spelled))
        {
            return;
        }

        var diagnostic = new Diagnostic(
            DiagnosticSeverity.Error,
            node.Id,
            $"this {what} is not spelled as JSON requires, so the document would not read back.",
            role: slot,
            code: Code);

        var escaped = JsonSpelling.Escape(spelled);

        findings.Add(new DiagnosticFix(
            diagnostic,
            FixId,
            "Escape it",
            _ => new SetPropertyAction(CellAddress.OfSlot(node.Id, slot), escaped),
            "Writes the value with the quotes and backslashes escaped, which is how the same text is spelled in a file."));
    }
}
