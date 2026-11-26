using Avalonia.Media;

using Projectional.Language;

namespace Projectional.Avalonia;

/// <summary>
/// The two brushes a <see cref="PaintRole"/> is painted with: one for the region's fill and one for its border.
/// </summary>
/// <remarks>
/// Two brushes rather than one because a border is a line and a fill is a surface, and the same colour reads as
/// neither when it is used for both - a light surface needs a darker line to be visible at all. Keeping the pair
/// together means a palette author decides both at once and cannot end up with a border that disappears into the
/// fill it outlines.
/// </remarks>
/// <param name="Role">What the region means.</param>
/// <param name="Fill">What fills the region.</param>
/// <param name="Border">What draws its outline.</param>
public readonly record struct PaintBrushes(PaintRole Role, IBrush Fill, IBrush Border);
