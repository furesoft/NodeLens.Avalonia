# Context actions (intentions)

*Planned, not implemented.* This document is the design for the light bulb: the affordance
that offers, at the caret, the edits that make sense *there* — "wrap in parentheses",
"negate", "add the missing condition", "extract this into a rule". It is written before the
code so that the three layers it touches — language, editing, control — agree on who owns
what, and so the milestone that implements it has a gate instead of an ambition.

The name is MPS': an **intention** is an action offered *because of where the caret is*,
chosen from a menu, applied as one edit. "Light bulb" is Visual Studio's name for the same
idea; the UI section says where the bulb appears, the rest of the document is about the
model behind it. Visual Studio's second half of the concept — a **quick fix** attached to a
diagnostic rather than to a caret — is the same list with a different source, and is scoped
to the milestone that first produces diagnostics.

## What an intention is

Not a new kind of edit. An intention is *a way to find* an `EditAction`:

- It has a **stable id** (`"expressions.wrap-in-parens"`), so a host can bind a key to it and
  a test can name it without matching a label that will be translated.
- It has a **label** for the menu, and optionally a **description** — one sentence saying what
  it will do, shown as a tooltip. A label like "Extract" without a description is a riddle;
  the description is part of the contract, not decoration.
- It has a **gesture** if any (most have none; `Alt+Enter` opens the *list*, not an action).
- It reports whether it **applies** at a caret position, and it can be **disabled with a
  reason** — "nothing to negate" is different from "this is not here at all".
- Applying it produces an `EditAction`, or a **program** of them, which the session runs
  inside one transaction so it is one undo step.

That last point is the one that decides the design. An intention that renames a node, moves
a child and fixes a reference must not be three undo steps, and it must not be the
intention's job to remember how to undo — the model already does that. So the program is
applied through the same session path a keystroke takes, with the coalescing key cleared so
the program is one step and a following keystroke starts another.

```csharp
public sealed class Intention
{
    public string Id { get; }            // stable, never translated
    public string Label { get; }         // menu text
    public string? Description { get; }  // one sentence, tooltip
    public string? Gesture { get; }      // optional, spelled as the toolkit parses it
    public IntentionAvailability Availability { get; }  // Applies | Disabled(reason) | Absent
    public Func<IntentionContext, EditAction?> Create { get; }
}
```

`IntentionContext` is the same view an `EditAction` already gets, plus what an intention
needs to *decide*: the node the caret is on, the cell's address, the selection, and read-only
access to the model. It deliberately does **not** expose the session's mutation methods —
deciding and applying stay separate, so an intention cannot half-apply while still deciding.

## Where intentions come from

Two sources, because they answer different questions:

1. **The language** says what is meaningful for a concept — an aspect, like every other
   language aspect, so it is data in the declared stages (M2/M5) and a delegate now:

   ```csharp
   concept.Intentions(caret => new[]
   {
       Intention.Of("expressions.wrap-in-parens", "Wrap in parentheses")
           .Described("Surround this expression with ( ) so precedence changes.")
           .When(n => n.Slot() is "operand")
           .Do(n => new WrapInNodeAction(TestLanguage.Parens)),
   });
   ```

2. **The host** adds actions that are not the language's business — "deploy", "generate
   now", "open the referenced model". These are registered on the session
   (`session.AddIntentions(provider)`), filtered by a predicate over the caret, and are how an
   embedding application puts its own commands next to the structural ones.

Resolution order is host intentions, then language intentions, then quick fixes, each group
filtered by availability and de-duplicated by id. Order is part of the visible behaviour, so
it is fixed here rather than left to registration order: a host's action is what the user of
that host wants first, and a language's action is what a user of that language expects.

The caret position is resolved first, exactly as for a key: an intention is offered for the
**cell the caret is on**, not for the node that happens to contain it. If the caret stands on
a keyword, the keyword's concept answers. If it stands on an empty slot, the *slot's owner*
answers — an empty slot has no concept of its own, and "insert a rule here" is an intention
about the parent.

