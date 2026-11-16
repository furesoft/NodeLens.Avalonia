using Projectional.Core;
using Projectional.Language;

namespace Projectional.Editing;

/// <summary>
/// One node a keystroke could grow from, and which end of it the caret stands at.
/// </summary>
/// <remarks>
/// A pair rather than a node, because the side is what decides which slot of the wrapper the node lands in -
/// and because a node drawn as a single cell has the caret at both ends at once, which
/// <see cref="TriggerAnchors.Of"/> reports as two candidates in the order they should be tried.
/// </remarks>
internal readonly record struct TriggerAnchor(Node Node, TriggerSide Side);
