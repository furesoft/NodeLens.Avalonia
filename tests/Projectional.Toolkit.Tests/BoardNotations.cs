using Projectional.Language;

namespace Projectional.Toolkit.Tests;

/// <summary>The notation of <see cref="BoardLanguage"/>, written with the builder a language author uses.</summary>
internal static class BoardNotations
{
    public static NotationRegistry Registry { get; } = Create();

    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(BoardLanguage.Concepts);

        notations.Add(BoardLanguage.Board, ed => ed.Sequence(
            ed.Line(ed.Keyword("board"), ed.Space, ed.Property("title")),
            ed.Indent(ed.Children(BoardLanguage.ItemsSlot, stacked: true))));

        notations.Add(BoardLanguage.Item, ed => ed.Line(
            ed.Punct("-"),
            ed.Space,
            ed.Property("text")));

        return notations;
    }
}