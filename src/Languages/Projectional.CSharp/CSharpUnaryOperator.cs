namespace Projectional.CSharp;

/// <summary>The operators that take one operand, as the symbols C# writes.</summary>
/// <remarks>
/// The four that turn a value into another value. Increment and decrement are absent on purpose: they are statements
/// as much as expressions, and a language that offers them owes the reader an answer about which of the two a use is -
/// that belongs with the statement forms, not with the operators.
/// </remarks>
public static class CSharpUnaryOperator
{
    /// <summary>Logical negation, which turns a truth value into the other one.</summary>
    public const string Not = "!";

    /// <summary>Arithmetic negation.</summary>
    public const string Negate = "-";

    /// <summary>A unary plus, which is a no-op that a reader sometimes wants to write.</summary>
    public const string Plus = "+";

    /// <summary>Bitwise complement.</summary>
    public const string Complement = "~";

    /// <summary>Every unary operator the property may hold, in the order the completion list offers them.</summary>
    public static IReadOnlyList<string> All { get; } = [Not, Negate, Plus, Complement];
}
