using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>The repair one finding offers: what to call it, and how to build the edit that does it.</summary>
/// <remarks>
/// <para>
/// A fix is a value <em>per finding</em>, not an entry in a static catalogue. The check that found the
/// problem is the only thing that knows what the repair would look like, so it builds the fix - including
/// a label that names the result ("Rename to \"Check2\"") - at the moment it reports the diagnostic. That
/// is also why a fix carries its <see cref="Diagnostic"/>: the pair is the identity a session re-resolves
/// against after a new set arrives, and it is what lets a refused pick be explained by the finding it
/// belonged to.
/// </para>
/// <para>
/// A fix never mutates the model. <see cref="Create"/> answers an <see cref="EditAction"/> - or a program
/// of several, once one exists - which the session applies exactly as it applies a keystroke, so the
/// repair is one undo step, testable without a window, and incapable of anything the model does not
/// allow. A repair that needs to open a dialog or call a service answers <see langword="null"/> and the
/// host runs its own command from the same <see cref="Id"/>.
/// </para>
/// <para>
/// The id is stable and never translated, so a host can bind a gesture to one and a test can name one
/// without matching a label that will be localised later. The label and the description are user-facing
/// text and may be computed per finding.
/// </para>
/// </remarks>
public sealed class DiagnosticFix
{
    /// <summary>Describes a repair for <paramref name="diagnostic"/>.</summary>
    /// <param name="diagnostic">The finding this fix repairs.</param>
    /// <param name="id">
    /// A stable identifier, unique among the fixes of that finding - <c>rules.duplicate-name.rename</c>.
    /// </param>
    /// <param name="label">What the entry says in a menu, which may name the result of applying it.</param>
    /// <param name="create">
    /// Builds the edit, or answers <see langword="null"/> when this finding turns out not to need it after
    /// all. Receives the context rather than hunting for the node itself.
    /// </param>
    /// <param name="description">One sentence of explanation, shown as a tooltip.</param>
    /// <param name="availability">
    /// Whether the fix can run now; <see cref="FixAvailability.Applies"/> when omitted.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="diagnostic"/> or <paramref name="create"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> or <paramref name="label"/> is empty.</exception>
    public DiagnosticFix(
        Diagnostic diagnostic,
        string id,
        string label,
        Func<FixContext, EditAction?> create,
        string? description = null,
        FixAvailability availability = default)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(create);

        Diagnostic = diagnostic;
        Id = id;
        Label = label;
        Create = create;
        Description = description;
        Availability = availability;
    }

    /// <summary>The finding this fix repairs.</summary>
    /// <remarks>
    /// Part of the fix's identity, not merely context: two findings on one node can have different cures,
    /// so "the fix called rename" is ambiguous until it is qualified by the finding it belongs to.
    /// </remarks>
    public Diagnostic Diagnostic { get; }

    /// <summary>A stable identifier, never shown to the user and never translated.</summary>
    public string Id { get; }

    /// <summary>What a menu entry says, which may name the result rather than the operation.</summary>
    public string Label { get; }

    /// <summary>Builds the edit that applies this fix, or answers <see langword="null"/>.</summary>
    /// <remarks>
    /// Called at the moment of application, not when the fix was built, so the action it produces
    /// describes the model as it is then. It must therefore not close over a resolved node: a fix built
    /// against an earlier model would otherwise reach a document that has moved on.
    /// </remarks>
    public Func<FixContext, EditAction?> Create { get; }

    /// <summary>One sentence explaining the repair, or <see langword="null"/>.</summary>
    /// <remarks>
    /// A label like "Rename" without a sentence is a riddle, so this is part of the contract rather than
    /// decoration. When a fix has none, a menu falls back to the finding's own message, which describes
    /// the problem the label is repairing.
    /// </remarks>
    public string? Description { get; }

    /// <summary>Whether the fix can run now, and why not when it cannot.</summary>
    public FixAvailability Availability { get; }

    /// <summary>Whether the fix is offered but cannot run.</summary>
    public bool IsDisabled => Availability.IsDisabled;

    /// <summary>The text a menu shows under the label: the fix's own sentence, or the finding's.</summary>
    public string Tip => Description ?? Diagnostic.Message;

    public override string ToString() => $"{Id}: {Label}";
}
