using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>The provider a session starts with: it says nothing, so every slot is answered by its declared type.</summary>
/// <remarks>
/// Never <see langword="null"/> at the seam, so a host that installs nothing keeps the behaviour the layer had before
/// the seam existed - the same bargain <see cref="DefaultScopeProvider"/> makes for references.
/// </remarks>
public sealed class DefaultValueProvider : IValueProvider
{
    /// <summary>The one instance, which holds no state.</summary>
    public static DefaultValueProvider Instance { get; } = new();

    private DefaultValueProvider()
    {
    }

    /// <inheritdoc/>
    public IReadOnlyList<string>? ValuesOf(Node owner, string slot) => null;
}
