# Quick fixes

A projectional editor knows which node a keystroke is about and owns every change as an
`EditAction`, so the repair for a problem is a value it can hand the user, not advice it prints.
This document is the design of the layer between a finding and the edit that removes it: what a
fix is, where it lives, how it reaches the user, and in which order the slices land.

Status: **in progress.** Slices Q1 to Q4 are delivered - the fix model, the fix set on the session,
the query that answers which repairs the caret's line offers, applying one as a single undo step
through [ADR 0016](../adr/0016-quick-fixes-are-edits-a-host-supplies.md), two real checks that carry
repairs, and the `Alt+Enter`/`Ctrl+.` gesture with its three answers. What a user still cannot see is
the list of several repairs. Q5 and Q6 are planned.
Markers are delivered for M1 (slices A-D in [markers.md](markers.md)); this feature is not the
constraint engine that will produce findings in volume at M4 - it is the half that makes the findings
that already exist actionable.

## A fix is an edit with a reason

The vocabulary has three words and only one of them is new:

| Word | What it is | Where it lives |
| --- | --- | --- |
| **Diagnostic** | what is *wrong*: a severity, the node, the role or index, a message, a code, an optional range | [semantics.md](semantics.md), immutable record |
| **Fix** | what to *do* about one diagnostic: a stable id, a label, one sentence, and a function that produces an `EditAction` | this document |
| **Intention** | the same shape sourced from a caret instead of from a diagnostic | [context-actions.md](context-actions.md) |

A fix is therefore not a callback hung on a diagnostic and not a command the editor runs behind
the user's back. It produces an `EditAction`, and it is applied through the session exactly as a
keystroke is, so it is one undo step, it is testable without a window, and it cannot do anything
the model does not allow. A fix that wants to open a dialog, call a service or run generation
returns no action: the host runs its own command from the same id, and the bulb is a launcher
rather than an editor.

That is the whole reason quick fixes come before the intentions that share their shape. Building
the general "action offered because of something" list first would invent its contract from one
consumer; this feature gives it a second consumer, and the contract can then be read off two real
sources instead of guessed.

## The fix list lives beside the marker set

[ADR 0013](../adr/0013-markers-are-view-state.md) states that fixes, quick-fix lists and severity
thresholds are **absent from the marker model**, because adding them there "would make the editor
decide policy". This document respects that decision rather than superseding it, which fixes the
shape of the new data:

- **No `Fixes` property on `Diagnostic`.** Putting them there would make the vocabulary of "what is
  wrong" also the vocabulary of "what to do about it" ([markers.md](markers.md)).
- **No fix field on `MarkerSet`.** The set is what a renderer and a hit test read; neither has any
  use for a repair.
- **A separate host-supplied `FixSet`, assigned to `EditorSession.Fixes`,** exactly as `Markers` is
  assigned today. A host that runs no checks assigns nothing and pays nothing.

The key is the **`Diagnostic` record itself**. Keying by node would answer "what is wrong here"
when the question is "what does this particular finding offer", and two findings on one node with
different cures would collide. Value equality on the record - severity, target, role, index,
message, code, range - is the honest key, because a check that rewrote its message has rewritten
the explanation it builds its fixes from, and the fixes it would build now are the ones that
belong with the message it would write now.

`FixSet` is immutable and order-preserving like `MarkerSet`, and it does one thing `MarkerSet`
deliberately does not: **two entries for one diagnostic are rejected**. Two fix lists for one
finding is a programming error, and silently keeping one hides it - the same strictness
`SyntaxPalette.Create` applies to a kind mentioned twice.

## What a fix carries

```csharp
public sealed class DiagnosticFix
{
    public string Id { get; }                  // "rules.duplicate-name.rename"
    public string Label { get; }               // "Rename to \"Check2\""
    public string? Description { get; }        // one sentence, shown as a tooltip
    public FixAvailability Availability { get; }
    public Func<FixContext, EditAction?> Create { get; }
}
```

- **The id is stable and never translated**, so a host can bind a key to one and a test can name one
  without matching a label that will be translated at M5.
- **The label may be computed per finding, not hardcoded.** "Rename to `Check2`" carries the suffix
  the fix would actually use, so the user sees the result before applying it. A fix is therefore a
  value *per finding*, not an entry in a static catalogue.
- **The description is part of the contract.** A label like "Rename" without a sentence is a riddle;
  the same rule as an intention's description.
- **`Create` may return a program** for a repair that takes several edits, or `null` for a
  diagnostic this fix has nothing to do about after all.

