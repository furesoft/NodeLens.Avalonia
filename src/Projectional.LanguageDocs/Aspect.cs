namespace Projectional.LanguageDocs;

/// <summary>One aspect of a language, as the documentation gives it a page of its own.</summary>
/// <remarks>
/// The names are the aspect folders' own words, pluralised where the folder is singular: <c>generator/</c> becomes
/// "Generators" because a language has several, and <c>import/</c> becomes "Importers" because that is what the folder
/// holds. Enumerations are here although the framework has no folder for them - a declaration that lowering resolves into
/// a property type is still a part of the language a reader needs a page for.
/// </remarks>
internal enum Aspect
{
    /// <summary>The enumerations the language's property slots are declared over.</summary>
    Enumerations,

    /// <summary>What draws each concept.</summary>
    Notations,

    /// <summary>The type rules, and the subtype, comparison and replacement relations.</summary>
    Types,

    /// <summary>What a burst of typing creates.</summary>
    Typing,

    /// <summary>The constraints, per concept.</summary>
    Constraints,

    /// <summary>Which concepts answer references, and for which slots.</summary>
    Scopes,

    /// <summary>Whether intentions are offered at all.</summary>
    Intentions,

    /// <summary>The generators, with the template each concept is written out by.</summary>
    Generators,

    /// <summary>The text formats the language can read.</summary>
    Importers,

    /// <summary>The documents that are models of the language itself.</summary>
    Accessory,

    /// <summary>The findings a document of the language is checked for.</summary>
    /// <remarks>
    /// The one aspect with no folder: a check is framework code rather than a declaration, so a language takes them from
    /// the catalogue instead of stating them. The page exists anyway, because "what will be reported about my document"
    /// is a question about the language whether or not its author wrote the answer.
    /// </remarks>
    Checks,

    /// <summary>What each concept evaluates to.</summary>
    /// <remarks>
    /// The second aspect with no folder, and for a stronger reason than checks: the others are languages a project writes
    /// in a folder, while a rule about meaning is a computation and no model for one exists here.
    /// </remarks>
    Evaluation,
}

/// <summary>Where each aspect's page lives, what it is called, and what a language has in it.</summary>
internal static class AspectNames
{
    /// <summary>The file an aspect's page is written to, inside the language's folder.</summary>
    internal static string File(Aspect aspect) => aspect switch
    {
        Aspect.Enumerations => "enumerations.html",
        Aspect.Notations => "notations.html",
        Aspect.Types => "types.html",
        Aspect.Typing => "typing.html",
        Aspect.Constraints => "constraints.html",
        Aspect.Scopes => "scopes.html",
        Aspect.Intentions => "intentions.html",
        Aspect.Generators => "generators.html",
        Aspect.Importers => "importers.html",
        Aspect.Accessory => "accessory.html",
        Aspect.Checks => "checks.html",
        Aspect.Evaluation => "evaluation.html",
        _ => throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "No page for that aspect."),
    };

    /// <summary>The heading an aspect's page and its navigation entry carry.</summary>
    internal static string Title(Aspect aspect) => aspect switch
    {
        Aspect.Enumerations => "Enumerations",
        Aspect.Notations => "Notations",
        Aspect.Types => "Types",
        Aspect.Typing => "Typing",
        Aspect.Constraints => "Constraints",
        Aspect.Scopes => "Scopes",
        Aspect.Intentions => "Intentions",
        Aspect.Generators => "Generators",
        Aspect.Importers => "Importers",
        Aspect.Accessory => "Accessory",
        Aspect.Checks => "Checks",
        Aspect.Evaluation => "Evaluation",
        _ => throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "No title for that aspect."),
    };


}