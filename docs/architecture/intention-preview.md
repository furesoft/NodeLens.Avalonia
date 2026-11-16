# Intention preview

What an intention would do, shown before it is applied: hovering an entry for long enough replaces the
page with the result of that edit, marks what changed, and puts everything back when the pointer leaves.
For a rename that reads as the new names in place; for a conversion - a `for` turned into a `while`, a
rule turned into a table row - it is the only honest answer, because the question "what will this look
like" has no good sentence.

Status: **in progress.** The intention list it needs exists ([context-actions.md](context-actions.md),
slices A-E), and slice V1 - the copy and the diff - is delivered; V2 onwards are the preview itself. The
decision it rests on is [ADR 0017](../adr/0017-preview-is-a-discarded-edit.md).

This document reverses a decision that was recorded twice: [context-actions.md](context-actions.md) and
[quick-fixes.md](quick-fixes.md) both left previews out because they assumed a preview needs a
side-effect-free "describe" form per action. It does not need one - see below - and open question 34 is
answered by this document rather than deferred again.

## Three words

| Word | What it is |
| --- | --- |
| **The edit** | the `EditAction` the intention produces, which is also what applying it runs |
| **The sandbox** | a copy of the document, in its own project, that the edit is applied to and that is thrown away |
| **The ghost** | the projection of the sandbox, drawn instead of the live page, with the changes marked |

A preview is those three in that order. Nothing else is a preview: not a description, not a synthetic
summary, not a diff table.

## A preview is a projection of a discarded edit

The one sentence that decides the shape: **to show what an edit would do, do it to a copy.**

Why this and not something cheaper:

- **It cannot drift.** A preview that runs the real action shows the real result. A "describe" form per
  action would be a second implementation, and the second implementation is always the one that is
  wrong when the first one changes - the reason this project has one notation engine and not two.
- **It costs no new vocabulary.** The intention already produces an `EditAction`
  ([ADR 0016](../adr/0016-quick-fixes-are-edits-a-host-supplies.md), the same shape for fixes). The
  preview needs no per-action cooperation at all.
- **It is only possible because the model is not text.** An action addresses node ids and slot names
  ([ADR 0002](../adr/0002-model-as-source-of-truth.md),
  [ADR 0004](../adr/0004-stable-node-ids.md)), so the same action value means the same thing in a copy.
  A text buffer would have offsets that no longer exist.

What it rejects, and why, is in [ADR 0017](../adr/0017-preview-is-a-discarded-edit.md). The one
alternative worth restating here: **applying to the live model and rolling back**. It looks cheaper and
is not - every subscriber sees the change and its inverse, the tree is rebuilt twice, the caret moves,
and the journal gains a step unless the rollback path is perfect.

## The sandbox is a separate project, not a second model

A copy cannot live next to the original. `Project` owns the node table (`FindNode` answers by id), so a
copy carrying the original's ids would collide with it; the sandbox is therefore its own `Project` with
the same `ConceptRegistry`.

```csharp
using var sandbox = PreviewSandbox.Open(session);   // copy + notations + measure, own project
sandbox.Apply(action);                              // the real action, on the copy
var ghost = sandbox.Tree;                           // a projection of the result
```

The copy is a Core capability (`Model.CopyTo(Project target)`), because it needs the detached-node seam
that is `internal` to Core and because copying a model is a model concern, not an editing one. It
preserves ids, properties, references and child order; that is the whole contract, and a test asserts a
copy and its original render to identical snapshots.

Cost is linear in nodes, so it is budgeted rather than assumed cheap:

| Budget | Value | On exceeding it |
| --- | --- | --- |
| nodes copied | 20 000 | no preview; the entry keeps its description |
| wall clock for copy + apply + layout | 100 ms | the running preview is abandoned and not shown |
| changes marked | 2 000 cells | the ghost is shown, the marks are truncated with a count |

The numbers are tuning; the shape is the decision: **a preview that is too expensive is not shown, and
never delays the keystroke that opened it.**

## What the ghost shows

The ghost is the preview's projection, laid out with the same measure, drawn **instead of** the live
page. Replacing the page rather than overlaying it is what makes the answer honest: a rename changes
widths, so an overlay would draw the new text at the old positions and lie about the layout.

| Mark | Meaning | Drawn as |
| --- | --- | --- |
| **Added** | the cell's node does not exist in the live document | the palette's addition tint behind the text |
| **Changed** | the node exists in both, and a property, a reference or its place differs | the palette's change tint, plus the old value as ghosted text after the new one |
| **Removed here** | a node that existed at this place in the live document is gone | a thin marker at the position the surviving sibling now occupies, with the removed text in its tooltip |
| **Unchanged** | everything else | the normal palette, dimmed by one step so the marks read first |

A badge in the corner of the ghost says what it is looking at: `Preview - rename, 4 changes` or
`Preview - convert to while, 2 added, 3 removed`. Without it the page is a document that appears to
have been edited, which is the one thing a preview must never look like.

**Removals are the hard case** and the plan is deliberately modest: the removed text has no position in
the preview layout, so it is *named* rather than drawn in place. A struck-through band would require
interleaving two layouts, which is a second kind of layout engine - and the badge plus the tooltip
answer the same question for a fraction of the machinery. If it turns out to be the missing piece in
practice, it is an addition to the ghost, not a change to the model.

## The trigger: a ladder, not a switch

Hovering an entry goes through three stages, and each one is allowed to end the ladder:

| Delay | What appears | Ends when |
| --- | --- | --- |
| 0 ms | the entry highlights | the pointer leaves |
| 300 ms | the description as a tooltip | the pointer leaves |
| 700 ms | the preview replaces the page | the pointer leaves, the list closes, the document changes, `Esc` |

