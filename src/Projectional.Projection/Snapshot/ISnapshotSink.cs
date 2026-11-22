namespace Projectional.Projection;

/// <summary>Where a snapshot's text goes: a character grid, or a list of classified runs.</summary>
/// <remarks>
/// The drawing routine is one, and this is what makes it more than one output. Keeping it one routine is the point:
/// a highlighted picture and a plain one that were produced by two walkers could disagree about a column, and the
/// picture is the thing a person compares against the editor.
/// </remarks>
internal interface ISnapshotSink
{
    /// <summary>Draws <paramref name="text"/> at <paramref name="row"/> and <paramref name="column"/>.</summary>
    void Write(int row, int column, string text, TextStyleKind kind);
}