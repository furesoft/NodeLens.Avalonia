using System.Xml;
using System.Xml.Linq;
using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Projectional.Xml;

namespace Projectional.Xml.Checks;

/// <summary>Text or an attribute value that is not a spelling XML would read back, with the escape as its repair.</summary>
/// <remarks>
/// <para>
/// The importer settles the spelling when a value enters the model and the generator emits it verbatim, so a cell the
/// user edits is the one place the two can disagree: a bare <c>&amp;</c> is not character data, a <c>&lt;</c> starts
/// markup, and a literal tab in an attribute value is normalised to a space on the way back in. The file would then no
/// longer parse, which is the one thing the language cannot allow.
/// </para>
/// <para>
/// "Canonical" is decided by asking the parser, not by matching a list of forbidden characters: the value is wrapped in
/// an element, read back, and spelled again with the same rules the importer uses. A value that already is the spelling
/// of what it says gets no finding, so a reader is never told to change a value that is correct.
/// </para>
/// <para>
/// Text and attribute values are two different spellings, which is why each is read with its own function rather than
/// with a flag in the middle of the check.
/// </para>
/// </remarks>
public sealed class XmlSpellingCheck : ICheck
{
    /// <summary>The diagnostic for a text or attribute value that would not survive the round trip.</summary>
    public const string Code = "XML001";

    string ICheck.Code => Code;

    string ICheck.Title => "Text or an attribute value that is not a spelling XML would read back, with the escape as its repair.";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "xml.spelling.escape";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            if (string.Equals(node.Concept.Key, XmlConceptKeys.Text, StringComparison.Ordinal))
            {
                Inspect(node, XmlLanguage.TextSlot, XmlEscaping.Text, "text", findings);
            }
            else if (string.Equals(node.Concept.Key, XmlConceptKeys.Attribute, StringComparison.Ordinal))
            {
                Inspect(node, XmlLanguage.ValueSlot, XmlEscaping.Attribute, "attribute value", findings);
            }
        }

        return findings;
    }

    /// <summary>Reports <paramref name="node"/>'s <paramref name="slot"/> when it is not its own canonical spelling.</summary>
    private static void Inspect(
        Node node,
        string slot,
        Func<string, string> spell,
        string what,
        List<DiagnosticFix> findings)
    {
        var stored = node.GetOrNull(slot);

        if (stored is null)
        {
            return;
        }

        // What the value would be if it were written again: null means it is not character data at all.
        var canonical = Decode(stored) is { } decoded ? spell(decoded) : null;

        if (canonical is not null && string.Equals(canonical, stored, StringComparison.Ordinal))
        {
            return;
        }

        var repaired = canonical ?? spell(stored);

        var diagnostic = new Diagnostic(
            DiagnosticSeverity.Error,
            node.Id,
            $"this {what} is not spelled as XML requires, so the file would not read back.",
            role: slot,
            code: Code);

        findings.Add(new DiagnosticFix(
            diagnostic,
            FixId,
            "Escape it",
            _ => new SetPropertyAction(CellAddress.OfSlot(node.Id, slot), repaired),
            "Writes the same characters with the escapes XML uses, so the file means what the cell says."));
    }

    /// <summary>What <paramref name="spelled"/> stands for when it is character data, else <see langword="null"/>.</summary>
    private static string? Decode(string spelled)
    {
        try
        {
            return XElement.Parse($"<x>{spelled}</x>", LoadOptions.PreserveWhitespace).Value;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
