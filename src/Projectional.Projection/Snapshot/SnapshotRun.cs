namespace Projectional.Projection;

/// <summary>One piece of a snapshot: the text a cell drew, where it landed, and what it means.</summary>
/// <param name="Row">The row it starts on, counted from the top of the picture.</param>
/// <param name="Column">The column it starts at, counted from the left edge.</param>
/// <param name="Text">What was drawn, already cut to the width that was asked for.</param>
/// <param name="Kind">What the text is, which is what a palette turns into a colour.</param>
/// <remarks>
/// A run is the same information a snapshot's characters carry, plus the classification the painter needs. It exists
/// so that a caller which wants to colour a picture - a documentation page, an exporter - does not have to re-derive the
/// meaning from the text, and does not have to guess the layout either: the run is where the renderer put it.
/// </remarks>
public readonly record struct SnapshotRun(int Row, int Column, string Text, TextStyleKind Kind);