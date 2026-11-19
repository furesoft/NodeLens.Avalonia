namespace Projectional.Core;

/// <summary>How one word of a name is spelled inside a <see cref="NameCase"/>.</summary>
internal enum NameWordCase
{
    /// <summary>Every letter lower case, as in <c>my</c>.</summary>
    Lower,

    /// <summary>Every letter upper case, as in <c>MY</c>.</summary>
    Upper,

    /// <summary>The first letter upper case and the rest lower, as in <c>My</c>.</summary>
    Capitalized,
}