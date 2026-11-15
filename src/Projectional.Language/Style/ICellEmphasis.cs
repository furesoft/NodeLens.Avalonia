using Projectional.Core;

namespace Projectional.Language;

/// <summary>
/// Answers, while a model is being projected, how a node and everything under it is decorated.
/// </summary>
/// <remarks>
/// <para>
/// A notation says what a language always looks like. This says what a particular model looks like
/// today - a rule that failed its check is struck through, a deprecated entity is italic - and it is
/// asked fresh every time the projection is built, so an edit that changes the model changes the
/// decoration on the next rebuild with nothing else to invalidate.
/// </para>
/// <para>
/// It lives beside the notation because it is the same kind of statement: how this language is read.
/// It returns emphasis rather than colour because emphasis is part of the geometry - a bold or italic
/// face is wider than the regular one, and the layout has to know before it hands out rectangles. A
/// colour, by contrast, is applied while painting and cannot move anything.
/// </para>
/// <para>
/// What a resolver returns is inherited: it applies to the node's own cells and to every cell below
/// it, so "this rule is rejected" covers the rule's keywords, its condition and whatever that
/// condition is made of, without the resolver having to walk the subtree itself. Emphasis declared by
/// a notation is added on top, which means a resolver can point at something without having to
/// restate how the language already draws it.
/// </para>
/// </remarks>
public interface ICellEmphasis
{
    /// <summary>Returns the decoration this resolver wants for <paramref name="node"/>.</summary>
    /// <param name="node">The node whose subtree is being projected.</param>
    /// <returns>The emphasis to add, or <see cref="TextEmphasis.None"/> to add nothing.</returns>
    TextEmphasis EmphasisOf(Node node);
}
