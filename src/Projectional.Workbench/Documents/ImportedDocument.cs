using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;

namespace Projectional.Workbench;

/// <summary>A document an opened language read out of a text file, and what the importer said about it.</summary>
/// <remarks>
/// The language travels with the model because it is not derivable from the model afterwards: an imported document has
/// the concepts of its language but no folder it belongs to, and the shell needs the project for the aspects to open
/// with (its types, constraints, scopes, intentions and generators) as much as it needs the model for the tab. The
/// result travels because a successful import can carry warnings, and a caller that only kept the model would have no
/// way to report them - which is the silent part of a silent rewrite.
/// </remarks>
/// <param name="Model">The model the importer filled.</param>
/// <param name="Language">The opened project whose importer read the text.</param>
/// <param name="Result">What the importer answered, which succeeded.</param>
internal sealed record ImportedDocument(Model Model, LanguageProject Language, ImportResult Result);
