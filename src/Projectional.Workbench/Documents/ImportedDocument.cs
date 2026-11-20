using Projectional.Core;
using Projectional.Languages;

namespace Projectional.Workbench;

/// <summary>A document an opened language read out of a text file, and the language that read it.</summary>
/// <remarks>
/// The two travel together because the language is not derivable from the model afterwards: an imported document has
/// the concepts of its language but no folder it belongs to, and the shell needs the project for the aspects to open
/// with (its types, constraints, scopes, intentions and generators) as much as it needs the model for the tab.
/// </remarks>
/// <param name="Model">The model the importer filled.</param>
/// <param name="Language">The opened project whose importer read the text.</param>
internal sealed record ImportedDocument(Model Model, LanguageProject Language);