`FixContext` resolves the diagnostic's target once, so a fix does not hunt for its own node:

```csharp
public readonly record struct FixContext(Model Model, Diagnostic Diagnostic, Node? Node);
```

`Node` is `null` when the target is no longer in the project. That is the stale case, not an
exception: `Project.FindNode` answers `null` and the context carries the `Diagnostic` anyway, so a
fix for a node that was deleted can still decline cleanly instead of throwing inside a menu build.

## "Not offered" is the absence of a fix

`FixAvailability` has two states, not three:

```csharp
public readonly record struct FixAvailability
{
    public static FixAvailability Applies { get; }
    public static FixAvailability Disabled(string reason) { ... }
    public bool IsDisabled { get; }
    public string? Reason { get; }
}
```

An intention needs a third state (`Absent`) because it lives in a registry and is asked "do you
apply *here*?" after it exists. A fix does not: it is **built per finding** by the check that found
the problem, so a repair that does not apply is simply not built. What remains is the useful
distinction - a fix that applies, and a fix that is listed but cannot run *right now*, with the
reason the user needs. A `Disabled` fix is still shown, because "this concept knows about this
problem and here is why it cannot act" is the useful half of the message, and the diagnostic's own
message is close enough to stand in as the description when the fix has none of its own.

When a fix feeds the general intention list later, a fix maps to `Applies` or `Disabled` and never
to `Absent`, so the two vocabularies compose without a translation layer.

## Which fixes are offered

One question, two ways to ask it, both headless:

| Caller | Call | Answer |
| --- | --- | --- |
| the popup, the bulb, `Alt+Enter` | `QuickFixes.At(session)` | the fixes for the line the caret is on, the worst finding's first |
| a host that already has a finding in hand | `QuickFixes.Of(session, diagnostic)` | that finding's fixes, in the order the set holds them |

The line is resolved geometrically, because a document has no lines:

1. `CaretBounds.Of(...)` gives the caret's rectangle and therefore the top of the page line it is
   drawn on.
2. `MarkerGeometry.Of(layout, markers, measure)` gives one `MarkerLine` per marked stretch, each
   with the `Y` of the line it sits on.
3. A `MarkerLine` whose `Y` falls inside `[top, top + LineHeight)` is on the caret's line. Comparing
   `Y` rather than a line number is what makes a marker on a wrapped continuation line of a cell
   three cells to the left count as "this line" - which is the line the user is looking at.

Within that line, the **worst** marker is the one the bulb reports, by `MarkerSet`'s existing rule
(document order, then severity descending). Its fixes come first, then the fixes of the other
findings on the line in the set's own order. Order is fixed here rather than left to a dictionary,
because order is visible behaviour - the same reason [context-actions.md](context-actions.md) fixes
the source order.

Two rules carry over from everything else in this layer:

- **The list is derived per call and never cached.** Same rule as `EditorMenu.For` and the planned
  `IntentionList.For`: nothing survives an edit, an undo, a variant switch or a change the session
  did not make, so nothing can be stale. What makes it affordable is that it is one line's worth of
  work over sets that are already indexed.
- **No fixes means no affordance.** A finding whose check supplied no repairs offers nothing, and
  the bulb does not appear for it. That is why the catalogue is keyed by diagnostic: a miss is a
  real answer.

## Applying one is one undo step

```csharp
public EditResult Apply(DiagnosticFix fix);
```

`EditorSession` grows one method, and it does three things in this order:

1. **Re-resolve the fix against the current set.** The pick is joined on `(Diagnostic, Id)`, and the
   *current* object is what runs - never the one the caller passed. Everything a fix knows was
   computed against the model of the moment the set was published, and re-running an old closure
   after an undo is how a repair lands on a document that has moved on. This is also why the id must
   be stable per finding: it is the join key across recomputations.
2. **Reject a pick that no longer resolves** with `EditResult.Rejected(selection, reason)`, the same
   outcome a refused keystroke uses, so the control repaints and the host can say why. A finding
   that vanished because the user fixed it another way is the ordinary case here, not an error.
3. **Apply the action with the coalescing key cleared**, so a fix is one step and a following
   keystroke starts another. A repair that takes several edits returns an `EditProgram.Of(...)`,
   which the session runs inside one transaction; if any step is rejected the transaction rolls back
   and the result is that step's `Rejected`, so a half-applied fix never reaches the model.

`EditProgram` arrived with this slice; the intentions plan in [context-actions.md](context-actions.md)
then reuses it instead of introducing a second program type.

