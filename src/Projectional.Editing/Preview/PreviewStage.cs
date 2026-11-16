namespace Projectional.Editing;

/// <summary>
/// How far down the hover ladder an entry has travelled: what the host should be showing right now.
/// </summary>
/// <remarks>
/// Three rungs, and the first one is not here on purpose. A highlighted entry is the list's own hover state, which
/// a popup already knows how to draw; what needs a clock is the two rungs that cost something - the description,
/// which is a tooltip, and the preview, which is a whole document projected instead of the page.
/// </remarks>
public enum PreviewStage
{
    /// <summary>Nothing extra is shown: the entry is highlighted and nothing else.</summary>
    Nothing,

    /// <summary>The entry's description is shown as a tooltip.</summary>
    Tooltip,

    /// <summary>The preview replaces the page.</summary>
    Preview,
}
