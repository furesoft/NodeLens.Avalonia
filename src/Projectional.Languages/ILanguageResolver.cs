namespace Projectional.Languages;

/// <summary>Answers which language a declared dependency names, or says that it is not available.</summary>
/// <remarks>
/// <para>
/// The seam exists because where a language comes from is a deployment question and this library has no answer for
/// it: a folder, a package feed and an assembly are all plausible, and a loader that guessed one would decide the
/// other two out of existence. A host therefore supplies the answer, and
/// [ADR 0026](../../docs/adr/0026-a-dependency-resolves-to-the-vocabulary.md) records why that is the boundary.
/// </para>
/// <para>
/// The workbench's answer is the set of languages its window already holds, so a language project's declared
/// dependency is resolved against what the user opened - nothing is read from beside the folder.
/// </para>
/// </remarks>
public interface ILanguageResolver
{
    /// <summary>The language called <paramref name="name"/>, or <see langword="null"/> when the host has none.</summary>
    /// <param name="name">The name a <c>language.mps</c> declared as a dependency or an extension.</param>
    LanguageProject? Find(string name);
}
