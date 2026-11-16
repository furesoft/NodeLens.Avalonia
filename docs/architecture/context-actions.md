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
diagnostic rather than to a caret — is the same list with a different source.
[Quick fixes](quick-fixes.md) is planned separately and lands **first**, because diagnostics
exist today while intentions do not: the general list is then read off two real sources
instead of guessed from one, and it inherits the `EditProgram` that fixes build.

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

Resolution order is host intentions, then language intentions, then [quick
fixes](quick-fixes.md), each group filtered by availability and de-duplicated by id. Order is
part of the visible behaviour, so it is fixed here rather than left to registration order: a
host's action is what the user of that host wants first, and a language's action is what a
user of that language expects.

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
| hover an entry for 700 ms | show what it would do, in place ([intention-preview.md](intention-preview.md)) |

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

## Hovering long enough shows a preview

A label answers "what does this do"; it does not answer "what will it look like", and for a
conversion - a `for` becoming a `while`, a rule becoming a table row - that is the question the user
has. So an entry that is hovered long enough replaces the page with the result of its edit, marks what
changed, and puts everything back when the pointer leaves.

The result is produced by applying the real action to a **discarded copy** of the document and
projecting that copy: no second "describe" implementation per action, and nothing that can drift from
what applying it does. The live model, its history and its journal are never touched. The whole design
- the sandbox, the diff by node id, the marks, the hover ladder and its budget - is
[intention-preview.md](intention-preview.md), decided in
[ADR 0017](../adr/0017-preview-is-a-discarded-edit.md).

Two consequences for this document's own plan:

- The description does not become redundant, and the slice that builds the list still has to write
  one. A preview is available to a pointer that hovers long enough, so it cannot be the only
  explanation of what an entry does.
- A preview is a property of the *list*, not of a single affordance: the bulb, the popup and the
  context-menu submenu all show the same list, so they all get previews from one implementation.

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

A diagnostic's repairs are [quick fixes](quick-fixes.md), and they land **before** this list,
because diagnostics exist today while intentions do not. The two meet at the bulb — how the
diagnostic is *shown* is [markers.md](markers.md):

- the bulb for the caret's line reports the highest-severity marker on that line;
- opening it lists the diagnostic's fixes first, then the intentions, separated;
- a fix that is not applicable is shown disabled with the diagnostic's message as its
  description, so the user reads the problem in the same place as the cure.

The quick-fix slices therefore deliver the bulb, the keys and the "Quick fixes" submenu, and
what remains here is teaching the same affordances a second source. That is the honest order:
the shapes (`Intention`, `IntentionContext`, `IntentionList`, `IntentionAvailability`) are then
read off two real sources instead of guessed from one, and `EditProgram` is built by the slice
that needs it first rather than by two slices independently.

## Standard intentions a language can include