The model may change as a *result* of the fix - the node the diagnostic pointed at can be renamed,
moved or deleted. Nothing here republishes `Markers` or `Fixes`; the host owns its checks and is
already subscribed to `Model.Changed` (that is how `DefaultNames` stays current), so the next set
arrives through the path that already works. The session must not invent policy by editing a set it
was handed.

## The affordance, cheapest first

The four surfaces are delivered in dependency order, and only the last one needs a window:

1. **`Alt+Enter` and `Ctrl+.` in the control**, the two spellings of the same command in the two
   editors this one is modelled on (MPS and Visual Studio). If the line offers exactly one
   applicable fix it runs immediately - the common case is a single repair, and a popup with one
   entry costs the user a keystroke for nothing. Otherwise the list opens.
   Both are exceptions to "keys held with Control or Alt are the host's", and they are taken
   deliberately and early: a quick-fix gesture that moves after users have learned it is worse than
   one gesture reserved from the host on day one. [context-actions.md](context-actions.md) already
   names both for the intention list, and there must be one binding, not two.

   **Delivered as far as the editing layer goes.** `EditorKey.QuickFix` carries the gesture,
   `EditorKeyMap` maps both spellings to it, and `EditorSession.ApplyQuickFixes()` gives the three
   answers: nothing offered, one repair applied as its own undo step, or several held in
   `OfferedFixes` for whoever draws them. What is still missing is the drawing - the list is a value
   with no presenter yet, which is why the popup moved to slice Q5 with the menu rather than
   shipping half of it here.
2. **A "Quick fixes" submenu in the context menu.** Free: `EditorMenu.For(session)` is rebuilt on
   every opening and its entries are already `EditorMenuEntry`s that `EditorMenuPresenter` maps to
   the toolkit, so the fixes appear in the menu the user already knows before the popup, the bulb or
   a headless UI test exists.
3. **The bulb in the margin**, last, because it is the only part that needs geometry the control does
   not have yet: a glyph left of the page aligned with the caret's line, a hit test for it, and a
   repaint policy that does not flicker while the caret blinks. The editing layer supplies the
   geometry (`QuickFixBulb.BoundsOf(layout, caret, measure)`) and nothing about pixels, exactly as
   the intention plan already requires, so a second renderer can draw its own.

The tooltip closes an open thread at the same time. [markers.md](markers.md) says the tip does not
promise a fix it cannot deliver - the severity, the message and the code, and nothing else. Once
fixes exist the tip gains its fourth line: the number of fixes and the gesture, or nothing when the
finding has none. That is slice Q5 and it is a change in `MarkerTip`, not in the marker model.

## The first real checks

Two checks ship with the slice, and they carry the two claims this feature has to make: that a language
can supply checks with its own vocabulary, and that a host can supply them without any privileged
access.

| Check | Where | Diagnostic | Fix |
| --- | --- | --- | --- |
| two rules of one ruleset with the same name | `Projectional.Samples.Checks` | `RULES001` on the later rule, targeting its name | rename to the first free suffix (`Check2`, `Check3`, ...) - one property write |
| a name still carrying its concept's placeholder | `Projectional.Workbench/Checks/DefaultNames.cs` | `SHELL001`, unchanged | name it after its concept (`Rule1`) - one property write |

Neither needs scope resolution, type inference or a constraint engine: the first walks one child slot and
compares sibling names, the second compares a name against the concept's declared default. Both are
model-only edits, both are one property write, and both are undoable by the mechanism that already
exists.

**The check did not go inside the language package, and that is a correction to this plan.** A fix
produces an `EditAction` and a diagnostic, so a check needs `Projectional.Editing` and
`Projectional.Semantics` - but `Projectional.Samples` references only `Language` and `Expressions`
precisely so that a language can be used headlessly. Putting the check there would have made every
consumer of the sample drag in the editor. The shape that ships is therefore **a checks package beside
the language**: `Projectional.Samples` declares the language, `Projectional.Samples.Checks` declares what
is wrong with a model of it. That is also the recommendation for a real DSL, and it is open question 59
answered.

One thing fell out of doing it that way: the check answers with `IReadOnlyList<DiagnosticFix>` rather
than with diagnostics, because every finding it makes has a cure and a fix carries its own diagnostic.
The host derives the marker set from the fixes, so the two sets cannot describe different states of the
document - worth doing where it is available, and impossible where a check reports a problem it cannot
repair.

## Slices

Each slice is its own branch and its own merge, per the working rules.

