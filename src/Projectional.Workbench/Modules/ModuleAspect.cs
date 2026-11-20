namespace Projectional.Workbench;

/// <summary>One aspect a language module can have: the folder it lives in, the language it is written in, and how much is in it.</summary>
/// <param name="Name">The folder's name on disk, which is also the aspect's name.</param>
/// <param name="Language">The language a document of this folder is written in.</param>
/// <param name="Documents">How many definition documents the folder holds, zero when it does not exist.</param>
/// <remarks>
/// A row of the settings dialog rather than of the explorer: the explorer lists the folders that exist, while a module's
/// settings have to answer for the ones that do not - "no constraints" is information about the module, and a list that
/// simply left the row out could not say it.
/// </remarks>
internal sealed record ModuleAspect(string Name, string Language, int Documents)
{
    /// <summary>Whether the module has this aspect at all.</summary>
    internal bool IsPresent => Documents > 0;
}