## The bulb is derived, never stored

`IntentionList.For(session)` returns the list for the current caret, freshly, on every call —
the same rule `EditorMenu.For` already follows. Nothing is cached, so nothing can go stale
after an edit, an undo, a variant switch, or a change the session did not make. What makes
that affordable:

- the list is one caret position's worth of work: resolve the place, ask two providers;
- providers are expected to be cheap predicates, not analyses — a provider that needs scope
  resolution or type inference is a provider that runs during validation, not during paint;
- the control computes the list **after** a rebuild, once per rebuild, and keeps only the
  boolean "is there anything here" for drawing the bulb. The entries themselves are built
  when the popup opens.

The bulb showing means "there is at least one applicable intention". A disabled-only list
still shows the bulb: the user learns that the concept knows about this caret and why it
cannot act, which is the useful half of the message.

## The bulb is chrome, not a cell

The bulb is **not** an editor cell and never enters the projection. It is drawn by the
control in the margin left of the page, vertically aligned with the line the caret is on,
because:

- a cell would appear in the editor tree, so it would need an address, a caret place and hit
  testing, and the projection would stop being a pure function of model and notation
  ([ADR 0004](../adr/0004-stable-node-ids.md), [ADR 0009](../adr/0009-custom-drawn-editor-control.md));
- an intention is a property of the *session's caret*, and the editing layer already owns
  that. A host that paints its own editor must be able to show the same bulb without a
  custom cell type;
- the bulb must not shift layout. A margin glyph that changes the width of the document when
  it appears would move the text under the user's eyes while they type.

The editing layer therefore exposes the *geometry* (`IntentionBulb.BoundsOf(layout, caret,
measure)`) and nothing about pixels, so a headless test can assert where the bulb is and a
second renderer can draw its own. The margin width is a control constant, not a measure
constant — an embedded editor inside a narrow panel should not spend 20 px on a gutter it
never shows.

## Interaction

| Input | Effect |
| --- | --- |
| `Alt+Enter`, `Ctrl+.` | open the list for the caret; if it holds exactly one applicable intention, run it |
| click the bulb | same list, with the pointer |
| `Esc` | close without editing |
| `Up`/`Down`, `Enter` | move and run — the list is navigable without a mouse |

"One intention → run it" is deliberate: the common case is a single fix, and a popup with one
entry costs the user a keystroke for nothing. It also makes the feature usable from the
keyboard before the popup exists, which is why the popup is a separate slice.

The list reuses the context-menu model: entries are `EditorMenuEntry`s, so labels, gestures,
enabled state, separators, nesting and execution are already built and tested, and
`EditorMenuPresenter` does the toolkit mapping. The two surfaces stay in sync by construction
— the same intention appears in the context menu under an "Intentions" submenu when it
applies, which is slice D rather than a second implementation.

Focus and lifetime follow the context menu's rules exactly: the popup belongs to the control,
opening a second one closes the first, detaching the control closes it, and a command runs
the session directly and then measures and repaints.

## Undo, and what a program is

The session grows one method:

```csharp
public EditResult Apply(Intention intention);
```

It resolves the caret, asks the intention for its action, and applies it with the coalescing
key disabled. An intention that needs several edits returns a program
(`EditProgram.Of(action1, action2, …)`) which the session runs inside a single transaction;
if any step is rejected the transaction is rolled back and the result is `Rejected` with the
step's reason, so a half-applied intention never reaches the model.

This is a *narrower* promise than "an intention may do arbitrary work": programs of
`EditAction`s are what keeps an intention undoable, testable headlessly and legal in a
projectional editor where the model is the only source of truth. An intention that wants to
do something else — call a service, open a dialog, run generation — returns no action and the
host runs its own command from the same id; the bulb is then a launcher, not an editor.

## Error markers and quick fixes

