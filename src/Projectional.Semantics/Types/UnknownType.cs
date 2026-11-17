namespace Projectional.Semantics;

/// <summary>
/// The type of something whose type could not be computed.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="UnknownType"/> is a first-class type rather than a <see langword="null"/> result, and the
/// difference matters: a null would make every caller write a null check and every one of them would
/// answer differently. As a type it accepts everything and is accepted by everything, which is what
/// suppresses follow-on errors - one mistake in a condition should not produce fifty findings about the
/// expressions that read it.
/// </para>
/// <para>
/// It is therefore <em>not</em> a wildcard to be relied on. A caller that must not proceed on a guess - a
/// generator, a type-directed refactoring - asks <c>type is UnknownType</c> explicitly. That is the
/// "cannot leak into generation unnoticed" rule: the type does not stop the caller, it makes the caller
/// say what it wants to do about not knowing.
/// </para>
/// </remarks>
public sealed class UnknownType : IType
{
    private UnknownType()
    {
    }

    /// <summary>The one instance, since the type holds no state.</summary>
    public static UnknownType Instance { get; } = new();

    /// <inheritdoc />
    public string Name => "unknown";

    /// <inheritdoc />
    public bool Accepts(IType actual) => true;

    public override string ToString() => Name;
}
