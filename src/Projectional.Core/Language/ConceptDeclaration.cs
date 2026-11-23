namespace Projectional.Core;

/// <summary>
/// What a language says about one kind of node: its key, the slots it adds, the concepts it
/// extends, and which slot holds the name that references display.
/// </summary>
/// <remarks>
/// Super concepts exist for semantics — aspects, the inherited slot set, and conformance
/// checks. They deliberately do not shape storage: a node's data is always one flat slot map.
/// </remarks>
public sealed class ConceptDeclaration
{
    public ConceptDeclaration(
        string key,
        IEnumerable<SlotDeclaration>? slots = null,
        IEnumerable<string>? superConcepts = null,
        string? nameSlot = null,
        bool isAbstract = false,
        bool isOfferedInCompletion = true,
        string? description = null,
        string? collapseSlot = null,
        Applicability? applicability = null,
        IEnumerable<ConceptTrigger>? triggers = null,
        bool isInterface = false,
        IEnumerable<string>? interfaces = null,
        NameRule? nameRule = null,
        Func<Node, string?>? nameOf = null,
        string? alias = null,
        bool isRoot = true)
    {
        if (!NameRules.IsValidConceptKey(key))
        {
            throw new ArgumentException($"'{key}' is not a valid concept key.", nameof(key));
        }

        var slotList = new List<SlotDeclaration>();
        var slotNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in slots ?? [])
        {
            ArgumentNullException.ThrowIfNull(slot);

            if (!slotNames.Add(slot.Name))
            {
                throw new ArgumentException(
                    $"Concept '{key}' declares slot '{slot.Name}' more than once.",
                    nameof(slots));
            }

            slotList.Add(slot);
        }

        var superList = new List<string>();
        foreach (var super in superConcepts ?? [])
        {
            if (!NameRules.IsValidConceptKey(super))
            {
                throw new ArgumentException($"'{super}' is not a valid concept key.", nameof(superConcepts));
            }

            if (super == key)
            {
                throw new ArgumentException($"Concept '{key}' cannot be its own super concept.", nameof(superConcepts));
            }

            if (!superList.Contains(super, StringComparer.Ordinal))
            {
                superList.Add(super);
            }
        }

        if (nameSlot is not null && !NameRules.IsValidSlotName(nameSlot))
        {
            throw new ArgumentException($"'{nameSlot}' is not a valid slot name.", nameof(nameSlot));
        }

        if (isInterface && !isAbstract)
        {
            // An interface is abstract by definition, so saying both is the only spelling: a concrete
            // interface would be a concept nothing can instantiate and nothing can be below.
            throw new ArgumentException(
                $"Concept '{key}' is an interface, so it is abstract by definition; pass isAbstract: true or leave isInterface off.",
                nameof(isInterface));
        }

        var interfaceList = new List<string>();
        foreach (var implemented in interfaces ?? [])
        {
            if (!NameRules.IsValidConceptKey(implemented))
            {
                throw new ArgumentException($"'{implemented}' is not a valid concept key.", nameof(interfaces));
            }

            if (implemented == key)
            {
                throw new ArgumentException(
                    $"Concept '{key}' cannot implement itself.",
                    nameof(interfaces));
            }

            if (superList.Contains(implemented, StringComparer.Ordinal))
            {
                // The two lists become one walk, so a key in both would declare one conformance twice -
                // and which of the two a reader meant is not recoverable here.
                throw new ArgumentException(
                    $"Concept '{key}' names '{implemented}' twice, as a super concept and as an interface.",
                    nameof(interfaces));
            }

            if (!interfaceList.Contains(implemented, StringComparer.Ordinal))
            {
                interfaceList.Add(implemented);
            }
        }

        if (collapseSlot is not null && !NameRules.IsValidSlotName(collapseSlot))
        {
            throw new ArgumentException($"'{collapseSlot}' is not a valid slot name.", nameof(collapseSlot));
        }

        Key = key;
        Slots = slotList;
        SuperConcepts = superList;
        Interfaces = interfaceList;
        IsInterface = isInterface;
        NameSlot = nameSlot;
        NameRule = nameRule;
        NameOf = nameOf;
        IsAbstract = isAbstract;
        IsOfferedInCompletion = isOfferedInCompletion;
        CollapseSlot = collapseSlot;
        Applicability = applicability ?? Applicability.Anywhere;
        Triggers = [.. triggers ?? []];

