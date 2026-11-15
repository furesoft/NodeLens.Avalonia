namespace Projectional.Language;

/// <summary>
/// What a language says about text that arrives while the caret is in an empty slot.
/// </summary>
/// <remarks>
/// <para>
/// Typing into an empty slot is ambiguous: the slot names a set of concepts and the characters say
/// which one the user meant, but nothing in the model maps one to the other. This interface is that
/// mapping, declared by the language and asked by the editor, so that typing a digit into an empty
/// expression slot produces an integer literal instead of being refused.
/// </para>
/// <para>
/// It lives beside the notation rather than in the editing layer for the same reason
/// <see cref="ICellEmphasis"/> does: it is a statement about how this language is written, and a
/// language package has to be able to declare it without referencing the editor. The editor walks the
/// concepts the slot can hold in declaration order and creates the first one that claims the text, so
/// that order is the language author's answer to "what did the user most likely mean" - the same
/// meaning <see cref="Projectional.Core.ConceptRegistry.DefaultInstantiableFor"/> gives it.
/// </para>
/// </remarks>
public interface ITypingRules
{
    /// <summary>The rule of <paramref name="conceptKey"/> that claims <paramref name="text"/>, if any.</summary>
    /// <param name="conceptKey">A concept the empty slot can hold.</param>
    /// <param name="text">The characters the host typed.</param>
    /// <returns>
    /// The rule to apply, or <see langword="null"/> when this concept does not claim the text - which is
    /// also the answer for a concept that declares no rule at all.
    /// </returns>
    TypingRule? ClaimFor(string conceptKey, string text);
}
