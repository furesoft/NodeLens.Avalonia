using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Projectional.Xml;

namespace Projectional.Xml.Checks;

/// <summary>A comment whose text contains <c>--</c>, which XML forbids inside one.</summary>
/// <remarks>
/// <para>
/// A comment's text is stored as it was written and emitted verbatim, so a user who types the two dashes that end a
/// comment in the middle of one produces a document no XML reader accepts. The repair separates them, which is the
/// smallest change that keeps every character the user wrote.
/// </para>
/// <para>
/// It is a check rather than a constraint because the repair is unambiguous: unlike a missing condition, there is exactly
/// one spelling that means the same thing here.
/// </para>
/// </remarks>
public sealed class XmlCommentCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "XML002";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "xml.comment.separate-dashes";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            if (!string.Equals(node.Concept.Key, XmlConceptKeys.Comment, StringComparison.Ordinal))
            {
                continue;
            }

            if (node.GetOrNull(XmlLanguage.TextSlot) is not { } text || !text.Contains("--", StringComparison.Ordinal))
            {
                continue;
            }

            var repaired = text.Replace("--", "- -", StringComparison.Ordinal);

            var diagnostic = new Diagnostic(
                DiagnosticSeverity.Error,
                node.Id,
                "a comment cannot contain \"--\", so the file would not read back.",
                role: XmlLanguage.TextSlot,
                code: Code);

            findings.Add(new DiagnosticFix(
                diagnostic,
                FixId,
                "Separate the dashes",
                _ => new SetPropertyAction(CellAddress.OfSlot(node.Id, XmlLanguage.TextSlot), repaired),
                "Puts a space between the two dashes, which keeps the words and makes the comment legal again."));
        }

        return findings;
    }
}
