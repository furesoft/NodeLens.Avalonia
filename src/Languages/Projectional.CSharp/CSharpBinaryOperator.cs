namespace Projectional.CSharp;

/// <summary>The operators a <c>Binary</c> node may carry, as the symbols C# writes.</summary>
/// <remarks>
/// Arithmetic, comparison and the two short-circuiting logical operators: the subset is what a behaviour body or a
/// generator condition needs, and every entry is one the generator emits unchanged. A missing operator is a missing
/// concept rather than a missing string, so this list is the whole of what the language can say.
/// </remarks>
public static class CSharpBinaryOperator
{
    /// <summary>Addition.</summary>
    public const string Add = "+";

    /// <summary>Subtraction.</summary>
    public const string Subtract = "-";

    /// <summary>Multiplication.</summary>
    public const string Multiply = "*";

    /// <summary>Division.</summary>
    public const string Divide = "/";

    /// <summary>Equality.</summary>
    public const string Equal = "==";

    /// <summary>Inequality.</summary>
    public const string NotEqual = "!=";

    /// <summary>Less than.</summary>
    public const string LessThan = "<";

    /// <summary>Greater than.</summary>
    public const string GreaterThan = ">";

    /// <summary>Logical and, short-circuiting.</summary>
    public const string And = "&&";

    /// <summary>Logical or, short-circuiting.</summary>
    public const string Or = "||";

    /// <summary>Every operator the property may hold, in the order the completion list offers them.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Add, Subtract, Multiply, Divide, Equal, NotEqual, LessThan, GreaterThan, And, Or];
}
