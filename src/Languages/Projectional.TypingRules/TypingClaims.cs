namespace Projectional.TypingRules;

/// <summary>The four ways a declared typing rule can recognise the text a user starts a node with.</summary>
/// <remarks>
/// <para>
/// The runtime contract is a predicate (<see cref="Projectional.Language.TypingRule"/> holds a
/// <c>Func&lt;string, bool&gt;</c>), and a model cannot hold a function. These four are what a model can say instead,
/// and they are the whole vocabulary on purpose: they are exactly the predicates the shipped languages write by hand,
/// each one taken from a language that needed it - digits from a literal's value, a name from a name slot, a prefix
/// from a string literal's opening quote, and a word list from <c>true</c>/<c>false</c>.
/// </para>
/// <para>
/// A general expression would turn the aspect into an interpreter, and a fifth symbol nobody uses would be a
/// vocabulary that grows before a language needs it. When a language needs a predicate these four cannot say, the
/// answer is a new symbol here with the language that needed it, not an escape hatch.
/// </para>
/// </remarks>
public static class TypingClaims
{
    /// <summary>Every character is a digit - how a numeric literal claims its value.</summary>
    public const string Digits = "digits";

    /// <summary>A letter or an underscore, then letters, digits and underscores - how a name claims its name.</summary>
    public const string Name = "name";

    /// <summary>The text starts with <c>text</c> - how a quoted literal claims its opening quote.</summary>
    public const string Prefix = "starts with";

    /// <summary>The text is one of the words in <c>text</c>, separated by spaces - how a boolean claims its keyword.</summary>
    public const string AnyOf = "any of";

    /// <summary>
    /// The text is the concept's own <c>alias</c>, which is the word completion offers and a user therefore types.
    /// </summary>
    /// <remarks>
    /// The one claim that names no text of its own: the word lives on the concept declaration, so the rule says "the
    /// alias" and the concept says which. That is MPS' "an instance created from a typed alias" - the proposal list and
    /// the keystroke agree because both read the same declaration, instead of the author spelling the word twice and
    /// one of the two going stale.
    /// </remarks>
    public const string Alias = "alias";
}
