namespace Projectional.Language;

/// <summary>A line break, followed by the indentation the current level asks for.</summary>
/// <remarks>
/// The only part that emits whitespace, and it emits the indentation rather than the template: that is what makes
/// the drawing of a generated block a property of the template's structure instead of a count of spaces somebody
/// typed. A generated file therefore ends without a trailing newline unless its template asks for one - the engine
/// adds nothing of its own.
/// </remarks>
public sealed record LineBreakPart : TemplatePart
{
    /// <summary>The one instance, because a line break has no state.</summary>
    public static LineBreakPart Instance { get; } = new();

    private LineBreakPart()
    {
    }

    public override string ToString() => "line break";
}