The quick-fix plan catalogues what a framework can ship as a *repair*
([quick-fixes.md](quick-fixes.md#standard-checks-a-language-can-include)). Intentions need the same
treatment for the same reason, and the filter is the sharper version of the same test: an intention is
generic when it can be derived from **the shape of the place the caret stands on** - a node, an empty
slot, a value, an element of a list - plus the language's declarations. Nothing about the catalogue
below knows what a rule or a loop *means*.

That is why the tables are ordered by that shape: it is the editor's own addressing vocabulary, so a
missing entry is easy to spot, and every entry names the data that produces it.

### The caret stands on a node

| Id | The entry | What it does | Class | Needs |
| --- | --- | --- | --- | --- |
| `INT001` | `Rename` | puts the caret in the name slot, the value selected | plain | new: a caret jump that selects, which is `MoveCaretAction` plus `SelectsLandedValue` |
| `INT002` | `Duplicate` | copies the subtree as the next sibling | plain | new: subtree copy plus insert-at-index |
| `INT003` | `Delete` | removes the node, exactly as the key does | **destructive** | - (`DeleteNodeAction`) |
| `INT004` | `Move up` / `Move down` | one place within its slot | plain | - (`MoveNodeAction`) |
| `INT005` | `Move to front` / `Move to back` | to the first or last place of its slot | plain | new: insert-at-index |
| `INT006` | `Wrap in "X"` | one entry per concept that has a single-child slot accepting this node's concept and can itself stand in this node's place | plain | new: `WrapInAction` - the only genuinely new action here |
| `INT007` | `Replace with "Y"` | swaps the node for another concept the slot accepts | **destructive** (the old subtree is gone) | `DefaultInstantiableFor` plus a replace action |
| `INT008` | `Select parent` / `Select node` | widens the selection one rung | plain | - (`SelectAllAction`'s ladder) |

`INT006` is the entry this catalogue exists for. It is how "surround with `if`" happens in a code DSL,
but in this framework it needs no `if`: any concept with a hole of the right shape is a wrapper, so the
editor offers the language's own compositions instead of a fixed list. Two rules keep it honest, and
they are open questions 76 and 77.

`INT001` deserves the note: it is a *navigation*, not a refactoring. It moves the caret into the name
and lets the user type, so it cannot break a reference in another model - which is exactly the problem
the standard rename *fix* has and cannot solve (open question 74).

### The caret stands on an empty slot

| Id | The entry | What it does | Class | Needs |
| --- | --- | --- | --- | --- |
| `INT010` | `Insert "X"` | fills the slot with the concept `DefaultInstantiableFor` answers | plain | - (`InsertNodeAction`) |
| `INT011` | `Insert...` | the same list completion offers, for picking another | plain | - |

These two are what Enter and `Ctrl+Space` already do; listing them is what makes the pair discoverable
from a menu, and it costs nothing because the actions exist.

### The caret stands in a value

| Id | The entry | What it does | Class | Needs |
| --- | --- | --- | --- | --- |
| `INT020` | `Reset to the default` | writes the declared default over the current value | plain | - (`SetPropertyAction`) |
| `INT021` | `Select the value` | selects the whole text of the cell | plain | - (`SelectValueAction`) |

`INT020` is the cheapest useful intention in the framework: always correct, always local, always one
undo step, and it is the one gesture a user reaches for after pasting a value they did not want.

### The caret stands on an element of a list

| Id | The entry | What it does | Class | Needs |
| --- | --- | --- | --- | --- |
| `INT030` | `Insert before` / `Insert after` | a new empty sibling at that place | plain | new: insert-at-index |
| `INT031` | `Sort by name` | reorders the slot by the elements' name slot | **destructive** (order is data) | a program of moves, and a name slot on the element concept |

### With `Projectional.Expressions`

| Id | The entry | What it does | Class | Needs |
| --- | --- | --- | --- | --- |
| `INT040` | `Wrap in parentheses` | surrounds the expression with the grouping concept | plain | `WrapInAction`, the same one as `INT006` |
| `INT041` | `Negate` | puts a `Unary` over it | plain | a small action, or `WrapInAction` with the operator preset |
| `INT042` | `Swap operands` | swaps left and right of a binary | plain | two `MoveNodeAction`s? no - a small swap action, because a move would renumber |

`INT042` is the one place where "reuse the existing action" fails: swapping two children of one slot by
moving is order-dependent and collides with itself, so it needs its own action even though it looks like
a move.

### What the framework can only *list*

| Id | The entry | Why it is not an action |
| --- | --- | --- |
| `INT050` | `Show the target` | navigating to another model is the host's job: it owns tabs, windows and focus. The framework resolves the reference and offers the entry, the host runs its own command from the same id - the boundary the intention contract already draws |
| `INT051` | `Generate` / `Run` | same: the host runs its pipeline, the language supplies the generator |

### What is free, and what is not

Nine of the entries above are configurations of actions that already exist
(`InsertNodeAction`, `DeleteNodeAction`, `MoveNodeAction`, `SelectAllAction`, `SelectValueAction`,
`SetPropertyAction`, `EditProgram`). Four are not, and they reduce to three primitives:

| Missing primitive | Blocks | Why it is a primitive, not a per-feature hack |
| --- | --- | --- |
| `Model.InsertChild(parent, slot, index, concept)` | `INT002`, `INT005`, `INT030` | the model appends today, so "insert here" is impossible for anything; a list needs it whichever feature asks |
| a subtree copy (`Node.CopyTo(sibling)`) | `INT002` | the same walk `Model.CopyTo` needs for the preview sandbox, reused instead of written twice |
| `WrapInAction` | `INT006`, `INT040` | wrapping is insert-at-index plus a move, and both halves have to happen in one transaction |
| `SortAction` | `INT031` | a stable permutation of one slot, which a program of moves cannot express without colliding |

That table is the plan's real content: the catalogue is not nineteen features, it is **four primitives
and fifteen four-line compositions**.

### Two rules, one of them shared with the fixes

**The safety class is shared, so it belongs on the offer rather than on the fix.** The quick-fix
catalogue calls it `FixSafety`; writing the intentions down shows that both kinds of offer need it, so it
becomes `EditSafety` and both `DiagnosticFix` and `Intention` carry it. The rule it enforces is the same
in both places: a lone offer runs immediately only when it is plain, and a lone destructive one opens the
list to ask (open question 73).

**The two catalogues must not offer the same thing twice.** `STD010` fills an empty slot because a check
found the hole; `INT010` fills it because the caret is there. Both are right, and both are in the list the
user sees at that caret - so they need one id or an explicit suppression rule, or the list shows the same
sentence twice. The cheap answer is one id per operation, which is also what the de-duplication in the
resolution order already does.

## Slices

Each slice is its own branch and its own merge, per the working rules. The quick-fix slices
Q1-Q6 in [quick-fixes.md](quick-fixes.md) come first; the branches below follow them.

| Slice | Branch | Delivers |
| --- | --- | --- |
| A | `feat/intentions-model` | `Intention`, `IntentionContext`, `IntentionList.For(session)`, provider registration on the session, one fixture intention in the test language, tests for order, availability, dedup, and the two-source resolution order (host, language, fixes) |
| B | `feat/intentions-apply` | `EditorSession.Apply(Intention)`, one-undo-step and rollback tests against the `EditProgram` that quick fixes introduced |
| C | `feat/intentions-keys` | the intention list in the `Alt+Enter` / `Ctrl+.` popup beside the fixes, built from `EditorMenuEntry`s through `EditorMenuPresenter` |
| D | `feat/intentions-bulb` | the "Intentions" submenu in the context menu, and the bulb reporting intentions when the line carries no marker |
| E | `feat/intentions-sample` | two real intentions in `Projectional.Expressions` (wrap in parentheses, negate) and one in `Projectional.Samples`, proving a host adds none of the plumbing |
| F | `feat/intentions-preview` | the longer-hover preview: slices V1-V6 in [intention-preview.md](intention-preview.md), which is where the copy, the diff, the ladder and the ghost are specified |
| G | `feat/standard-intentions` | `EditSafety` on the offer (shared with the fix catalogue) and the first primitives the catalogue needs: insert-at-index, subtree copy and `WrapInAction`; then the first four entries (`INT001`, `INT006`, `INT020`, `INT040`) |

Slices A and B are the ones with the invariants in them and are testable without a window;
C and D extend affordances that quick fixes have already delivered and verified.

## Decisions this plan proposes

To be frozen as an ADR when slice A lands, not before — the text above is the argument:

1. An intention produces `EditAction`s, applied by the session as one undo step; it never
   mutates the model itself.
2. The list is derived on demand and never persisted or cached across rebuilds.
3. The bulb is chrome drawn by the control from editing-layer geometry, never a cell.
4. Language intentions are an aspect; host intentions are session registrations; both are
   filtered by the caret's place, and the order is host, language, fixes.
5. The intention list and the context menu share one entry model.

The preview is not in that list because it was decided separately and earlier, in
[ADR 0017](../adr/0017-preview-is-a-discarded-edit.md): a preview is a projection of a discarded
edit, never a second description of one.

## What this plan deliberately leaves out

| Left out | Why |
| --- | --- |
| Authoring intentions in the language editor (declared form) | follows the other aspects through the bootstrap stages; M5 |
| Intention search across the whole document (`Ctrl+Shift+A`) | a command palette is a shell feature, not an editing one, and needs its own ranking rules |
| Preview of a change across several files | the same machinery as a single-document preview; what it still needs is a policy for scrolling and switching tabs, which is M6 with the refactorings |
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
