using Avalonia.Media;

namespace Projectional.Avalonia;

/// <summary>
/// What a shaped run is identified by: the text and the brush it was shaped in.
/// </summary>
/// <remarks>
/// The brush is part of the key because a <see cref="FormattedText"/> carries the brush it was created
/// with, and one string can be on screen in several colours - the same word is a keyword in one place
/// and a name in another. Comparing brushes rather than colours keeps the key cheap, and the theme hands
/// out one instance per kind, so equal keys are the same instance.
/// </remarks>
/// <param name="Text">The text that was shaped.</param>
/// <param name="Brush">The brush it was shaped in.</param>
internal readonly record struct ShapedTextKey(string Text, IBrush Brush);