Keyboard navigation uses the same ladder per highlighted entry, because a list walked with `Up`/`Down`
would otherwise flash a preview per entry. Leaving an entry cancels immediately; leaving the *list* to
move between the bulb and an entry has a 150 ms grace period so that a pointer on its way does not
cancel and restart the preview four times.

Everything about the ladder is headless: a `PreviewDelay` value object holding the three numbers and a
`PreviewState` machine that takes "hovered", "left", "ticked" and "invalidated" and answers "nothing",
"tooltip" or "preview". The control owns a timer and calls `Advance(elapsed)` - the same arrangement as
`CaretBlink`, which is why the timing is testable without a window.

## What a preview may not do

- **Not mutate the live model**, its journal or its version. A preview is not undoable because it never
  happened; a test asserts the journal is empty and `Version` is unchanged afterwards.
- **Not move the caret or the selection** of the live document, and not scroll it. The ghost is
  anchored to the caret's line, so the region the preview is about stays where the user was looking.
- **Not run anything with a side effect.** An intention that calls a service, opens a dialog or runs
  generation returns no action and is not previewable, so this is a consequence of the existing rule
  rather than a new one.
- **Not be the only explanation.** The label and the description stay, and they are what a screen
  reader and a keyboard-only user get; the preview is an extra, not the contract.
- **Not outlive its list.** Closing the list, detaching the control, or losing focus ends the preview
  and restores the page, the scroll offset and the selection exactly.

## What is not previewable, and what happens then

| Case | Answer |
| --- | --- |
| an intention that returns no action (a host command) | no preview; the entry keeps its label and description |
| an intention that declares itself not previewable | same |
| an edit that captures live `Node` objects rather than addresses | no preview; the preview catches the failure and reports "cannot be previewed", never an exception |
| a document above the copy budget | no preview, with the reason on the badge if a preview was already running |
| a model change during the delay | cancelled, and not restarted by the same hover - the user moves the pointer to ask again |

`Intention.IsPreviewable` is a declaration by the author, defaulting to true, with the sandbox failure
as the safety net. The framework cannot infer previewability: whether an action captured a node is a
property of its implementation.

## Slices

| Slice | Branch | M | Delivers |
| --- | --- | --- | --- |
| V1 ✓ | `feat/model-copy-and-diff` | M3 | `Model.CopyTo(Project)` (ids preserved) and `ModelDiff` by node id - added, removed, moved, property changed - with a snapshot-equality test and diff tests |
| V2 | `feat/preview-sandbox` | M3 | `PreviewSandbox.Open(session)`, applying one action to the copy, reading back tree and layout; tests that the live model, journal and version are untouched and that a failed preview leaves nothing |
| V3 | `feat/preview-delay` | M3 | `PreviewDelay`, `PreviewState`, the ladder and its cancellation, headless with a fake clock |
| V4 | `feat/preview-ghost` | M3 | `CellChange` marks from the diff, the ghost frame, the badge, the anchor and exact restore of scroll and selection; verified with a probe |
| V5 | `feat/preview-wire` | M3 | the popup and the menu ask the state machine, draw the ghost, and dismiss on every documented event |
| V6 | `feat/preview-sample` | M3 | previews for the sample intentions: rename in `Projectional.Samples`, wrap-in-parentheses and negate in `Projectional.Expressions` |

V1 and V2 hold the invariants and are testable without a window; V3 is headless by construction; V4 and
V5 are the first customers of the headless Avalonia harness the intention slices already need. A
preview of a *refactoring* that touches several files is M6 and needs nothing new but a scroll-and-tab
policy for the ghost.

## Testing

| Level | What it proves |
| --- | --- |
| copy | a copied document renders to the same snapshot as the original, with the same ids and order |
| diff | added, removed, moved, property-changed and unchanged nodes; a move is not a delete plus an insert |
| isolation | after a preview: live journal empty, `Version` unchanged, no undo step, selection and scroll identical |
| failure | an action that throws on a copy yields "no preview" and leaves the editor usable |
| budget | above the node budget no preview runs; a preview that overruns the clock is abandoned |
| timing | the ladder with a fake clock: tooltip at 300, preview at 700, cancelled by leave, by close, by a model change; the grace period |
| wiring | `Avalonia.Headless.XUnit` once it exists, with a probe standing in until then: the ghost replaces the page, the badge counts, `Esc` restores |

## Open questions

Rows 69-72 in [open-questions.md](../open-questions.md#intention-preview) are the parts of this document
that are choices rather than mechanics.

## What this plan deliberately leaves out

| Left out | Why |
| --- | --- |
| Editing inside the ghost | the ghost is a projection of a copy; letting the user type into it would need a second session, a second history and a rule for merging the two |
| Applying from the preview | choosing the entry applies it, and the preview disappears first; a second confirm step would make the common case two decisions |
| A side-by-side before/after view | the page *is* the after; a second pane doubles the layout and splits attention |
| A struck-through band for removed nodes | interleaving two layouts is a second layout engine; the badge and the tooltip answer the same question (see above) |
| Previewing a change across several files | M6, with the refactorings that need it |
| Previews of generated text or interpretation results | those are not edits to this model; they are the output panel's job |
| Storing a preview in the session | a preview is derived per hover, exactly like the intention list itself |

## Decisions this plan proposes

Frozen in [ADR 0017](../adr/0017-preview-is-a-discarded-edit.md) now, because the copy seam and the
diff are prerequisites that are expensive to add later:

1. A preview applies the edit to a discarded copy in a sandbox project; the live model is never touched.
2. The preview is a model diff by node id, never a text diff.
3. A preview is opt-in per intention and best effort: a failure yields no preview, never an exception.
4. The preview reuses a throwaway session, so the previewed path is the applied path.
5. The description stays; the preview is never the only explanation.
