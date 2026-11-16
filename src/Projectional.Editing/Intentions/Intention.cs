using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>A way to find an edit that makes sense where the caret is.</summary>
/// <remarks>
/// <para>
/// An intention is not a new kind of edit: it is a <em>way to find</em> an <see cref="EditAction"/>, chosen
/// from a list that is derived from the model rather than from anything the user typed. That is why it
/// carries a function rather than a command: deciding has to stay separate from applying, so an intention
/// cannot half-apply while it is still deciding, and an intention that has nothing to do answers
/// <see langword="null"/> instead of doing something surprising.
/// </para>
/// <para>
/// The id is stable and never translated, so a host can bind a gesture to one and a test can name one without
/// matching a label that will be localised later. The label and the description are user-facing text and may
/// be computed per caret - "Wrap in \"(\"" is more useful than "Wrap in parentheses" when the concept decides
/// the spelling.
/// </para>
/// <para>
/// The description is required, unlike a fix's. A fix has the finding it repairs to fall back on, so a missing
/// sentence still has something true to show; an intention has nothing behind it, and a label like "Extract"
/// without a sentence is a riddle. Making it required is the only way to keep that promise, because nothing
/// downstream can invent the sentence.
/// </para>
/// <para>
/// An entry produced from a quick fix keeps that fix (<see cref="Fix"/>), because applying it has to go
/// through <see cref="EditorSession.Apply(DiagnosticFix)"/> and its re-resolution against the current fix
/// set. Everything else about the entry - id, label, description, availability, safety - is already the
/// answer the fix gave, so the two catalogues share one list without either learning about the other.
/// </para>
/// </remarks>
public sealed class Intention
{
    /// <summary>Describes an intention a language or a host declares.</summary>
    /// <param name="id">A stable identifier, unique within the list - <c>expressions.wrap-in-parens</c>.</param>
    /// <param name="label">What the entry says in a menu, which may name the result rather than the operation.</param>
    /// <param name="description">One sentence saying what it does, shown as a tooltip.</param>
    /// <param name="create">
    /// Builds the edit, or answers <see langword="null"/> when this caret turns out not to need it after all.
    /// </param>
    /// <param name="gesture">The key combination that runs it on its own, or <see langword="null"/> for none.</param>
    /// <param name="availability">
    /// Whether it applies at this caret; <see cref="IntentionAvailability.Applies"/> when omitted.
    /// </param>
    /// <param name="safety">
    /// Whether applying it removes content; <see cref="EditSafety.Plain"/> when omitted.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="create"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/>, <paramref name="label"/> or <paramref name="description"/> is empty, or
    /// <paramref name="gesture"/> is empty when it was passed at all.
    /// </exception>
    public Intention(
        string id,
        string label,
        string description,
        Func<IntentionContext, EditAction?> create,
        string? gesture = null,
        IntentionAvailability availability = default,
        EditSafety safety = EditSafety.Plain)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(create);

