namespace Projectional.Avalonia;

/// <summary>Everything the shell asks the user, so a host can answer it its own way.</summary>
/// <remarks>
/// <para>
/// ADR 0015's rule: anything the shell asks the user goes through this seam. The methods speak <em>paths</em> and
/// <see cref="UnsavedChoice"/>, never an Avalonia type, which is what makes the interface implementable outside this
/// library - a host whose files live in a database can answer <see cref="ChooseFileToOpenAsync"/> with a key of its
/// own and map it back when the shell reads the document.
/// </para>
/// <para>
/// Every chooser answers <see langword="null"/> for "the user declined", because a cancelled dialog and a dismissed one
/// are the same decision to a caller and inventing two values would make every call site test both.
/// </para>
/// <para>
/// The shell asks these questions and nothing else, so the list is short on purpose. A host that needs to ask something
/// of its own asks it itself: this is the seam for the shell's questions, not a general dialog service.
/// </para>
/// </remarks>
public interface IShellDialogs
{
    /// <summary>Asks for a folder, as a path the shell's storage can use.</summary>
    /// <param name="title">What the question is about, in the user's language.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user declined.</returns>
    Task<string?> ChooseFolderAsync(string title);

    /// <summary>Asks for one existing file.</summary>
    /// <param name="title">What the question is about, in the user's language.</param>
    /// <param name="type">The kind of file offered, which is a filter and never a refusal.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user declined.</returns>
    Task<string?> ChooseFileToOpenAsync(string title, ShellFileType type);

    /// <summary>Asks where a file should be written, offering a name.</summary>
    /// <param name="title">What the question is about, in the user's language.</param>
    /// <param name="suggestedName">The name to offer first, usually the document's title.</param>
    /// <param name="type">The kind of file offered.</param>
    /// <param name="defaultExtension">The extension to append when the user types none, without its dot.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user declined.</returns>
    Task<string?> ChooseFileToSaveAsync(
        string title,
        string suggestedName,
        ShellFileType type,
        string? defaultExtension = null);

    /// <summary>Asks what to do about a document with changes that are not on disk.</summary>
    /// <param name="title">The document's title, which is what the question names.</param>
    /// <returns>What the user chose; <see cref="UnsavedChoice.Cancel"/> when the question was dismissed.</returns>
    Task<UnsavedChoice> AskUnsavedAsync(string title);
}
