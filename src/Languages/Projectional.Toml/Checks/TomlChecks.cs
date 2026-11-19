using Projectional.Checks;

namespace Projectional.Toml.Checks;

/// <summary>The rules TOML states about itself, as the set a host runs for a document of that language.</summary>
/// <remarks>
/// Every finding carries its repair, which is what a check is: a language package states what a document may be, this
/// package states what is wrong with one and how to put it right. A host composes it with the standard catalogue
/// (<c>StandardChecks.All</c>) the way any language's checks are composed.
/// </remarks>
public static class TomlChecks
{
    /// <summary>Every check this language declares, in the order they run.</summary>
    public static CheckSet All { get; } = CheckSet.Empty
        .With(new TomlDuplicateKeyCheck())
        .With(new TomlNumberCheck())
        .With(new TomlDateTimeCheck())
        .With(new TomlStringCheck());
}
