using Projectional.Language;

namespace Projectional.Screenshots;

/// <summary>A language that claims no keystroke, so typing into an empty slot stays unchanged.</summary>
internal sealed class NoTyping : ITypingRules
{
    public static NoTyping Instance { get; } = new();

    /// <inheritdoc />
    public TypingRule? ClaimFor(string conceptKey, string text) => null;
}
