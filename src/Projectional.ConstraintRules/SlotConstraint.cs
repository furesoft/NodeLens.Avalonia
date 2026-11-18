using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.ConstraintRules;

/// <summary>
/// A declared rule reduced to one question about a slot, which is what every body kind in this language has in
/// common: where to look, what to say, how much it matters, and which identifier it reports under.
/// </summary>
/// <remarks>
/// <para>
/// The base exists so that each kind is one predicate, and so that the reporting - which is the same four lines in
/// every one of them - has a single implementation. A rule reports and never repairs, so there is nothing else for a
/// body to do: the repair belongs to whoever knows how to fix the finding, exactly as it does for a hand-written
/// <see cref="IConstraint"/>.
/// </para>
/// <para>
/// A rule does not rely on the lowering having checked its slot. A rule declared for <em>every</em> node names a slot
/// no single concept could have been checked against, so a body that does not apply to a node simply says nothing -
/// which is the same answer a rule gets for a node whose concept declares the slot with another kind.
/// </para>
/// </remarks>
public abstract class SlotConstraint : IConstraint
{
    private protected SlotConstraint(string slot, string message, DiagnosticSeverity severity, string? code)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentException.ThrowIfNullOrEmpty(message);

        Slot = slot;
        Message = message;
        Severity = severity;
        Code = code;
    }

    /// <summary>The slot of the node this rule reads.</summary>
    public string Slot { get; }

    /// <summary>What is wrong, in one sentence the user can act on.</summary>
    public string Message { get; }

    /// <summary>How much the finding matters.</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>The identifier a host groups findings by, or <see langword="null"/> for none.</summary>
    public string? Code { get; }

    /// <inheritdoc/>
    public void Check(ConstraintContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Applies(context.Node))
        {
            context.Report(Severity, context.Node, Message, Slot, Code);
        }
    }

    /// <summary>Whether <paramref name="node"/> has the problem this rule states about <see cref="Slot"/>.</summary>
    private protected abstract bool Applies(Node node);

    public override string ToString() => $"{GetType().Name} '{Slot}'";
}
