using Projectional.Core;
using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>A language with one of every slot kind a body part can name, and every concept a root.</summary>
/// <remarks>
/// <para>
/// The lowering tests ask what a body answers, not how C# is shaped, so they are written against this fixture
/// instead of the reference language: only C#&apos;s document is a root there, and reaching a node with a child, a
/// reference and a boolean would mean building six levels of tree to test a delegate that reads one slot. One test
/// lowers a body against the real C# registry, so the fixture is not the only thing the narrowing is proven on.
/// </para>
/// <para>
/// Every concept is a root here, which is what keeps the tests to one line per node - and it costs nothing, since a
/// name function may be asked about a node wherever it happens to sit.
/// </para>
/// </remarks>
internal static class BehaviourTestLanguage
{
    public const string Node = "BehaviourFixture.Node";
    public const string Leaf = "BehaviourFixture.Leaf";

    /// <summary>A string property, filled by default so that a body reading it always answers.</summary>
    public const string ValueSlot = "value";

    /// <summary>A boolean property, which is what a choice can be decided on.</summary>
    public const string FlagSlot = "isFlag";

    /// <summary>A whole-number property, whose stored spelling can differ from a parseable one.</summary>
    public const string CountSlot = "count";

    /// <summary>The one child, so that a name can be made of another node's name.</summary>
    public const string InnerSlot = "inner";

    /// <summary>A list of children, which has no single name to give.</summary>
    public const string ItemsSlot = "items";

    /// <summary>The one reference, so that a name can be the name of what it points at.</summary>
    public const string TargetSlot = "target";

    /// <summary>A reference to a leaf, whose name is its own property.</summary>
    public const string PlaceSlot = "place";

    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            Node,
            [
                new PropertySlot(ValueSlot, PropertyType.String, "seven"),
                new PropertySlot(FlagSlot, PropertyType.Boolean, "false"),
                new PropertySlot(CountSlot, PropertyType.Integer, "0"),
                new ChildSlot(InnerSlot, Leaf, Cardinality.One),
                new ChildSlot(ItemsSlot, Leaf, Cardinality.Many),
                new ReferenceSlot(TargetSlot, Node, allowOwnSubtree: true),
                new ReferenceSlot(PlaceSlot, Leaf, allowOwnSubtree: true),
            ],
            isRoot: true,
            nameSlot: ValueSlot,
            description: "A node holding one of every slot kind a body can read."),

        new ConceptDeclaration(
            Leaf,
            [new PropertySlot(ValueSlot, PropertyType.String, "leaf")],
            isRoot: true,
            nameSlot: ValueSlot,
            description: "The child a body names, so that a name has something to name."),
    ]);

    public static Model NewModel() => new Project(Concepts).NewModel("behaviour fixture");
}
