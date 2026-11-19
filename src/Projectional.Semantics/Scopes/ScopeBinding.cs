namespace Projectional.Semantics;

/// <summary>Where one reference slot's names come from: a place in the document, and optionally one concept.</summary>
/// <remarks>
/// <para>
/// The pair is the whole of what a language can say about a slot beyond the default chain, and it is two facts rather
/// than one: <see cref="Place"/> says where the names are written, <see cref="Target"/> says which of them are
/// visible. A language that names no target accepts every named node of the place, which is what makes "everywhere,
/// no filter" the default chain.
/// </para>
/// <para>
/// A record struct because a binding is a value a language declares and a test compares; the target concept is a key
/// rather than a <c>Concept</c> because a binding outlives the registry it was written against.
/// </para>
/// </remarks>
public readonly record struct ScopeBinding(ScopePlace Place, string? Target = null)
{
    public override string ToString() => Target is null ? Place.ToString() : $"{Place} of {Target}";
}
