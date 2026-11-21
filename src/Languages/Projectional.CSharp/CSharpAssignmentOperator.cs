namespace Projectional.CSharp;

/// <summary>The forms an assignment can take, as the symbols C# writes.</summary>
/// <remarks>
/// One concept with a property rather than five concepts, because every form is "the target receives a value computed
/// from it" - the operator says how. A language that made <c>+=</c> its own concept would have to repeat the target,
/// the value, the notation and the generator for each symbol it added.
/// </remarks>
public static class CSharpAssignmentOperator
{
    /// <summary>Plain assignment.</summary>
    public const string Assign = "=";

    /// <summary>Add and assign.</summary>
    public const string Add = "+=";

    /// <summary>Subtract and assign.</summary>
    public const string Subtract = "-=";

    /// <summary>Multiply and assign.</summary>
    public const string Multiply = "*=";

    /// <summary>Divide and assign.</summary>
    public const string Divide = "/=";

    /// <summary>Every form the property may hold, in the order the completion list offers them.</summary>
    public static IReadOnlyList<string> All { get; } = [Assign, Add, Subtract, Multiply, Divide];
}
