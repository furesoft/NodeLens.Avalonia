namespace Projectional.Editing;

/// <summary>
/// What a completion item stands for, which is what a host picks an icon by.
/// </summary>
/// <remarks>
/// The vocabulary belongs to the editing layer, not to a model: a host maps it onto its own pictures, so
/// nothing here names a glyph and nothing the model stores names a picture. <see cref="Node"/> and
/// <see cref="Value"/> are the two members the editor produces today; offering the targets of a reference
/// adds a member here rather than a second list type, so a host keeps one lookup.
/// </remarks>
public enum CompletionKind
{
    /// <summary>A node of a concept, which is what filling a slot with it creates.</summary>
    Node,

    /// <summary>
    /// One legal value of a property, which is what accepting it writes into the cell.
    /// </summary>
    /// <remarks>
    /// A value is not a node: accepting one replaces text that is already there instead of filling an empty
    /// slot, so a host that draws nodes and values alike would be hiding the one difference a user needs -
    /// that a value is a choice among the words this slot allows, not something new.
    /// </remarks>
    Value,
}
