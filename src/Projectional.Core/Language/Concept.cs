using System.Diagnostics.CodeAnalysis;

namespace Projectional.Core;

/// <summary>
/// A concept as the runtime sees it: super concepts resolved and the full inherited slot set
/// flattened into one ordered list. Instances come from <see cref="ConceptRegistry"/>.
/// </summary>
public sealed class Concept
{
    private readonly Dictionary<string, SlotDeclaration> _slotsByName;
    private readonly HashSet<string> _ancestry;

    internal Concept(
        string key,
        IReadOnlyList<Concept> superConcepts,
        IReadOnlyList<SlotDeclaration> slots,
        PropertySlot? nameSlot,
        bool isAbstract,
        bool isOfferedInCompletion,
        string? description = null,
        PropertySlot? collapseSlot = null,
        Applicability? applicability = null,
        IReadOnlyList<ConceptTrigger>? triggers = null,
        bool isInterface = false,
        IReadOnlyList<Concept>? extensions = null,
        IReadOnlyList<Concept>? interfaces = null,
        NameRule? nameRule = null,
        Func<Node, string?>? nameFunction = null,
        string? alias = null,
        bool isRoot = true)
    {
        Key = key;
        SuperConcepts = superConcepts;
        Slots = slots;
        NameSlot = nameSlot;
        NameRule = nameRule;
        NameFunction = nameFunction;
        IsAbstract = isAbstract;
        IsOfferedInCompletion = isOfferedInCompletion;
        Description = description;
        Alias = alias;
        IsRoot = isRoot;
        CollapseSlot = collapseSlot;
        Applicability = applicability ?? Applicability.Anywhere;
        Triggers = triggers ?? [];
        IsInterface = isInterface;
        Extensions = extensions ?? superConcepts;
        Interfaces = interfaces ?? [];
        _slotsByName = new Dictionary<string, SlotDeclaration>(slots.Count, StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            _slotsByName.Add(slot.Name, slot);
        }

        _ancestry = new HashSet<string>(StringComparer.Ordinal) { key };
        foreach (var super in superConcepts)
        {
            _ancestry.UnionWith(super._ancestry);
        }
    }

    public string Key { get; }

    /// <summary>
    /// Every concept this one conforms to, in one ordered walk: the extensions first, then the interfaces, each in
    /// declaration order.
    /// </summary>
    /// <remarks>
    /// The union rather than two walks, because this is what <see cref="IsKindOf"/>, the slot merge and every
    /// aspect lookup traverse - a second list those walks did not see would be a second conformance relation by
    /// accident. <see cref="Extensions"/> and <see cref="Interfaces"/> are how a tool gets the difference back.
    /// </remarks>
    public IReadOnlyList<Concept> SuperConcepts { get; }

    /// <summary>The super concepts this one extends, in declaration order.</summary>
    public IReadOnlyList<Concept> Extensions { get; }

    /// <summary>The interfaces this one implements, in declaration order.</summary>
    public IReadOnlyList<Concept> Interfaces { get; }

    /// <summary>
    /// Whether this concept is an interface: abstract by definition, never instantiated, and the source of the
    /// properties and references its implementers inherit.
    /// </summary>
    /// <remarks>
    /// A kind, not a second relation. A slot declared over an interface accepts every implementer and every
    /// descendant of one, exactly as a slot declared over an abstract concept does, and a node is never an
    /// instance of one - which is why no notation is required and no place may be named
    /// (docs/architecture/interfaces.md).
    /// </remarks>
    public bool IsInterface { get; }

    /// <summary>
    /// The flat slot set: inherited slots first, then own ones, each in declaration order.
    /// This order is also the order the persistence format writes slots in.
    /// </summary>
    public IReadOnlyList<SlotDeclaration> Slots { get; }

    /// <summary>The string property that references to this concept display, if declared.</summary>
    public PropertySlot? NameSlot { get; }

    /// <summary>
    /// The function that computes what a node of this concept is called, or <see langword="null"/> when the name is
    /// the value of <see cref="NameSlot"/>.
    /// </summary>
    /// <remarks>
    /// Already resolved through the super concepts - own declaration first, then the first declaring super in
    /// declaration order - so no consumer walks the chain. <see cref="NameOf"/> is the answer; this is what it is
    /// built from, and it is exposed so that a lowering can see the difference between "declared here" and
    /// "inherited".
    /// </remarks>
    public Func<Node, string?>? NameFunction { get; }

