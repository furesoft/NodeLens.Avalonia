using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;

namespace Projectional.Workbench;

/// <summary>One shipped file format, as a language a document can be written in.</summary>
/// <remarks>
/// <para>
/// The shell knows two kinds of document: the languages it composes for itself (<see cref="WorkbenchLanguage"/>, which is
/// one set of concepts for the whole app) and the reference languages, of which each document may be a different one.
/// This is what the second kind needs to answer the four questions a tab asks - how is it drawn, what can it generate,
/// what does it offer, what is wrong with it - plus the importer that turns a file into a model of it.
/// </para>
/// <para>
/// A value rather than a set of static lookups, because the app has one of each and a host would have its own. The
/// registries are the language's own static ones, which is safe: notation and concept registries are immutable once
/// built, so two documents of one language share them the way they share the language's declarations.
/// </para>
/// </remarks>
/// <param name="Name">What a menu and a message call it, such as <c>JSON</c>.</param>
/// <param name="Format">The name its importer reports, such as <c>json</c>.</param>
/// <param name="Extension">The extension a file is expected to have, leading dot included.</param>
/// <param name="Importer">The importer that turns text into a model of this language.</param>
/// <param name="Concepts">The concepts a document of it is written in.</param>
/// <param name="Notations">How those concepts are drawn.</param>
/// <param name="Generators">What it can generate; empty when it declares none.</param>
/// <param name="Intentions">The entries it brings, or <see langword="null"/> when it has none of its own.</param>
/// <param name="Checks">The rules it states about itself, with a repair for every finding.</param>
internal sealed record ReferenceLanguage(
    string Name,
    string Format,
    string Extension,
    ITextImporter Importer,
    ConceptRegistry Concepts,
    NotationRegistry Notations,
    IReadOnlyList<TextGenerator> Generators,
    IIntentionProvider? Intentions,
    CheckSet Checks);
