using Projectional.Core;

namespace Projectional.Language;

/// <summary>Turns a string a host supplies into nodes of one language, once.</summary>
/// <remarks>
/// <para>
/// Import is the only place a parser exists, and it runs once: the string becomes a model and is then thrown away,
/// because the model is the source of truth ([ADR 0002](../../../docs/adr/0002-model-as-source-of-truth.md)). Nothing
/// here writes text back, so there is still no text buffer and no parser in the editing path.
/// </para>
/// <para>
/// An importer fills a model the host already created: the framework does not know whether the user is importing into
/// the current document, a new tab or a scratch model. The whole import is one transaction, so it is one undo step and a
/// failure leaves the model exactly as it was.
/// </para>
/// <para>
/// The two outcomes are in <see cref="ImportResult"/>: a refusal with one error and no roots, or the roots plus whatever
/// was worth reporting. A construct the language cannot model is kept as raw text and reported as a warning - never
/// dropped, because an editor that silently discards what it does not understand rewrites the user's file.
/// </para>
/// </remarks>
public interface ITextImporter
{
    /// <summary>The name of the format, such as <c>json</c>, for a host's file filter.</summary>
    string Format { get; }

    /// <summary>Whether <paramref name="text"/> looks like this format at all.</summary>
    /// <remarks>
    /// A hint for a file dialog, never a promise: it is a cheap shape check, and the answer that counts is
    /// <see cref="Import"/>. A host that guessed the format from an extension can skip this; one that shows a list of
    /// candidates can ask it.
    /// </remarks>
    /// <param name="text">The text a host read.</param>
    bool CanImport(string text);

    /// <summary>Imports <paramref name="text"/> into <paramref name="model"/> as one undoable step.</summary>
    /// <param name="model">The model the host created; it is filled, never replaced.</param>
    /// <param name="text">The whole text, read by the host.</param>
    /// <returns>The roots that were added, or the error that refused the text.</returns>
    ImportResult Import(Model model, string text);
}