    /// <summary>What <paramref name="node"/> is called: the declared function, else the name slot, else nothing.</summary>
    /// <param name="node">A node of this concept.</param>
    /// <returns>The name, or <see langword="null"/> when this concept answers neither way.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The one answer to "what is this node called", which <see cref="Node.Name"/> and therefore nine consumers -
    /// reference cells, sort by name, the outline, the problems panel, the scope, ambiguity, the stored fallback text,
    /// rename and the naming checks - read. A function is asked first and a slot second, so a concept that declares
    /// both keeps its slot readable for a slot declared over <see cref="FrameworkInterfaces.Named"/> while the
    /// computed word is what the drawing shows.
    /// </remarks>
    public string? NameOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return NameFunction is { } name
            ? name(node)
            : NameSlot is { } slot
                ? node.GetOrNull(slot.Name)
                : null;
    }

    /// <summary>
    /// What a name of this concept has to look like, or <see langword="null"/> when no concept in its chain declared
    /// a rule.
    /// </summary>
    /// <remarks>
    /// Inherited the way <see cref="NameSlot"/> and <see cref="CollapseSlot"/> are: the concept's own declaration
    /// wins, then the first declaring super in declaration order. The value here is already resolved, so a caller
    /// never walks the super concepts itself - but it stays nullable, because "nobody declared a rule" and "somebody
    /// declared <see cref="Core.NameRule.AnyText"/>" are different facts: only the second one survives inheritance,
    /// and only the second one makes a subtype's own rule disappear. A consumer that wants "what to enforce" reads
    /// <c>NameRule ?? NameRule.AnyText</c>.
    /// </remarks>
    public NameRule? NameRule { get; }

    /// <summary>
    /// Whether this concept only exists to be extended, so that no node is ever an instance of it.
    /// </summary>
    public bool IsAbstract { get; }

    /// <summary>
    /// Whether the editor may offer this concept when the user asks what can stand in a slot. It is a
    /// statement about the menu, not about the model: the concept stays a legal child of every slot
    /// that accepts it.
    /// </summary>
    public bool IsOfferedInCompletion { get; }

    /// <summary>
    /// The one sentence of documentation this concept declares, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Not inherited: <see cref="SuperConcepts"/> is walked for notation and for slots, but a sentence
    /// describes the concept it was written on. It is documentation rather than model data, so it is
    /// never stored per node and never appears in a change event.
    /// </remarks>
    public string? Description { get; }

    /// <summary>
    /// The short word a proposal list offers for this concept, or <see langword="null"/> when it offers the concept's
    /// own name.
    /// </summary>
    /// <remarks>
    /// Not inherited, and read only where a concept is <em>offered</em>: a notification of a concept is a word the
    /// user picks, while a node's name is a value in the model. The two never mix, which is why a host shows the alias
    /// in its list and the node's name in the document.
    /// </remarks>
    public string? Alias { get; }

    /// <summary>
    /// Whether a node of this concept may start a document; <see langword="true"/> when the language says nothing.
    /// </summary>
    /// <remarks>
    /// Asked by <see cref="Model.NewNode(Concept)"/> and by nothing else: the model's other ways to place a node -
    /// a child, a restored root, a pasted subtree - are about where an existing node goes, not about what a document
    /// may begin with.
    /// </remarks>
    public bool IsRoot { get; }

    /// <summary>
    /// The resolved slot that says whether a node of this concept may be folded, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Inherited the way <see cref="NameSlot"/> is: a concept that is foldable stays foldable in its
    /// subconcepts, and a language re-declares the designation only to override it. It is a
    /// <see cref="PropertySlot"/> rather than a name because the caller needs the type, and a collapse slot
    /// is always a boolean.
    /// </remarks>
    public PropertySlot? CollapseSlot { get; }

    /// <summary>Where nodes of this concept may stand; <see cref="Core.Applicability.Anywhere"/> when unconstrained.</summary>
    /// <remarks>
    /// A constraint the concept carries rather than one its slots carry, because it is not about the slot: the
    /// same concept may be allowed in one place and forbidden in another of the same type. The model refuses a
    /// placement it forbids, and the editor consults it before offering or creating one.
    /// </remarks>
    public Applicability Applicability { get; }

    /// <summary>The gestures that create this concept, in declaration order; empty when it has none.</summary>
    /// <remarks>
    /// Declared on the concept and not inherited: a trigger creates the concept it is declared on, so a subtype
    /// that reused its super concept's gesture would create the wrong node.
    /// </remarks>
    public IReadOnlyList<ConceptTrigger> Triggers { get; }


    /// <summary>Whether this concept is <paramref name="conceptKey"/> itself or extends it.</summary>
    public bool IsKindOf(string conceptKey) => conceptKey is not null && _ancestry.Contains(conceptKey);

    public bool TryGetSlot(string name, [NotNullWhen(true)] out SlotDeclaration? slot)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _slotsByName.TryGetValue(name, out slot);
    }

    public SlotDeclaration GetSlot(string name) => TryGetSlot(name, out var slot)
        ? slot
        : throw new KeyNotFoundException($"Concept '{Key}' declares no slot named '{name}'.");

    public PropertySlot? FindProperty(string name) => Find<PropertySlot>(name);

    public ChildSlot? FindChild(string name) => Find<ChildSlot>(name);

    public ReferenceSlot? FindReference(string name) => Find<ReferenceSlot>(name);

    private TSlot? Find<TSlot>(string name)
        where TSlot : SlotDeclaration =>
        TryGetSlot(name, out var slot) && slot is TSlot typed ? typed : null;

    public override string ToString() => Key;
}
