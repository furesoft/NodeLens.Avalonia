using Avalonia.Media;
using Projectional.Language;

namespace Projectional.Avalonia;

/// <summary>
/// What a shaped run is identified by: the text, the brush it was shaped in, and its emphasis.
/// </summary>
/// <remarks>
/// The brush is part of the key because a <see cref="FormattedText"/> carries the brush it was created
/// with, and one string can be on screen in several colours - the same word is a keyword in one place
/// and a name in another. Comparing brushes rather than colours keeps the key cheap, and the theme hands
/// out one instance per kind, so equal keys are the same instance.
/// <para>
/// The emphasis is part of the key for the same reason and with a sharper consequence: a
/// <see cref="FormattedText"/> also carries its typeface and its decorations, so the same word in bold
/// and in regular is two different measurements. Keying only on text and brush would hand the bold cell
/// the width of the regular one.
/// </para>
/// </remarks>
/// <param name="Text">The text that was shaped.</param>
/// <param name="Brush">The brush it was shaped in.</param>
/// <param name="Emphasis">How it is decorated.</param>
internal readonly record struct ShapedTextKey(string Text, IBrush Brush, TextEmphasis Emphasis);
