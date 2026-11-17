using Projectional.Core;
using Projectional.Language;

namespace Projectional.Semantics;

/// <summary>
/// Reports a reference whose name no longer identifies the element it points at, because more than one
/// element in its scope carries that name.
/// </summary>
/// <remarks>
/// <para>
/// This is the finding that closes the scope model. Ambiguity is a value there - a typed name is refused
/// with a reason and a completion list shows both - so a document cannot be *made* ambiguous by typing.
/// What it can do is become ambiguous afterwards: rename one thing and a reference elsewhere now shares its
/// name with something else. That reference still draws as resolved, because it points at a node id, and
/// nothing says anything about it. The name is the fallback that survives a copy, a merge or a file edited
/// elsewhere, and it no longer identifies one element.
/// </para>
/// <para>
/// It is declared for every node rather than per concept (<see cref="ConstraintSet.Everywhere"/>), because
/// a reference slot is not a property of some family of concepts: any concept may declare one, and a rule
/// that named a root would miss the concepts that do not inherit from it.
/// </para>
/// <para>
/// Two deliberate silences. An <em>unset</em> reference has no name to resolve, and an <em>unresolved</em>
/// one is already visibly wrong in the drawing (<c>&lt;unresolved&gt;</c>), while the text it fell back to
/// may be stale - reporting ambiguity there would blame the wrong problem. And the name that is resolved is
/// the target's <em>current</em> name, not the <c>resolveInfo</c> it was written with: the stored text is
/// not rewritten when a target is renamed, so resolving it would report names no user ever sees.
/// </para>
/// </remarks>
public sealed class AmbiguousReferenceConstraint : IConstraint
{
    /// <summary>The identifier this rule reports under.</summary>
    public const string Code = "scopes.reference.ambiguous";

    /// <inheritdoc />
    public void Check(ConstraintContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var node = context.Node;

        // The flat slot set, so a reference inherited from a super concept is checked too.
        foreach (var slot in node.Concept.Slots)
        {
            if (slot is not ReferenceSlot reference
                || node.GetReferenceTarget(reference.Name) is not { } target
                || target.Concept.NameSlot is not { } nameSlot
                || target.GetOrNull(nameSlot.Name) is not { Length: > 0 } name)
            {
                continue;
            }

            var resolution = context.ScopeOf(reference.Name).Resolve(name);

            if (resolution.Kind == ScopeResolutionKind.Ambiguous)
            {
                context.Warning(
                    $"\"{name}\" names {resolution.Candidates.Count} elements here, so this reference no longer identifies one of them.",
                    role: reference.Name,
                    code: Code);
            }
        }
    }
}
