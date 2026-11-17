namespace Projectional.Semantics;

/// <summary>What one run of a generator produced: the text and its traceability map, or why there is none.</summary>
/// <remarks>
/// The text is present even when the run failed, because half a file plus the node it stopped at is a better answer
/// than nothing: a host can show what was generated up to the mistake. <see cref="Succeeded"/> is what a caller that
/// must not write a partial file asks.
/// </remarks>
public sealed class GeneratedText
{
    internal GeneratedText(string text, IReadOnlyList<GeneratedRegion> regions, GenerationFailure? failure)
    {
        Text = text;
        Regions = regions;
        Failure = failure;
    }

    /// <summary>What was generated, up to the failure when there is one.</summary>
    public string Text { get; }

    /// <summary>Where each stretch of <see cref="Text"/> came from.</summary>
    public IReadOnlyList<GeneratedRegion> Regions { get; }

    /// <summary>Why the run stopped without finishing, or <see langword="null"/> when it did.</summary>
    public GenerationFailure? Failure { get; }

    /// <summary>Whether the whole model was generated.</summary>
    public bool Succeeded => Failure is null;

    /// <summary>The node that produced the text at <paramref name="offset"/>, or <see langword="null"/>.</summary>
    /// <param name="offset">A zero-based offset into <see cref="Text"/>.</param>
    /// <remarks>
    /// A first-match scan rather than an index: a region is a stretch of text, the runs are small, and a generator
    /// that had to build a per-character map would be paying for a lookup nobody asked for.
    /// </remarks>
    public GeneratedRegion? RegionAt(int offset)
    {
        foreach (var region in Regions)
        {
            if (region.Covers(offset))
            {
                return region;
            }
        }

        return null;
    }

    public override string ToString() =>
        Succeeded ? $"{Text.Length} character(s), {Regions.Count} region(s)" : Failure!.Message;
}
