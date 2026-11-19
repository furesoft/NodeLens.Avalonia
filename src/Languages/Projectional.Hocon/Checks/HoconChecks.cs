using Projectional.Checks;

namespace Projectional.Hocon.Checks;

/// <summary>The rules HOCON states about itself, as the set a host runs for a document of that language.</summary>
/// <remarks>
/// Two rules and no duplicate-key check: HOCON lets one object name a key twice on purpose, because that is how a file
/// overrides a value it inherited. A reference language does not get to forbid what its format calls a feature - the
/// mirror image of the TOML decision, and the reason these rules live in the language that knows the difference.
/// </remarks>
public static class HoconChecks
{
    /// <summary>Every check this language declares, in the order they run.</summary>
    public static CheckSet All { get; } = CheckSet.Empty
        .With(new HoconUnquotedCheck())
        .With(new HoconNumberCheck());
}
