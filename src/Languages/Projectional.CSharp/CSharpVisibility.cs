namespace Projectional.CSharp;

/// <summary>The values the <c>visibility</c> property holds, as the C# keywords that spell them.</summary>
/// <remarks>
/// They are constants rather than an enumeration in the CLR sense because a property value is text: a declared
/// language's enumeration is a set of allowed strings, and an intention that writes <c>public</c> writes this
/// constant. A host that needs the C# keyword gets it unchanged, which is the point of spelling them this way.
/// </remarks>
public static class CSharpVisibility
{
    /// <summary>Visible everywhere.</summary>
    public const string Public = "public";

    /// <summary>Visible inside the assembly.</summary>
    public const string Internal = "internal";

    /// <summary>Visible inside the type that declares it.</summary>
    public const string Private = "private";

    /// <summary>Visible to the type and to the types that derive from it.</summary>
    public const string Protected = "protected";

    /// <summary>Every value the property may hold, in the order the completion list offers them.</summary>
    public static IReadOnlyList<string> All { get; } = [Public, Internal, Private, Protected];
}