        // Trimmed, and an empty one is no description at all: "declared nothing" and "declared a blank"
        // have to be one state, or a language ships a tooltip that shows nothing.
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        // The same normalisation for the same reason: an alias that is only whitespace would be offered as an
        // entry with no word in it.
        Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
        IsRoot = isRoot;
    }

    public string Key { get; }

    /// <summary>Slots declared directly on this concept, in declaration order.</summary>
    public IReadOnlyList<SlotDeclaration> Slots { get; }

    /// <summary>Direct super concepts, in declaration order.</summary>
    public IReadOnlyList<string> SuperConcepts { get; }

    /// <summary>
    /// The interfaces this concept implements, in declaration order.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="SuperConcepts"/> because the two say different things about a concept: extending
    /// one makes this concept a kind of it, while implementing one promises its members. Both are conformance -
    /// <see cref="ConceptRegistry"/> resolves them into one ordered walk - but a reader of a declaration, and a
    /// tool that reports what a concept is made of, needs the difference back.
    /// </remarks>
    public IReadOnlyList<string> Interfaces { get; }

    /// <summary>
    /// Whether this concept is an interface: abstract by definition, never instantiated, and the source of
    /// members its implementers inherit.
    /// </summary>
    /// <remarks>
    /// A kind, not a second relation - <see cref="Concept.IsKindOf"/> answers for an interface exactly as for an
    /// abstract concept, which is what lets a slot be declared over "anything with a name".
    /// </remarks>
    public bool IsInterface { get; }

    /// <summary>The slot that supplies the text a reference to this concept displays.</summary>
    public string? NameSlot { get; }

    /// <summary>
    /// The function that computes what a node of this concept is called, or <see langword="null"/> when the name is
    /// the value of <see cref="NameSlot"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pure function of the node, passed in the declaration exactly as <see cref="NameSlot"/> is a string passed in
    /// one, so this layer stays reflection-free and learns only that a concept may name its own nodes. It exists
    /// because three ordinary shapes have no name slot to read: a name spread over two properties, a name that *is* a
    /// value, and a name that is a sentence.
    /// </para>
    /// <para>
    /// Inherited the way <see cref="NameSlot"/> and <see cref="CollapseSlot"/> are: the concept's own function wins,
    /// then the first declaring super in declaration order. A concept that declares both keeps its slot - the slot is
    /// what a slot declared over <see cref="FrameworkInterfaces.Named"/> reads - and the function is what
    /// <see cref="Concept.NameOf"/> answers first.
    /// </para>
    /// </remarks>
    public Func<Node, string?>? NameOf { get; }

    /// <summary>This declaration with <paramref name="nameOf"/> as the function that names its nodes.</summary>
    /// <param name="nameOf">The function that computes what a node of this concept is called.</param>
    /// <returns>A copy of this declaration, carrying <paramref name="nameOf"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="nameOf"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A copy, because a declaration is immutable and the registry is built from it: a name lowered from a
    /// <c>behaviour</c> folder is read after the declarations exist and before the registry does, so the answer has to
    /// reach the declaration the registry is about to be built from rather than be installed into a concept that
    /// already exists. Everything else is carried over unchanged, which is what makes this safe to call on a
    /// declaration that came from a base language's folder.
    /// </remarks>
    public ConceptDeclaration WithNameOf(Func<Node, string?> nameOf)
    {
        ArgumentNullException.ThrowIfNull(nameOf);

        return new ConceptDeclaration(
            Key,
            Slots,
            SuperConcepts,
            NameSlot,
            IsAbstract,
            IsOfferedInCompletion,
            Description,
            CollapseSlot,
            Applicability,
            Triggers,
            IsInterface,
            Interfaces,
            NameRule,
            nameOf,
            Alias,
            IsRoot);
    }

    /// <summary>
    /// What a name of this concept has to look like, or <see langword="null"/> to inherit what the super concept
    /// says - the same designation <see cref="NameSlot"/> is.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> is not "any text": it is "nothing declared here", and the registry resolves it up the
    /// super concepts and only then falls back to <see cref="Core.NameRule.AnyText"/>. The declared form of this
    /// declaration is an empty slot for the same reason, so a language that says <c>identifier</c> on a base
    /// concept does not silently lose it below itself.
    /// </remarks>
    public NameRule? NameRule { get; }

    /// <summary>
    /// Whether this concept exists only to be extended. An abstract concept can declare slots and be
    /// named as a slot's content, but no node is ever an instance of it: it stands for "one of the
    /// concepts below me", the way a base type does in a typed language.
    /// </summary>
    /// <remarks>
    /// Without this, a slot that accepts several kinds of thing has to be declared once per kind or
    /// has to accept a concept that is meaningless on its own — and <see cref="Model.NewChild"/>
    /// would happily create that meaningless node.
    /// </remarks>
    public bool IsAbstract { get; }

    /// <summary>
    /// Whether the editor may offer this concept when the user asks what can stand in a slot.
    /// </summary>
    /// <remarks>
    /// A language hides the concepts a user reaches for by typing rather than by picking from a
    /// menu — a literal is the obvious case, because "an integer literal" is not something anyone
    /// wants to read in a list of eight entries. Hiding one only silences the list: the concept is
    /// still a legal child of every slot that accepts it, and it can still be what
    /// <see cref="ConceptRegistry.DefaultInstantiableFor"/> materializes when the user presses Enter
    /// in an empty slot. Otherwise a language would have no way left to create it.
    /// </remarks>
    public bool IsOfferedInCompletion { get; }

    /// <summary>
    /// One sentence of documentation about this concept, shown where the concept is met: on hover over a
    /// node of it and beside the completion entry that offers it. <see langword="null"/> when the language
    /// declared none.
    /// </summary>
    /// <remarks>
    /// Language data, not model data: it is not persisted per node, creates no undo step and is not
    /// inherited - a super concept's sentence describes the super concept, not the node the user is
    /// looking at. A surface shows nothing at all when it is absent, because an empty box that repeats
    /// the concept key teaches the user that hovering is pointless.
    /// </remarks>
    public string? Description { get; }

    /// <summary>
    /// The slot that says whether a node of this concept may be folded in the editor, or
    /// <see langword="null"/> when the concept does not opt in.
    /// </summary>
    /// <remarks>
    /// A designated slot like <see cref="NameSlot"/>: the permission is a value on the node, so two nodes
    /// of one concept can differ - one rule's body is worth hiding and its sibling's is not. A concept that
    /// never nominates one behaves exactly as it did before folding existed.
    /// </remarks>
    public string? CollapseSlot { get; }

    /// <summary>
    /// Where nodes of this concept may stand, or <see cref="Applicability.Anywhere"/> when the language
    /// constrains nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the concepts that are not valid wherever their type is accepted: an attribute on a class, a method or
    /// a parameter and nowhere else, an XML comment only in front of a class or a method. Without it the editor
    /// offers such a concept in slots that would reject it, and the model accepts a node the language forbids
    /// ([Applicability](../architecture/language.md#applicability)).
    /// </para>
    /// <para>
    /// A place names a concept and, optionally, one slot of it; naming a super concept is how "anywhere inside a
    /// declaration" is written. The registry checks every place at registration - the concept must exist, and a
    /// named slot must hold children that accept this concept - so a typo is a language error at load, not a
    /// keystroke that does nothing.
    /// </para>
    /// </remarks>
    public Applicability Applicability { get; }

    /// <summary>
    /// The gestures that create this concept by typing at the edge of a node that already exists, in declaration
    /// order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Empty is the normal case: a concept is created from completion or from an empty slot unless the language
    /// says that a character at a neighbouring node means it. <c>5</c> followed by <c>+</c> is the reference
    /// case - the property refuses the character, and this list is what makes it an addition instead
    /// ([triggers.md](../architecture/triggers.md)).
    /// </para>
    /// <para>
    /// <b>Not inherited.</b> A trigger creates the concept it is declared on, so a subtype reusing its super
    /// concept's trigger would create the super concept. A language that wants the gesture below itself declares
    /// it again, which is also what makes the intent visible.
    /// </para>
    /// <para>
    /// The registry checks every trigger at registration - the anchor concept exists, the anchor slot is a
    /// single child of this concept and accepts the anchor, the property and caret slots exist, and the concept
    /// is concrete - so a typo is a language error at load rather than a keystroke that silently does nothing.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ConceptTrigger> Triggers { get; }

    /// <summary>
    /// The short word completion offers for this concept instead of the concept's own name, or
    /// <see langword="null"/> when it offers the name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A key is namespaced and a name may be long, while a list of proposals is read and filtered by what the user
    /// types. The alias is that other word: <c>RS</c> for a rule set, <c>If</c> for a conditional. It is a display
    /// word only - accepting the entry still inserts the concept this declaration names - so a language can offer a
    /// short word without renaming anything.
    /// </para>
    /// <para>
    /// Not inherited: an alias belongs to the concept it was written on, and a subtype that wanted the super
    /// concept's word would offer the wrong concept.
    /// </para>
    /// </remarks>
    public string? Alias { get; }

    /// <summary>
    /// Whether a node of this concept may start a document, or <see langword="true"/> when the language says nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A concept that refuses is one that only exists inside another: a rule's condition, an operator, an XML
    /// attribute. Without the flag a document can be opened on a concept that has nothing to say on its own, and
    /// the mistake is found by reading the file.
    /// </para>
    /// <para>
    /// <see langword="true"/> by default because that is what every concept meant before the flag existed - a
    /// language opts out, it never opts in. Refusing is also the model's answer to "may this be a root"
    /// (<see cref="Model.NewNode(Concept)"/>); loading a document never consults it, because a file this app can
    /// read is not refused for what its language has since said ([persistence](../architecture/persistence.md)).
    /// </para>
    /// </remarks>
    public bool IsRoot { get; }

    public override string ToString() => Key;
}
