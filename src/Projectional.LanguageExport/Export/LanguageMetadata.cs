namespace Projectional.LanguageExport;

/// <summary>What a language's root says about it, which an export states in the assembly's own metadata.</summary>
/// <param name="Version">The version the author spelled, or <see langword="null"/> when the root names none.</param>
/// <param name="Description">What the language is for, in the author's words, or <see langword="null"/> for none.</param>
/// <param name="DependsOn">The languages it says it builds on, each written as <c>Name</c> or <c>Name 1.0.0</c>.</param>
/// <param name="Extends">The languages whose behaviour it inherits, written the same way.</param>
/// <remarks>
/// <para>
/// The facts and not the attributes: which attribute carries them is the writer's decision, so a caller that wants
/// only the source does not have to know the spelling. It is the root's identity - <c>language.mps</c> - rather than
/// the folder's, which is why an exported package can say what it is even when the two differ.
/// </para>
/// <para>
/// A version stays a label here as it is in the model: it becomes the assembly's <em>informational</em> version and
/// never its assembly version, because that field is four numbers and nothing in this repository parses a version a
/// user typed.
/// </para>
/// </remarks>
public sealed record LanguageMetadata(
    string? Version,
    string? Description,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> Extends)
{
    /// <summary>No version and nothing it stands on: the identity of a language whose root declares nothing.</summary>
    public static LanguageMetadata Empty { get; } = new(null, null, [], []);
}
