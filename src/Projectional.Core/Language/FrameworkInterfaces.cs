namespace Projectional.Core;

/// <summary>The vocabulary every language has, because every language names the things it declares.</summary>
/// <remarks>
/// <para>
/// A language used to retype the same string property and the same name-slot designation in every concept that a
/// reference could point at, and a consumer had to trust that the convention held. The framework owns the role
/// instead: a concept implements <see cref="Named"/> and has a name, and a slot can be declared over it, which is
/// the first time "anything with a name" is something a declaration can say.
/// </para>
/// <para>
/// <b>Injected, not composed.</b> <see cref="ConceptRegistry.Create"/> adds these declarations to every registry it
/// resolves, because a host composes a language by concatenating declaration lists and the one declaration every
/// language must share would be reported as a duplicate by exactly that composition. A host can rely on
/// <see cref="Named"/> meaning the same thing in every document it opens.
/// </para>
/// <para>
/// The key is <c>Framework.Named</c> and not <c>Named</c>: concept keys are <c>Language.Name</c> and the prefix is
/// the namespace, so a bare <c>Named</c> would be the one key without one - and would collide with the first
/// language that declares a concept of that name.
/// </para>
/// </remarks>
public static class FrameworkInterfaces
{
    /// <summary>The key a language names when it wants the framework's name property.</summary>
    public const string Named = "Framework.Named";

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
    public static IReadOnlyList<ConceptDeclaration> All { get; } = [NamedDeclaration];
}
