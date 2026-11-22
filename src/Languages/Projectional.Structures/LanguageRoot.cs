namespace Projectional.Structures;

/// <summary>What a language says about itself in <c>language.mps</c>: its name, its version and what it stands on.</summary>
/// <param name="Name">What the language is called.</param>
/// <param name="Version">The version it declares for itself, or <see langword="null"/> when it declares none.</param>
/// <param name="Description">What it is for, in the author's words, or <see langword="null"/> when it says nothing.</param>
/// <param name="Dependencies">The languages it says it builds on, in the order the root names them.</param>
/// <param name="Extends">The languages it says it extends, in the order the root names them.</param>
/// <remarks>
/// <para>
/// The result of <see cref="StructureLowering.ReadLanguage"/>: the one document of a language project that is the
/// language rather than a part of it. It is a value and not a model, for the same reason every other lowering
/// result is - nothing above this point needs to know that a root was written as a node.
/// </para>
/// <para>
/// The two lists are declared, not resolved. Declaring a dependency says where the language belongs; merging that
/// language's concepts in is a different feature, and keeping the two apart is what lets a root be written before
/// the mechanism that acts on it exists.
/// </para>
/// </remarks>
public sealed record LanguageRoot(
    string Name,
    string? Version,
    string? Description,
    IReadOnlyList<LanguageDependency> Dependencies,
    IReadOnlyList<LanguageDependency> Extends);
