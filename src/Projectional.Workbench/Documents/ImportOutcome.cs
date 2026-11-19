using Projectional.Core;
using Projectional.Language;

namespace Projectional.Workbench;

/// <summary>What importing a file produced: a document, or the diagnostics that refused it.</summary>
/// <remarks>
/// The model is always the one that was built, so the two outcomes are read from <see cref="Result"/> rather than from a
/// second flag: a refusal leaves it empty, and a success carries the roots the importer added. Keeping one value rather
/// than a nullable model and a loose list is what lets the caller write one branch - either there is a document to open,
/// or there is something to tell the user.
/// </remarks>
/// <param name="Language">The language that was imported.</param>
/// <param name="Model">The model it was imported into, empty when the text was refused.</param>
/// <param name="Result">What the importer answered.</param>
internal sealed record ImportOutcome(ReferenceLanguage Language, Model Model, ImportResult Result)
{
    /// <summary>Whether the text became a document of <see cref="Language"/>.</summary>
    internal bool Succeeded => Result.Succeeded;

    /// <summary>What to show when the text was refused: the first error, in the importer's own words.</summary>
    internal ImportDiagnostic? Refusal =>
        Result.Succeeded || Result.Diagnostics.Count == 0 ? null : Result.Diagnostics[0];
}
