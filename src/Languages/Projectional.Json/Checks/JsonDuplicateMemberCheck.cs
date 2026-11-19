using System.Globalization;
using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Json;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Json.Checks;

/// <summary>One object naming the same member twice, with a rename to the first free suffix as its repair.</summary>
/// <remarks>
/// <para>
/// The importer keeps a duplicate member and reports it once, because a consumer of the file sees both and dropping one
/// would rewrite the user's document. Once the document is edited, the same state is a finding the user can act on - and
/// the repair is a rename, not a delete, because the two members may well be two things they meant to keep.
/// </para>
/// <para>
/// Names are compared by the value they spell, so <c>"a"</c> and <c>"\u0061"</c> are one name written twice. A name that
/// does not decode at all is compared as the text it is, which is the best answer available for a value that is broken
/// anyway - and <see cref="JsonSpellingCheck"/> is what reports that.
/// </para>
/// </remarks>
public sealed class JsonDuplicateMemberCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "JSON003";

    /// <summary>The identifier of the one repair this check offers.</summary>
    public const string FixId = "json.duplicate-name.rename";

    /// <inheritdoc/>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var node in model.Nodes)
        {
            if (string.Equals(node.Concept.Key, JsonConceptKeys.Object, StringComparison.Ordinal))
            {
                CollectFrom(node, findings);
            }
        }

        return findings;
    }

    /// <summary>Reports every member of <paramref name="container"/> that repeats an earlier member's name.</summary>
    /// <remarks>
    /// The names already taken grow as proposals are made, so three members named <c>a</c> propose <c>a2</c> and
    /// <c>a3</c> rather than both proposing <c>a2</c> - a repair that would only work if the user applied the first one.
    /// </remarks>
    private static void CollectFrom(Node container, List<DiagnosticFix> findings)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in container.GetChildren(JsonLanguage.MembersSlot))
        {
            // A note is not a member: it has no name to collide with, and reading its text as one would report two
            // comments with the same words as a duplicate key.
            if (!string.Equals(member.Concept.Key, JsonConceptKeys.Property, StringComparison.Ordinal))
            {
                continue;
            }

            if (member.GetOrNull(JsonLanguage.NameSlot) is not { Length: > 0 } spelled)
            {
                continue;
            }

            var name = JsonSpelling.Decode(spelled);

            if (taken.Add(name))
            {
                continue;
            }

            findings.Add(FixFor(member, name, FirstFree(name, taken)));
        }
    }

    private static DiagnosticFix FixFor(Node member, string name, string proposal)
    {
        var diagnostic = new Diagnostic(
            DiagnosticSeverity.Warning,
            member.Id,
            $"this object already has a member called \"{name}\", and a reader sees only one of them.",
            role: JsonLanguage.NameSlot,
            code: Code);

        return new DiagnosticFix(
            diagnostic,
            FixId,
            $"Rename to \"{proposal}\"",
            _ => new SetPropertyAction(CellAddress.OfSlot(member.Id, JsonLanguage.NameSlot), proposal),
            "Gives the member a name the object's other members do not use.");
    }

    /// <summary><paramref name="name"/> with the first suffix that is not in <paramref name="taken"/>.</summary>
    private static string FirstFree(string name, HashSet<string> taken)
    {
        for (var suffix = 2; ; suffix++)
        {
            var candidate = string.Concat(name, suffix.ToString(CultureInfo.InvariantCulture));

            if (taken.Add(candidate))
            {
                return candidate;
            }
        }
    }
}
