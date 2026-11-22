namespace Projectional.LanguageExport;

/// <summary>One file a caller wants in the archive besides what the export produced.</summary>
/// <param name="Name">The entry's name, with forward slashes and any folder prefix it needs.</param>
/// <param name="Bytes">What to write.</param>
/// <remarks>
/// A seam rather than a dependency: a package carries more than the generated C# - documentation, a licence, a sample -
/// and each of those is somebody else's product. The export layer takes the bytes and asks nothing about where they came
/// from, so adding a product does not add an arrow between two packages that must not know each other.
/// </remarks>
public sealed record PackageFile(string Name, byte[] Bytes);