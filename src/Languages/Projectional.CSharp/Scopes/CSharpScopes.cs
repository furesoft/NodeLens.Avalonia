using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.CSharp.Scopes;

/// <summary>Which declarations a name in C# can see.</summary>
/// <remarks>
/// <para>
/// One reference slot and one place, because C# resolution is one rule: a name sees the declarations written in the
/// nodes that enclose it, nearest first. A parameter is written in the method that encloses the body, a local in the
/// block that encloses the use, a field in the class that encloses both - and <see cref="ScopePlace.Ancestors"/>
/// walks exactly that chain, taking the named nodes at each level before the level above it. So the nearest
/// declaration wins without a second rule for shadowing.
/// </para>
/// <para>
/// The target is the interface rather than a list of three concepts, so a language that adds a fourth kind of
/// declaration makes it visible by implementing <see cref="CSharpConceptKeys.NamedDeclaration"/> - the reference and
/// the scope do not have to learn about it.
/// </para>
/// <para>
/// What this does not say is the order of a local's visibility inside one block: a local declared after the use is
/// still in the same block and therefore visible here. C#'s "definitely assigned" rule is a check about a program's
/// execution, not about what a name can mean, and reporting it belongs in a constraint once the type system can see
/// that the two names are of the same kind.
/// </para>
/// </remarks>
public static class CSharpScopes
{
    /// <summary>The scopes the base language declares.</summary>
    public static ScopeProviderRegistry Registry { get; } = ScopeProviderRegistry.Empty
        .Add(
            CSharpConceptKeys.Name,
            NamedScopeProvider.For(
                CSharpConceptKeys.TargetSlot,
                ScopePlace.Ancestors,
                CSharpConceptKeys.NamedDeclaration))

        // A class extends another class and implements interfaces, and both are written where the class is - so the
        // same place answers them, narrowed to the concept each slot may hold.
        .Add(
            CSharpConceptKeys.InheritanceEntry,
            new NamedScopeProvider(
                new Dictionary<string, ScopeBinding>(StringComparer.Ordinal)
                {
                    [CSharpConceptKeys.TargetSlot] = new(ScopePlace.Ancestors, CSharpConceptKeys.TypeDeclaration),
                }));
}