Once constraints produce diagnostics (M4), a diagnostic carries an optional list of fixes,
and a fix is an intention with the diagnostic as its source. The two meet at the bulb —
how the diagnostic is *shown* is [markers.md](markers.md):

- the bulb for the caret's line reports the highest-severity marker on that line;
- opening it lists the diagnostic's fixes first, then the intentions, separated;
- a fix that is not applicable is shown disabled with the diagnostic's message as its
  description, so the user reads the problem in the same place as the cure.

Nothing about that needs a second mechanism, which is the reason to design the intention
list now rather than after validation exists.

## Slices

Each slice is its own branch and its own merge, per the working rules.

| Slice | Branch | Delivers |
| --- | --- | --- |
| A | `feat/intentions-model` | `Intention`, `IntentionContext`, `IntentionList.For(session)`, provider registration on the session, one fixture intention in the test language, tests for order, availability, dedup |
| B | `feat/intentions-apply` | `EditorSession.Apply(Intention)`, `EditProgram`, one-undo-step and rollback tests |
| C | `feat/intentions-keys` | `Alt+Enter` / `Ctrl+.` routing in the control, run-when-single, popup built from `EditorMenuEntry`s through `EditorMenuPresenter` |
| D | `feat/intentions-bulb` | `IntentionBulb.BoundsOf`, drawing in the control's margin, its hit test, repaint policy, "Intentions" submenu in the context menu |
| E | `feat/intentions-sample` | two real intentions in `Projectional.Expressions` (wrap in parentheses, negate) and one in `Projectional.Samples`, proving a host adds none of the plumbing |
| F | (M4) `feat/quick-fixes` | diagnostics carry fixes; the bulb reports the line's marker; fixes listed before intentions |

Slices A and B are the ones with the invariants in them and are testable without a window;
C and D are presentation and are the first customers of the headless Avalonia test project.

## Decisions this plan proposes

To be frozen as an ADR when slice A lands, not before — the text above is the argument:

1. An intention produces `EditAction`s, applied by the session as one undo step; it never
   mutates the model itself.
2. The list is derived on demand and never persisted or cached across rebuilds.
3. The bulb is chrome drawn by the control from editing-layer geometry, never a cell.
4. Language intentions are an aspect; host intentions are session registrations; both are
   filtered by the caret's place, and the order is host, language, fixes.
5. The intention list and the context menu share one entry model.

## What this plan deliberately leaves out

| Left out | Why |
| --- | --- |
| Authoring intentions in the language editor (declared form) | follows the other aspects through the bootstrap stages; M5 |
| Intention search across the whole document (`Ctrl+Shift+A`) | a command palette is a shell feature, not an editing one, and needs its own ranking rules |
| Preview / diff of what an intention will do | needs a side-effect-free "describe" form per action; the description string is the honest 80 % |
| Auto-apply when typing (MPS' transformation triggers) | that is the substitution-action feature of M3, which shares the action model but not the affordance |
| Intention ranking by usage statistics | unexplainable ordering; fixed order is debuggable |
| Localisation of labels | labels are strings until the M5 message-key pass |

## Testing

- **Editing, headless.** A fixture language with three intentions (one always, one never,
  one conditional) and assertions on: the list for a caret in a keyword, in a property, on an
  empty slot, on a selection; the order of host versus language entries; de-duplication by
  id; a program that rolls back; one undo step per intention.
- **Geometry, headless.** `IntentionBulb.BoundsOf` for a caret on the first line, on a
  wrapped second line, and on a cell at the page's left edge.
- **Control, when `tests/Projectional.Avalonia.Tests` exists.** `Alt+Enter` with zero, one and
  three applicable intentions; `Esc`; the popup closing when the control detaches.
- **Golden corpus.** Slice E's sample intentions run as part of the sample-language corpus, so
  a change to the projection that breaks an intention's precondition fails a normal test, not
  a manual check.

## Open questions

The genuinely undecided parts are rows 30–34 in [open-questions.md](../open-questions.md);
this document states its recommendation for each.
