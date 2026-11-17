using Projectional.Core;
using Projectional.Editing;

namespace Projectional.Core.Tests;

/// <summary>
/// Opens documents built from <see cref="GroupLanguage"/>, and builds the selections the run tests need.
/// </summary>
/// <remarks>
/// A selection over several nodes cannot be produced by the keyboard ladder, which widens from one value to
/// one node; it is what the user drags or what a command establishes. So a test names the first and the last
/// node and this helper turns them into the range the user would have drawn - end inside the first cell of
/// the first node, other end at the end of the last cell of the last one.
/// </remarks>
internal static class GroupEditor
{
    private static readonly string[] NestedNames = ["first", "second"];

    /// <summary>Opens <paramref name="document"/> for editing, shown through <see cref="GroupNotations"/>.</summary>
    public static EditorSession Open(Node document) => new(document, GroupNotations.Registry);

    /// <summary>A root group holding one item per entry of <paramref name="names"/>, in that order.</summary>
    public static (Node Group, IReadOnlyList<Node> Items) GroupWithItems(params string[] names)
    {
        var model = GroupLanguage.NewProject().NewModel("Group");
        var group = model.NewNode(GroupLanguage.Group);
        model.AddRoot(group);

        return (group, [.. names.Select(name =>
        {
            var item = model.NewChild(group, "items", GroupLanguage.Item);
            item.Set("name", name);

            return item;
        })]);
    }

    /// <summary>A root group holding one group with two items, which is what a nest needs.</summary>
    public static (Node Root, Node Nested, IReadOnlyList<Node> Items) NestedGroupWithItems()
    {
        var (root, _) = GroupWithItems();
        var nested = root.Model.NewChild(root, "items", GroupLanguage.Group);
        List<Node> items = [];

        foreach (var name in NestedNames)
        {
            var item = root.Model.NewChild(nested, "items", GroupLanguage.Item);
            item.Set("name", name);
            items.Add(item);
        }

        return (root, nested, items);
    }

    /// <summary>
    /// Selects everything from the first cell of <paramref name="first"/> to the last cell of
    /// <paramref name="last"/>, which covers both nodes and every place between them.
    /// </summary>
    public static void SelectRun(EditorSession session, Node first, Node last)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(last);

        var places = CaretPlaces.Of(session.Tree);
        var start = places.First(place => place.Address.Owner == first.Id);
        var end = places.Last(place => place.Address.Owner == last.Id);

        session.Select(new Selection(CaretPosition.At(start, 0), CaretPosition.At(end, end.Length)));
    }
}
