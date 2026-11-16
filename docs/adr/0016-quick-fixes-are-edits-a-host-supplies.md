# ADR 0016 — Quick fixes are edits a host supplies, applied by the session

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR 0002](0002-model-as-source-of-truth.md),
  [ADR 0006](0006-change-event-backbone.md), [ADR 0013](0013-markers-are-view-state.md),
  [quick-fixes.md](../architecture/quick-fixes.md), [context-actions.md](../architecture/context-actions.md)

## Context

Diagnostics have existed since M1's marker slices, and nothing has made them actionable: the user
reads what is wrong and repairs it by hand. The repair, in a projectional editor, is not advice
but an edit - the editor knows which node a keystroke is about and owns every change as an
`EditAction`.

That leaves a set of shape questions which are cheap to answer before the affordances exist and
expensive afterwards, because every later surface (a key, a menu, a bulb, an intention list) would
otherwise invent its own answer:

- where a repair lives, and what it may be attached to;
- whether "cannot be applied" is a state or an absence;
- what happens to a repair that was offered for a document that has since changed;
- whether the offered list is stored or derived.

[quick-fixes.md](../architecture/quick-fixes.md) argues each question in full; slices Q1 and Q2
delivered the answers.

## Decision

**A quick fix is an edit that a host supplies for one finding, and the session applies it as one
undo step.**

| # | Decision | Why |
| --- | --- | --- |
| 1 | A fix produces `EditAction`s (one, or an `EditProgram` of several applied in one transaction). It never mutates the model itself. | The model has one gate ([ADR 0002](0002-model-as-source-of-truth.md), [ADR 0006](0006-change-event-backbone.md)); a repair that mutated directly would bypass the journal, the invariants and undo, and would not be testable headlessly. |
| 2 | The fixes are a separate host-supplied `FixSet` assigned to `EditorSession.Fixes`, keyed by the `Diagnostic` record. They are **not** a field on `Diagnostic` and **not** a field on `MarkerSet`. | [ADR 0013](0013-markers-are-view-state.md) keeps fixes out of the marker model, and the two readers of a marker set - a renderer and a hit test - have no use for a repair. Keying by the record, not by node, is what lets two findings on one node carry different cures and what stops a reworded finding from matching the repairs built for its old wording. |
| 3 | A fix is built per finding, so "not offered" is the absence of a fix. `FixAvailability` has two states, not three. | A repair that does not apply is simply not built by the check that found the problem. What remains worth saying is "listed but cannot run yet, and here is why". An intention needs an absent state because it is asked about a caret after it exists; a fix does not, so the two vocabularies still compose. |
| 4 | `Apply(DiagnosticFix)` re-resolves the pick on (finding, id) against the current set and runs the **current** object; a pick that no longer resolves is rejected with a reason. | A fix is a closure over the model of the moment its set was published. Running the caller's copy after an undo, a reload or a second run of the checks is how a repair lands on a document that has moved on. |
| 5 | The offered list is derived per call from the current sets; the sets are view state and are never persisted and never an undo step. | Same rule as the marker set and the context menu: anything cached across an edit can be stale, and nothing in this layer is allowed to hold a tree across a change. |

Two rules follow for implementers, and a pull request that breaks either is a bug:

1. **A capability enforced only by hidden UI is not a guarantee.** The same refusal has to be
   reachable from the command the UI calls, which is why rejection is expressed as an
   `EditResult` rather than as an entry that is simply not drawn.
2. **A fix may not do what a keystroke may not do.** It goes through the same `EditContext`
   operations, so the cell's own type stays the authority and the model's validation stays the
   only gate.

## Consequences

### Positive

- Every repair is undoable, testable without a window, and incapable of anything the model
  forbids - because it is an ordinary edit, not a privileged path.
- A host that runs checks supplies both the findings and the repairs through the public surface;
  the syntax highlighting of a language does not have to know that quick fixes exist.
- The intention list planned in [context-actions.md](../architecture/context-actions.md) inherits
  a working source, a re-resolution rule and `EditProgram`, instead of designing them from one
  guessed consumer.

### Negative / costs

- A fix carries a diagnostic, so the pair (finding, id) is the identity a caller holds. Renaming
  an id or rewording a finding invalidates a pick that a menu was still showing - which is
  handled, but it is a rule every producer of fixes has to respect.
- `EditProgram` cannot be applied by anything but a session, because the tree is rebuilt between
  steps. A caller holding an action graph has to route it through the session, and the type says
  so by throwing.
- Two sets have to be published instead of one. A host that forgets `Fixes` gets markers with no
  offers, which is a silent half-feature rather than an error.

### Rules

1. The fix list stays off `Diagnostic` and off `MarkerSet`.
2. `Apply` never runs a fix object the caller supplied; it resolves the current one first.
3. A fix that cannot run answers with a reason; it never silently does nothing.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **A `Fixes` list on `Diagnostic`** | Makes the vocabulary of "what is wrong" also the vocabulary of "what to do about it", and puts presentation policy in a record that a loader produces ([ADR 0013](0013-markers-are-view-state.md)). |
| **Fixes keyed by node** | Two findings on one node can have different cures, and "some fix for this node" is how a repair lands on a problem it was not built for. |
| **A third availability state (`Absent`)** | Nothing would ever set it: a fix that does not apply is not built. An unreachable state is a state that will be set wrongly later. |
| **Running the caller's fix object** | Cheapest to write and wrong the first time a user undoes an edit while a menu is open. |
| **Caching the line's list in the session** | One more thing that can disagree with the model; the list is one line's work over already-indexed sets. |
