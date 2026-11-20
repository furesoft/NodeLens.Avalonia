namespace Projectional.Core;

/// <summary>The vocabulary every language has: a base type for everything, and a name for what is referable.</summary>
/// <remarks>
/// <para>
/// A language used to retype the same string property and the same name-slot designation in every concept that a
/// reference could point at, and a consumer had to trust that the convention held. The framework owns the role
/// instead: a concept implements <see cref="Named"/> and has a name, and a slot can be declared over it, which is
/// the first time "anything with a name" is something a declaration can say.
/// </para>
/// <para>
/// <see cref="BaseConcept"/> is the other half of that, and the reason it exists is a slot: before it, a
/// declaration could not say "any node", so a concept that wanted to hold one of anything had to name a concept of
/// its own language and hope - and a language that composes borrowed declarations had no answer at all. Every
/// concept now implements it without declaring anything, so <c>Framework.BaseConcept</c> is the one key whose
/// meaning holds across every document and every language, including one a user opens from a folder.
/// </para>
/// <para>
/// <b>Injected, not composed.</b> <see cref="ConceptRegistry.Create"/> adds these declarations to every registry it
/// resolves, because a host composes a language by concatenating declaration lists and the one declaration every
/// language must share would be reported as a duplicate by exactly that composition. A host can rely on
/// <see cref="Named"/> and <see cref="BaseConcept"/> meaning the same thing in every document it opens.
/// </para>
/// <para>
/// The keys are <c>Framework.Named</c> and <c>Framework.BaseConcept</c> and not bare words: concept keys are
/// <c>Language.Name</c> and the prefix is the namespace, so a bare <c>Named</c> would be the one key without one - and
/// would collide with the first language that declares a concept of that name.
/// </para>
/// </remarks>
public static class FrameworkInterfaces
{
    /// <summary>The key every concept implements, so that a slot can be declared over "any node".</summary>
    public const string BaseConcept = "Framework.BaseConcept";

    /// <summary>The key a language names when it wants the framework's name property.</summary>
    public const string Named = "Framework.Named";

    /// <summary>The interface declaration for <see cref="BaseConcept"/>.</summary>
    /// <remarks>
    /// <para>
    /// It carries no slot, and that is deliberate rather than a placeholder. A base concept that declared
    /// <c>name</c> would give every concept a second slot of a name some of them already declare, and the whole
    /// point of this one is to be the type of "anything" without changing what anything is.
    /// </para>
    /// <para>
    /// It is spelled as an interface because that is this framework's one mechanism for "every concept conforms
    /// without declaring it": <see cref="ConceptRegistry.Create"/> injects it into every declaration's implemented
    /// interfaces, exactly as it injects <see cref="Named"/>. An abstract concept a language had to extend would be
    /// opt-in, which is the opposite of what a base type is for
    /// ([ADR 0020](../../docs/adr/0020-framework-vocabulary-is-injected.md)).
    /// </para>
    /// </remarks>
    public static ConceptDeclaration BaseConceptDeclaration { get; } = new(
        BaseConcept,
        [],
        isAbstract: true,
        isInterface: true,
        description: "Anything a document can hold.");

    /// <summary>The interface declaration for <see cref="Named"/>.</summary>
    /// <remarks>
    /// It carries no default value on purpose: a placeholder word is the language's decision, because
    /// <c>STD001</c> reports a name still equal to the declared default and one language wants <c>"unnamed"</c>
    /// while another wants nothing at all. A concept that does want one declares the slot again with a default and
    /// keeps the interface for its designation.
    /// </remarks>
    public static ConceptDeclaration NamedDeclaration { get; } = new(
        Named,
        [new PropertySlot("name", PropertyType.String, description: "What references to this node display.")],
        nameSlot: "name",
        isAbstract: true,
        isInterface: true,
        description: "Something a reader can refer to by name.");

    /// <summary>Every declaration the framework adds to a registry, in the order it is added.</summary>
    /// <remarks>
    /// The base concept comes first: it is what everything else conforms to, so it is what a reader of the registry
    /// meets before the vocabulary that builds on it.
    /// </remarks>
    public static IReadOnlyList<ConceptDeclaration> All { get; } = [BaseConceptDeclaration, NamedDeclaration];
}