        if (gesture is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gesture, nameof(gesture));
        }

        Id = id;
        Label = label;
        Description = description;
        Create = create;
        Gesture = gesture;
        Availability = availability;
        Safety = safety;
    }

    private Intention(
        string id,
        string label,
        string description,
        Func<IntentionContext, EditAction?> create,
        string? gesture,
        IntentionAvailability availability,
        EditSafety safety,
        DiagnosticFix fix)
        : this(id, label, description, create, gesture, availability, safety) => Fix = fix;

    /// <summary>A stable identifier, never shown to the user and never translated.</summary>
    public string Id { get; }

    /// <summary>What a menu entry says, which may name the result rather than the operation.</summary>
    public string Label { get; }

    /// <summary>One sentence explaining the intention, which is required rather than optional.</summary>
    public string Description { get; }

    /// <summary>Builds the edit that applies this intention, or answers <see langword="null"/>.</summary>
    /// <remarks>
    /// Called at the moment of application, not when the intention was declared, so the action it produces
    /// describes the model as it is then. It receives a read-only view of the caret rather than the session's
    /// mutation methods, so it can decide but not change anything.
    /// </remarks>
    public Func<IntentionContext, EditAction?> Create { get; }

    /// <summary>The key combination that runs it without opening the list, or <see langword="null"/>.</summary>
    /// <remarks>
    /// Spelled the way the toolkit parses it ("Ctrl+Shift+K"), so a menu entry can print the gesture it
    /// actually honours. Most intentions have none: <c>Alt+Enter</c> opens the <em>list</em>, not one action.
    /// </remarks>
    public string? Gesture { get; }

    /// <summary>Whether it applies at this caret, cannot run there, or is not about this place.</summary>
    public IntentionAvailability Availability { get; }

    /// <summary>What applying it can take away, which decides whether it may run unasked.</summary>
    /// <remarks>
    /// Shared with the fix catalogue: one enum and one rule, so "a lone offer runs immediately only when it is
    /// plain" cannot drift between the two sources of the list.
    /// </remarks>
    public EditSafety Safety { get; }

    /// <summary>The quick fix this entry stands for, or <see langword="null"/> for a declared intention.</summary>
    /// <remarks>
    /// Set only by <see cref="FromFix"/>. It exists so that <see cref="EditorSession"/> can route the entry
    /// back through the fix's own apply path, which re-resolves it against the current fix set - a guarantee a
    /// general intention does not need and a fix cannot do without.
    /// </remarks>
    public DiagnosticFix? Fix { get; }

    /// <summary>Whether the intention is not about this caret at all.</summary>
    public bool IsAbsent => Availability.IsAbsent;

    /// <summary>Whether the intention is offered but cannot run.</summary>
    public bool IsDisabled => Availability.IsDisabled;

    /// <summary>Whether applying the intention removes content.</summary>
    public bool IsDestructive => Safety == EditSafety.Destructive;

    /// <summary>The same intention, with a different availability for this caret.</summary>
    /// <param name="availability">What it answers at the caret being asked about.</param>
    /// <returns>A new intention; this one is unchanged.</returns>
    /// <remarks>
    /// How a declared intention is made per-caret. A language declares its entry once with everything that is
    /// true wherever it appears, then answers <see cref="IntentionAvailability.Absent"/> or
    /// <see cref="IntentionAvailability.Disabled"/> per caret - which is why the value is immutable and this
    /// method returns a copy rather than mutating.
    /// </remarks>
    public Intention With(IntentionAvailability availability) =>
        Fix is { } fix
            ? new Intention(Id, Label, Description, Create, Gesture, availability, Safety, fix)
            : new Intention(Id, Label, Description, Create, Gesture, availability, Safety);

    /// <summary>The entry that represents <paramref name="fix"/> in the context list.</summary>
    /// <param name="fix">The repair to stand for.</param>
    /// <remarks>
    /// <para>
    /// A fix is a repair for a finding, which is a different question from "what can I do at this caret" -
    /// but the user sees one list, so the two vocabularies meet here and nowhere else. The entry inherits
    /// everything the fix already answered: its id (which is what de-duplicates the two catalogues when they
    /// offer the same operation), its label, its sentence or the finding's message, its reason for being
    /// disabled, and its safety class. It maps to <c>Applies</c> or <c>Disabled</c> and never to
    /// <c>Absent</c>, because a fix that was built is about a real finding.
    /// </para>
    /// <para>
    /// The created action resolves the fix's own target rather than the caret's: a repair repairs the node it
    /// was built for, which is not necessarily the node the caret is on.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="fix"/> is <see langword="null"/>.</exception>
    public static Intention FromFix(DiagnosticFix fix)
    {
        ArgumentNullException.ThrowIfNull(fix);

        var availability = fix.Availability.Reason is { } reason
            ? IntentionAvailability.Disabled(reason)
            : IntentionAvailability.Applies;

        return new Intention(
            fix.Id,
            fix.Label,
            fix.Tip,
            context => fix.Create(new FixContext(
                context.Document.Model,
                fix.Diagnostic,
                context.Document.Model.Project.FindNode(fix.Diagnostic.Target))),
            gesture: null,
            availability,
            fix.Safety,
            fix);
    }

    public override string ToString() => $"{Id}: {Label} ({Availability})";
}
