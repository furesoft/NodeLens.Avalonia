namespace Projectional.Core.Tests;

/// <summary>
/// The language the tests that act on a <em>run</em> of nodes are written in: an item, and a group that is
/// itself an item and holds many of them.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TestLanguage"/> deliberately has no concept that can hold more than one child of what stands
/// where it stands, so it cannot express "wrap these three siblings in one new parent" at all - the operation
/// would have to put its wrapper into a single-valued slot. Growing the shared fixture for that would change
/// every test that counts its concepts, so the shape lives here instead: a group stands where an item stands
/// and holds any number of items, which is the smallest language in which a run can be wrapped.
/// </para>
/// <para>
/// It is a plausible little language rather than a fixture with a name like <c>Container</c>: nesting items in
/// a group is what a reader would expect to be able to do with them.
/// </para>
/// </remarks>
internal static class GroupLanguage
{
    public const string Item = "Group.Item";
    public const string Group = "Group.Group";

    public static ConceptRegistry Registry { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            Item,
            [new PropertySlot("name", PropertyType.String, "unnamed")],
            nameSlot: "name"),

        new ConceptDeclaration(
            Group,
            [new ChildSlot("items", Item)],
            [Item],
            nameSlot: "name"),
    ]);

    public static Project NewProject() => new(Registry);
}
