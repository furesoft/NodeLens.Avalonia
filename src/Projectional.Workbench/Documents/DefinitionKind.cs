namespace Projectional.Workbench;

/// <summary>One kind of language-definition document the shell can start, and the aspect it belongs to.</summary>
/// <param name="Id">What its command is called after <c>new-</c>, which is also the model's name.</param>
/// <param name="Label">What the menu calls it.</param>
/// <param name="Folder">
/// The aspect folder of <see cref="Projectional.Languages.LanguageProject.AspectFolders"/> that holds it.
/// </param>
/// <param name="ConceptKey">The concept its one root is, which is what makes the document that definition.</param>
/// <remarks>
/// A value rather than a factory method per kind, because the menu, the command registry and the tests all ask the
/// same three questions of it. A method per kind was what the shell had, and it could start four of the ten kinds of
/// definition a language declares - an aspect an author could open but not write.
/// </remarks>
internal sealed record DefinitionKind(string Id, string Label, string Folder, string ConceptKey);
