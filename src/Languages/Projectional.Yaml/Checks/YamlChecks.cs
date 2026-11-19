using Projectional.Checks;

namespace Projectional.Yaml.Checks;

/// <summary>The rules YAML states about itself, as the set a host runs for a document of that language.</summary>
/// <remarks>
/// Two rules, and both are about a value that no longer reads back: a placement a block collection needs, and a scalar
/// whose spelling stopped being one. There is deliberately no duplicate-key check - YAML permits a repeated key as much as
/// HOCON does, and dropping one would rewrite the file.
/// </remarks>
public static class YamlChecks
{
    /// <summary>Every check this language declares, in the order they run.</summary>
    public static CheckSet All { get; } = CheckSet.Empty
        .With(new YamlPlacementCheck())
        .With(new YamlScalarCheck());
}