| Slice | Branch | Delivers |
| --- | --- | --- |
| Q1 ✓ | `feat/quick-fix-model` | `FixAvailability`, `DiagnosticFix`, `FixContext`, `FixSet`, `EditorSession.Fixes`, `QuickFixes.At/Of`, tests for keying, duplicate rejection, order, disabled, and a marker on a wrapped line |
| Q2 ✓ | `feat/quick-fix-apply` | `EditorSession.Apply(DiagnosticFix)`, `EditProgram`, re-resolution by id, one-undo-step, stale rejection, rollback |
| Q3 ✓ | `feat/quick-fix-sample` | `RULES001` and its rename fix in `Projectional.Samples.Checks`, a fix for the workbench's `SHELL001`, both published to the editor at open and on `Model.Changed` |
| Q4 ✓ | `feat/quick-fix-keys` | `Alt+Enter`/`Ctrl+.` in `EditorKeyMap` and `KeyRouter`, run-when-single, and the list of several as `OfferedFixes`; verified in a real window with a probe |
| Q5 | `feat/quick-fix-popup` | the popup that draws `OfferedFixes` (a presenter beside `CompletionPresenter`, with its own row hit test and keyboard navigation), the "Quick fixes" submenu in `EditorMenu`, and the fix line in `MarkerTip` |
| Q6 | `feat/quick-fix-bulb` | `QuickFixBulb.BoundsOf`, the margin glyph, its hit test, its repaint policy, and the bulb showing for a marked line |

Q1 and Q2 hold the invariants and are testable without a window; Q4 to Q6 are presentation and are
the first customers of the headless Avalonia test project, with a probe standing in until it exists.

After Q6 the intentions plan continues at `feat/intentions-model` with a source already in place,
and its old slice F (`feat/quick-fixes`) disappears - the general list now starts with a working
source instead of ending with one.

## Decisions this plan proposes

Frozen in [ADR 0016](../adr/0016-quick-fixes-are-edits-a-host-supplies.md) when Q2 landed, not
before - the text above is the argument:

1. A fix produces `EditAction`s applied by the session as one undo step; it never mutates the model.
2. The fix list is a separate, host-supplied `FixSet` keyed by the `Diagnostic` record, beside the
   marker set rather than on it or on the diagnostic.
3. A fix is built per finding, so "not offered" is the absence of a fix rather than a third
   availability state.
4. `Apply` re-resolves on `(Diagnostic, Id)` against the current set and refuses a pick that no
   longer resolves, so a closure over a stale model never runs.
5. The list is derived per call; the set is view state - never persisted, never its own undo step.

## What this plan deliberately leaves out

| Left out | Why |
| --- | --- |
| The constraint engine that produces findings in volume | M4; this feature makes the findings that exist actionable first |
| Fix all / batch repair (`Ctrl+Shift+.`) | one transaction over many fixes needs a policy for repairs that overlap or contradict, and that policy is not a side effect of one fix |
| Preview or diff of what a fix will do | the label carries the result where it matters ("Rename to `Check2`"); a repair is previewed by the intention preview instead, through the same sandbox ([intention-preview.md](intention-preview.md)) |
| Fixes on save, or automatic repair | the model changes only when the user says so; MPS' transformation triggers are the substitution actions of M3 |
| Severity thresholds ("show warnings only") | policy that belongs to the host that runs the checks, per ADR 0013 |
| Localisation of labels and descriptions | strings until the M5 message-key pass |
| Ranking by usage | an unexplainable order; the fixed order is debuggable |

## Testing

- **Model and resolution, headless.** `FixSetTests`: order kept, duplicates rejected, `For` answering
  empty for a finding nobody repaired. `QuickFixesTests`: the worst finding's fixes first, a marker on
  a wrapped continuation line, a marker on the line above not being offered, a disabled fix still
  listed, a finding with no fix offering nothing.
- **Applying, headless.** `EditorSessionFixTests`: one undo step, undo restoring the old name, a pick
  whose diagnostic disappeared rejected with a reason, a fix re-resolved to the current object after
  the set was republished, a two-step program that rolls back completely.
- **Language, through the samples.** `RULES001` reported on the second of two same-named rules and
  not on the first, and applying its fix leaving two distinct names.
- **Control, when `tests/Projectional.Avalonia.Tests` exists.** `Alt+Enter` with zero, one and three
  fixes; the popup closing on `Esc`; the menu's "Quick fixes" submenu disabled when the line has none.
  Until then a probe drives the real window, because key events are raisable and pointer events are
  not.

## Open questions

Rows 31 and 32 in [open-questions.md](../open-questions.md) settle the order of the sources and the
bulb's trigger; rows 58 to 60 are the ones this document opens.
