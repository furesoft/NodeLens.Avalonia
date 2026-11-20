using Projectional.Core;
using Projectional.Editing;
using Projectional.Languages;
using Projectional.Projection;

namespace Projectional.Testing;

/// <summary>How a test names a node, and what it gets when the name does not resolve.</summary>
/// <remarks>
/// <para>
/// A node is named by the text the document shows at it, found through <see cref="Finder"/> - the same question a
/// user's find asks, and the same answer the editor test's caret step uses. That is the whole mechanism: a test
/// that had to spell a node id would be unreadable, would break the moment a fixture was regenerated, and could
/// not be reviewed in a diff.
/// </para>
/// <para>
/// It is one helper rather than a copy per assertion because two ways of finding a node would eventually disagree
/// about case, about which occurrence wins, or about whether a cell that shows nothing counts - and a test suite
/// whose two halves disagree is worse than one that cannot name a node at all.
/// </para>
/// </remarks>
internal static class TestNodes
{
    /// <summary>The node whose cell shows <paramref name="text"/>, or <see langword="null"/> with the reason.</summary>
    /// <param name="language">The language, which supplies the notation the search walks.</param>
    /// <param name="root">The document to search.</param>
    /// <param name="text">The text the document is expected to show somewhere.</param>
    /// <param name="refusal">Why there is no node, or <see langword="null"/> when there is one.</param>
    /// <remarks>
    /// Ordinal matching: a test that says <c>release</c> and lands on <c>Release</c> would pass for the wrong
    /// reason, and the point of spelling the text out is that the test is exact about where it stands.
    /// </remarks>
    internal static Node? Resolve(
        LanguageProject language,
        Node root,
        string text,
        out string? refusal)
    {
        using var session = new EditorSession(root, language.Notations);

        if (Match(session, text) is not { } match)
        {
            refusal = $"nothing shows '{text}', so no node is named by it";

            return null;
        }

        refusal = null;

        return root.Model.Project.FindNode(match.Address.Owner);
    }

    /// <summary>The first occurrence of <paramref name="text"/> in the document <paramref name="session"/> shows.</summary>
    internal static FindMatch? Match(EditorSession session, string text)
    {
        var matches = Finder.Find(session, new FindQuery(text, MatchCase: true));

        return matches.Count == 0 ? null : matches[0];
    }
}
