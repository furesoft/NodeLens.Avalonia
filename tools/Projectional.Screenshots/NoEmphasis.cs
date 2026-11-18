using Projectional.Core;
using Projectional.Language;

namespace Projectional.Screenshots;

/// <summary>A language that decorates nothing, which is what most of them do.</summary>
internal sealed class NoEmphasis : ICellEmphasis
{
    public static NoEmphasis Instance { get; } = new();

    /// <inheritdoc />
    public TextEmphasis EmphasisOf(Node node) => TextEmphasis.None;
}
