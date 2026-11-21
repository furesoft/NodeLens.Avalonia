namespace Projectional.CSharp;

/// <summary>The operators that take one operand, as the symbols C# writes.</summary>
/// <remarks>
/// They turn a value into another value. Increment and decrement are here as *expressions*, which is the form a `for`
/// header needs: `i++` produces the old value and has no semicolon, so it can stand between two of them. The statement
/// form is what a body would use, and that is a later addition - offering one concept for both would leave the reader
/// guessing which of the two a use is.
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

    /// <summary>Increment, as an expression: the value, then the addition.</summary>
    public const string Increment = "++";

    /// <summary>Decrement, as an expression.</summary>
    public const string Decrement = "--";

    /// <summary>Every unary operator the property may hold, in the order the completion list offers them.</summary>
    public static IReadOnlyList<string> All { get; } = [Not, Negate, Plus, Complement, Increment, Decrement];
}
