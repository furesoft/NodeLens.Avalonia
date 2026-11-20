using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>A type that implements nothing but the name, which is what a language author may now do.</summary>
/// <remarks>
/// It exists to exercise the default <see cref="IType.Accepts"/> on the interface itself: every type the framework
/// ships overrides that method for a reason of its own (the primitives are lenient about
/// <see cref="UnknownType"/>, a named type looks through its expansion, a structure compares its parts), so none of
/// them can show what a type that says nothing extra gets.
/// </remarks>
/// <param name="Name">How the type is written.</param>
internal sealed class BareType(string name) : IType
{
    /// <inheritdoc />
    public string Name { get; } = name;

    public override string ToString() => Name;
}
