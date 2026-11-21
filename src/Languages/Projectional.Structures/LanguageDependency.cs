namespace Projectional.Structures;

/// <summary>Another language this one says it stands on, as <c>language.mps</c> names it.</summary>
/// <param name="Name">The name the dependency is declared by.</param>
/// <param name="Version">The version it asks for, or <see langword="null"/> when it asks for any.</param>
/// <remarks>
/// A declaration and nothing more: nothing in the framework resolves it, because resolving it means merging two
/// concept registries across a project boundary and that decision has its own record to be written first. Reading
/// it keeps the statement in the model, where the author wrote it and where a host can report it, instead of in a
/// comment.
/// </remarks>
public sealed record LanguageDependency(string Name, string? Version);
