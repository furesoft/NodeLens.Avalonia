namespace Projectional.Editing;

/// <summary>
/// What a completion item stands for, which is what a host picks an icon by.
/// </summary>
/// <remarks>
/// The vocabulary belongs to the editing layer, not to a model: a host maps it onto its own pictures, so
/// nothing here names a glyph and nothing the model stores names a picture. <see cref="Node"/> is the
/// only member today because filling a slot is the only completion the editor offers; offering the
/// targets of a reference adds a member here rather than a second list type, so a host keeps one
/// lookup.
/// </remarks>
public enum CompletionKind
{
    /// <summary>A node of a concept, which is what filling a slot with it creates.</summary>
    Node,
}
