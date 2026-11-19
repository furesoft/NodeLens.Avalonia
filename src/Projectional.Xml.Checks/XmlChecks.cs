using Projectional.Checks;

namespace Projectional.Xml.Checks;

/// <summary>The rules XML states about itself, as the set a host runs for a document of that language.</summary>
/// <remarks>
/// Every finding carries its repair, which is what a check is: the language package says what a document may be, this
/// package says what is wrong with one and how to put it right. A host composes it with the standard catalogue the way
/// any language's checks are composed.
/// </remarks>
public static class XmlChecks
{
    /// <summary>Every check this language declares, in the order they run.</summary>
    public static CheckSet All { get; } = CheckSet.Empty
        .With(new XmlSpellingCheck())
        .With(new XmlCommentCheck());
}
