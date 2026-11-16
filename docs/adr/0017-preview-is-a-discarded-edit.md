# ADR 0017 — A preview is a projection of a discarded edit

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR 0002](0002-model-as-source-of-truth.md), [ADR 0004](0004-stable-node-ids.md),
  [ADR 0009](0009-custom-drawn-editor-control.md), [ADR 0016](0016-quick-fixes-are-edits-a-host-supplies.md),
  [intention-preview.md](../architecture/intention-preview.md),
  [context-actions.md](../architecture/context-actions.md)

## Context

An intention is offered by its label and one sentence of description. For a rename that is enough;
for a conversion - a `for` becoming a `while`, a rule becoming a table row - the user has to imagine
the result before choosing, which is exactly what a projectional editor is good at showing.

Two documents had already decided the opposite, for the same reason:

- [context-actions.md](../architecture/context-actions.md) left "preview / diff of what an intention
  will do" out of scope because it "needs a side-effect-free describe form per action".
- [quick-fixes.md](../architecture/quick-fixes.md) left the same thing out with the same argument.

The reason was sound and the conclusion is wrong. "Describe what you would do" asks every action
type to grow a second implementation that must agree with the first one forever - a description that
drifts from the edit is worse than no preview. But there is a second way to answer "what would this
do", and it needs no second implementation at all: **do it, to a copy.**

That is possible only because of decisions this project has already taken: the model is the only
source of truth and there is no text buffer ([ADR 0002](0002-model-as-source-of-truth.md)), node
identities are stable ([ADR 0004](0004-stable-node-ids.md)), and every change is an `EditAction`
value rather than a method call on a live object ([ADR 0016](0016-quick-fixes-are-edits-a-host-supplies.md)).
An action addresses ids and slots, so applying it to a copy of the document is meaningful - it is
the same edit, against the same identities, on a model nobody is looking at.

## Decision

**A preview applies the intention's edit to a discarded copy of the document, projects that copy,
and shows the difference - the live model is never touched.**

| # | Rule | Why |
| --- | --- | --- |
| 1 | The edit runs in a **sandbox**: a copy of the document in its own `Project`, thrown away afterwards. | Node ids are unique per project, so a copy carrying the same ids cannot live in the live project. A separate project makes that a fact of the type rather than a convention. |
| 2 | A preview **never** mutates the live model, its history or its journal, and produces no undo step. | A preview is a question, not an edit. Anything visible in the journal would make "look at it" and "do it" indistinguishable afterwards. |
| 3 | The preview is a **model diff by node id**, never a text diff. | A text diff would be a second opinion about what the model says, and it cannot name a move, a slot change or an added child. Ids make the comparison exact and cheap. |
| 4 | The preview is **opt-in per intention and best effort**: an intention declares that it can be previewed, and a preview that fails yields no preview, never an exception. | Some intentions are host commands or capture live nodes; a preview must never be the reason the editor breaks. |
| 5 | The **description stays**. The preview is an enhancement, never the only explanation of what an intention does. | It is available only to a pointer that hovers long enough, so it cannot be the only way to learn the effect. |

The preview reuses one throwaway `EditorSession` over the copy to apply the action, so the code path
a preview exercises is the code path that runs when the user applies it - if the preview shows a
result, applying the intention produces that result.

## Consequences

### Positive

- No action ever grows a second "describe" implementation, and a preview cannot drift from the edit
  it previews: it *is* the edit.
- The same machinery previews quick fixes, substitutions and refactorings, because all of them are
  `EditAction`s.
- The diff by id is a type the rest of the product wants anyway: model-aware merge (M6), a
  "what changed" view and a better undo description all read the same structure.

### Negative / costs

- Every preview costs a model copy. That is linear in the document and has to be bounded, so the
  feature needs a budget and a declared "too large to preview" answer.
- A previewable action has to be address-based. An action that captured live `Node` references would
  address them in a copy that does not contain them - so previewability is a property an intention
  author declares, not something the framework can infer.
- Two projections of one document exist while a preview is visible, and the ghost replaces the live
  page, so the control has to restore scroll and selection exactly when the preview ends.
- The diff, the sandbox and the ghost are three new concepts in a layer that had two.

### Rules

1. A preview may read the live model and must write only the sandbox.
2. A preview that throws is caught, reported as "no preview", and leaves the editor as it was.
3. Nothing about a preview is persisted, and no preview may outlive the popup that owns it.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Apply on the live model, then roll back** | Every subscriber sees the change and its inverse, the caret and the tree are rebuilt twice, and a failure leaves a half-applied document behind. The journal would also gain a step unless the rollback path is perfect - and "perfect" is not a property to bet the editor on. |
| **A side-effect-free `Describe()` per action** | A second implementation per action type, which is exactly the drift this project avoids elsewhere; it also cannot describe a structural change without inventing a notation for it. |
| **A text diff of generated text** | Text is not the model ([ADR 0002](0002-model-as-source-of-truth.md)); a move would read as a deletion plus an insertion, and a language with no generator would have no preview at all. |
| **Preview in a separate window or dialog** | Splits attention between the entry being chosen and the result, and the result is *this* document at *this* place; a dialog would need its own projection, layout and scroll. |
| **No preview, better labels** | The label is already as good as a label can be. For a conversion the user's question is what the code will look like, which no sentence answers. |
