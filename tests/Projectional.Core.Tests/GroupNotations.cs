using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>The notation of <see cref="GroupLanguage"/>, written the way a language author would write it.</summary>
/// <remarks>
/// Every item draws on one line and a group draws its items indented below its header, so a wrapped run is
/// visible in a snapshot rather than merely present in the model - and so a caret in a wrapped item lands
/// somewhere two lines below the keyword.
/// </remarks>
internal static class GroupNotations
{
    public static NotationRegistry Registry { get; } = Create();

    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(GroupLanguage.Registry);

        notations.Add(GroupLanguage.Item, ed => ed.Line(
            ed.Keyword("item"),
            ed.Space,
            ed.Property("name")));

        notations.Add(GroupLanguage.Group, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("group"),
                ed.Space,
                ed.Property("name")),
            ed.Indent(ed.Children("items"))));

        return notations;
    }
}
