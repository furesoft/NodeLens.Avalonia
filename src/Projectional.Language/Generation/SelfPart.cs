namespace Projectional.Language;

/// <summary>The node's own generated text: its template, applied to itself.</summary>
/// <remarks>
/// Only interesting inside a <see cref="LoopPart"/> body, where "this node" is the child being visited rather than
/// the node the template was chosen for - it is how a loop says "the child's own text, here, among what I add".
/// A template that names itself outside such a body recurses for ever, which the engine stops with a depth limit
/// rather than a stack overflow.
/// </remarks>
public sealed record SelfPart : TemplatePart
{
    /// <summary>The one instance, because the part names nothing.</summary>
    public static SelfPart Instance { get; } = new();

    private SelfPart()
    {
    }

    public override string ToString() => "self";
}
