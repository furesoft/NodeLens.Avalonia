using Projectional.Core;

namespace Projectional.Workbench;

/// <summary>Reads a file's text into a document of the reference language that claims it.</summary>
/// <remarks>
/// <para>
/// Importing is deliberately not "opening": the file on disk is not linked to the document that comes out of it. What is
/// built is a model, which the user saves as a <c>.mps</c> document or generates back into the format; editing the
/// original file afterwards changes nothing here, and writing the model never writes that file.
/// </para>
/// <para>
/// The model's name is the file's, because that is what the tab strip shows and what a save suggests. A model with no
/// name would open as "Untitled" while the user is looking at a file they just picked.
/// </para>
/// </remarks>
internal static class ReferenceImport
{
    /// <summary>Imports <paramref name="text"/> as a new document of <paramref name="language"/>.</summary>
    /// <param name="language">The language that claims the file.</param>
    /// <param name="name">What the document is called - the file's name without its extension.</param>
    /// <param name="text">The whole file, read by the host.</param>
    internal static ImportOutcome Import(ReferenceLanguage language, string name, string text)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);

        var model = new Project(language.Concepts).NewModel(name);

        return new ImportOutcome(language, model, language.Importer.Import(model, text));
    }
}
