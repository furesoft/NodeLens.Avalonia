# Intention preview

What an intention would do, shown before it is applied: hovering an entry for long enough replaces the
page with the result of that edit, marks what changed, and puts everything back when the pointer leaves.
For a rename that reads as the new names in place; for a conversion - a `for` turned into a `while`, a
rule turned into a table row - it is the only honest answer, because the question "what will this look
like" has no good sentence.

Status: **in progress.** The intention list it needs exists ([context-actions.md](context-actions.md),
slices A-E) and slices V1 (copy and diff), V2 (the sandbox), V3 (the ladder), V4 (the ghost, painted by the
control) and V5 (the wiring: hover tracking, the timer and the dismissals) are delivered; what is left is V6
(previews for the sample intentions). The decision it rests on is
[ADR 0017](../adr/0017-preview-is-a-discarded-edit.md).

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

The marks and the badge are headless, and they are where the mapping lives - a diff names *nodes* and
*slots*, a ghost is drawn as *cells*:

- **`PreviewMarks.Of(diff, ghost)`** turns a `ModelDiff` into `CellChange` values. A node difference
  marks every cell whose `CellAddress.Owner` is that node (a new node is drawn as several cells); a slot
  difference marks exactly the cell that shows that slot. Ownership, not address equality, for the same
  reason the caret uses it.
- **A removal is marked where it used to be.** The node has no cells in the ghost, so the mark goes on the
  slot cell of the parent it was taken out of, with the removed text in `CellChange.OldValue`. Only the
  outermost node of a removed subtree has such a place, which is why the diff may name three removals and
  the ghost show one mark.
- **Counts count nodes; marks count cells.** "Seven added" because a rule happens to be drawn with seven
  cells would be counting the renderer. A difference whose node is not drawn at all - no notation, a hidden
  slot - is counted and not marked, so the badge stays honest about an edit the ghost cannot show.
- **`PreviewMarks.CellBudget` (2 000)** keeps a large diff drawable: past it the first marks are kept and
  `Truncated` says how many were left out, which the badge appends as "N marks not drawn".
- **`PreviewBadge.Of(label, marks)`** owns the sentence, because it is user-facing text with grammar in it:
  one kind of change reads as a total ("4 changes"), several are broken down ("2 added, 3 removed"), and
  one change is singular. The label is the entry's own, so the badge cannot disagree with the list.
- **`GhostFrame.Build(sandbox, marks, label, viewport, offsetX, offsetY)`** is the frame a host paints: the
  copy's text runs, the marks to look up per run, and the badge. It takes the **live page's offsets**, which is
  what anchors the preview to the line the user is looking at, and it carries **no caret and no selection** - a
  preview is not editable and must not look as if the document had a caret somewhere else.
- **"Unchanged" is not a mark, it is the absence of one.** `GhostFrame.IsDimmed(run)` is true exactly when no
  mark names that cell, which is the rule "the marks read first" written as code rather than as a palette
  decision. `PreviewMarks.ChangeAt(address)` is the indexed lookup behind both, and the first mark for an
  address wins - every removal out of one slot names that slot's cell.

Painting it is the control's half, and it is deliberately dumb:

- **`ProjectionalEditorControl.Ghost` is settable by the host.** While a `GhostFrame` is set the control paints
  *that* instead of the page - tints, dimmed text, badge - and nothing else: no gutter numbers, no bulb, no
  selection, no caret, no lists, no tips, because none of those describe the copy. The host decides *when*,
  which is what keeps the hover ladder out of the control.
- **`GhostPresenter` renders a frame; `PreviewPalette` names the colours.** A ghost is a whole-surface painter,
  not an overlay, because the ghost replaces the page. `PreviewPalette` (added/changed/removed tints plus the
  dim colour) is part of `SyntaxTheme`, like `Tip` and `Markers`, so a themed page implies a themed preview -
  a host that themes the document but not the ghost ships a light sheet on a dark background.
- **The runs are layout coordinates; the presenter converts with the frame's own viewport and the gutter.** The
  first implementation passed layout coordinates straight to `DrawingContext`, so every tint landed in the
  corner - the picture is what caught it.

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

Everything about the ladder is headless, and it is two types rather than a timer in the control:

- **`PreviewDelay`** holds the four durations - highlight, description, preview and grace - as one immutable
  value, with `IsValid`/`Validate` for a host that builds it from settings. `PreviewDelay.Default` is the table
  above. A duration may be zero (that rung is then immediate) but the rungs may not be out of order, because a
  ladder that shows the answer before the question is a bug and not a preference.
- **`PreviewState`** is the machine: `Hover(entryId)`, `Leave()`, `LeaveList()`, `Invalidate()` and one
  `Tick(elapsed)` per timer tick, answering `Stage` as `PreviewStage.Nothing`, `Tooltip` or `Preview`. The entry
  key is the `Intention.Id`, so the machine knows nothing about intentions or about a toolkit. The control owns
  the timer and calls `Tick` - the same arrangement as `CaretBlink`, which is why the timing is testable without
  a window.

The rules the machine encodes, each of which is a case that would otherwise be a flicker:

- **A different entry starts from nothing.** A preview is an answer about *that* edit and is never left standing
  while the next entry is considered.
- **The same entry again continues the clock.** That is what the pointer does when it crosses the gap between the
  bulb and the list, so the grace period is not a pause for its own sake.
- **Leaving an entry is immediate; leaving the list is not.** The preview survives `Grace` so a pointer on its
  way does not cancel and restart it, and the clock is *paused* while the pointer is away - coming back resumes
  the rung that was showing rather than arriving at the next one.
