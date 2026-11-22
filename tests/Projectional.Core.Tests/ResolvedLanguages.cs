using Projectional.Languages;

namespace Projectional.Core.Tests;

/// <summary>The language projects a test hands to <see cref="LanguageProject.Open"/> as its resolver.</summary>
/// <remarks>
/// A resolver is a host's answer to "where does this language come from". A test that needs one dependency to resolve
/// should say so in a line rather than build a shell around it.
/// </remarks>
internal sealed class ResolvedLanguages : ILanguageResolver
{
    private readonly IReadOnlyList<LanguageProject> _languages;

    internal ResolvedLanguages(params LanguageProject[] languages) => _languages = languages;

    public LanguageProject? Find(string name) =>
        _languages.FirstOrDefault(language => string.Equals(language.Name, name, StringComparison.Ordinal));
}
