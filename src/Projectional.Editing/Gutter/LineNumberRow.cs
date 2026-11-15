using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// One numbered row of the document: the number, and the band of layout it sits beside.
/// </summary>
/// <remarks>
/// <para>
/// A projectional editor has no lines, so a row is not something the model knows about - it is what the
/// layout turned out to hold. <see cref="Bounds"/> is therefore the layout's own band for that row, wide
/// zero, because how wide a gutter is belongs to whoever paints it and not to the geometry here.
/// </para>
/// <para>
/// The number is a position in the document, not an identity: editing above a row renumbers everything
/// below it, exactly like a text editor. Nothing may cache a number across a rebuild.
/// </para>
/// </remarks>
/// <param name="Number">The row's number, counting from one.</param>
/// <param name="Bounds">The band the row occupies, in layout coordinates.</param>
public readonly record struct LineNumberRow(int Number, LayoutRect Bounds);