- **A document that changed under a hover stays silent.** `Invalidate` remembers the entry, so the ladder does not
  restart on the next tick; only leaving and returning asks again. That is "the user moves the pointer to ask
  again" written as a rule.
- **Late ticks are not lost.** The stage follows how long the entry has been hovered, not how often the host
  ticked, so a busy second does not leave a tooltip showing for another second.

## Driving the ladder

`OfferPreview` (`Projectional.Editing/Preview/`) is the one place the three ingredients are joined, because a
preview is a property of the *list* rather than of one affordance: the bulb, the popup and a context-menu submenu
all show the same list, so all three get their answers from one implementation instead of three. It owns no
timer and no pixels - a host says `Hover`, `Tick` and `Ghost`, passing its own viewport and scroll offsets, which
is what anchors the preview to the line the user is reading.

The rules that make the join honest, each of which is a bug if it is "simplified" away:

- **The copy is opened lazily, on the rung that asks for it.** A pointer crossing a row is not a request to copy a
  document, so `Hover` only starts the clock and `Tick` - on reaching `Preview` - is what opens the sandbox.
- **A failure silences the ladder instead of retrying it.** No action, an edit that throws on the copy, or a
  document over the copy budget all answer "no preview", and the entry stays quiet until the pointer leaves and
  comes back. Retrying per tick would copy a twenty-thousand-node document thirty times a second.
- **A frame is kept until its question changes.** The viewport, the two offsets and the styler identify it, so a
  repaint per caret blink never re-projects the copy.
- **The host owns the clock.** The control runs a 30 ms `DispatcherTimer` while an entry is hovered and stops it
  when none is - a document nobody is pointing at costs no timer. Nothing below the control asks what time it is,
  which is why the timing needs no window to test.

The control's half is one statement plus the pointer tracking that feeds it:

- **Hovering a row moves the highlight**, because the first rung of the ladder is the list's own hover state: a
  row that is previewed but not highlighted would describe one entry while looking as if it described another.
- **A pointer between rows gets the grace period, never an immediate cancel** (`LeaveList`), which is what lets it
  travel to and from the bulb without the preview flickering.
- **An arrow key is the keyboard's version of hovering**: `SelectOfferAction` moves the highlight and the entry it
  landed on starts the ladder, so a list walked with `Up`/`Down` does not flash a preview per row.
- **`ShowPreview()` is the only place the ghost is set or cleared**, and every path that can end a hover reaches it
  - an edit or a caret move (`Show`), the pointer leaving the surface, a republished marker set (a document that
  changed under a hover), a closed list, and a detached control. One statement is what keeps "a preview never
  outlives its list" true as paths are added.
- **Only the ghost the control set is taken back.** A host that assigned its own `Ghost` is answering a different
  question, and the ladder ending must not overwrite it.
- **The middle rung is painted too**: the entry's sentence appears under the list box, not under the row, because
  a row's screen position depends on the window the list scrolled to. A label says what an entry is called; the
  sentence is what it would do.

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
| V2 ✓ | `feat/preview-sandbox` | M3 | `PreviewSandbox.Open(session)`, applying one action to the copy, reading back tree and layout; tests that the live model, journal and version are untouched and that a failed preview leaves nothing |
| V3 ✓ | `feat/preview-delay` | M3 | `PreviewDelay`, `PreviewStage`, `PreviewState`, the ladder and its cancellation, headless with a fake clock |
| V4 ✓ | `feat/preview-ghost` | M3 | `CellChange` marks from the diff, the ghost frame, the badge, the anchor and the painting - marks and badge in `feat/preview-marks`, frame and anchor in `feat/preview-ghost`, `Ghost`/`GhostPresenter`/`PreviewPalette` in `feat/preview-paint`; verified with a probe that renders the ghost |
| V5 ✓ | `feat/preview-wire` | M3 | `OfferPreview` joins the ladder to the sandbox and the ghost in `Projectional.Editing`, and the control owns the timer, tracks the row under the pointer, moves the highlight with it, paints the description under the list and drops the ghost on every documented event |
| V6 | `feat/preview-sample` | M3 | previews for the sample intentions: rename in `Projectional.Samples`, wrap-in-parentheses and negate in `Projectional.Expressions` |

V1 and V2 hold the invariants and are testable without a window; V3 is headless by construction (it never asks what time it is); V4 and
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
| timing | the ladder with a fake clock: tooltip at 300, preview at 700, cancelled by leave, by close, by a model change; the grace period - `PreviewStateTests`, plus `PreviewDelayTests` for the numbers themselves |
| marks | `PreviewMarksTests`: an added node marks the cells it owns, a rewritten property marks its own cell with the old value, a removal marks the slot it left, the budget truncates and counts, an undrawn difference is counted and not marked - `PreviewBadgeTests` for the grammar |
| frame | `GhostFrameTests`: the ghost draws the copy and not the page, a touched cell is not dimmed while every other one is, a wrapper marks its own text, the badge says what happened, and the ghost takes the page's (clamped) offset instead of choosing its own view |
| join | `OfferPreviewTests`: hovering copies nothing, the copy is taken on the preview rung, the ghost says what the copy says while the live model's value, version, history and drawing stay as they were, leaving drops the ghost, the grace period keeps it and then drops it, an entry with no action and an edit that throws are both "no preview", a changed document silences the entry until the pointer returns, and a frame is reused until its viewport or offset changes |
| wiring | a real-window probe, since the control cannot be constructed headlessly: hovering a row puts the ghost on screen with the copy's change, another row previews another edit, the description appears under the list, `Esc`, a pointer leaving and an edit each restore the page - verified for `feat/preview-wire`, which is also the slice that made `OfferPreviewTests` prove the join itself |

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
