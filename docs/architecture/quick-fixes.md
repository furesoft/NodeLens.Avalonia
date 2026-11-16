# Quick fixes

A projectional editor knows which node a keystroke is about and owns every change as an
`EditAction`, so the repair for a problem is a value it can hand the user, not advice it prints.
This document is the design of the layer between a finding and the edit that removes it: what a
fix is, where it lives, how it reaches the user, and in which order the slices land.

Status: **delivered.** Slices Q1 to Q8 are done - the fix model, the fix set on the session,
the query that answers which repairs the caret's line offers, applying one as a single undo step
through [ADR 0016](../adr/0016-quick-fixes-are-edits-a-host-supplies.md), two real checks that carry
repairs, the `Alt+Enter`/`Ctrl+.` gesture that always opens the list, the keyboard-navigable list, the
context-menu submenu, the tip line, the painted popup and the bulb in the margin. Slice Q7 then added
the safety class the catalogue needed and shipped the catalogue itself as `Projectional.Checks`, so a
language author includes the standard checks by naming them.
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
    public EditSafety Safety { get; }          // Plain or Destructive - see the two rules below
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
   editors this one is modelled on (MPS and Visual Studio). The gesture always opens the list - for one
   offer as much as for ten. It used to apply a lone plain repair immediately, and that shortcut was
   removed: one keystroke that sometimes acts and sometimes asks is a keystroke the user has to guess
   about, and the meaning would change as soon as a language declared a second offer. The gesture asks
   what is available, and *only* that.
   Both are exceptions to "keys held with Control or Alt are the host's", and they are taken
   deliberately and early: a quick-fix gesture that moves after users have learned it is worse than
   one gesture reserved from the host on day one. [context-actions.md](context-actions.md) already
   names both for the intention list, and there must be one binding, not two.

   **Delivered as far as the editing layer goes.** `EditorKey.QuickFix` carries the gesture,
   `EditorKeyMap` maps both spellings to it, and `EditorSession.ApplyOffers()` gives two answers:
   nothing offered, or a list held in `Offered` for whoever draws them - one entry or several, the
   gesture never applies an offer by itself. See
   [context-actions.md](context-actions.md#interaction). Q7 added the safety class that decides the second and third
   case, and Q8 (`OfferPresenter`) draws them beside the caret - a list of one included, because a
   destructive offer that opened it is asking rather than offering. **Slice C of
   [context-actions.md](context-actions.md) turned the same gesture into the intentions gesture**:
   the list is `IntentionList.For(session)`, so it holds the host's commands and the language's
   intentions as well, and each row is an `Intention` (a repair is one carrying its `DiagnosticFix`).
   The names that survive are the ones about the *affordance* - the key, the bulb, the painter - while
   everything about the *list* is an offer.
2. **A "Quick fixes" submenu in the context menu.** Free: `EditorMenu.For(session)` is rebuilt on
   every opening and its entries are already `EditorMenuEntry`s that `EditorMenuPresenter` maps to
   the toolkit, so the fixes appear in the menu the user already knows before the popup, the bulb or
   a headless UI test exists.

   **Delivered.** One submenu at the top, present only when the line offers something, with a
   disabled repair keeping its reason - which is why `EditorMenuItem` grew a `Description` rather
   than leaving the user to guess why an entry is grey. Since slice D of
   [context-actions.md](context-actions.md) there are two submenus, "Quick fixes" and "Intentions",
   both built from the one `IntentionList` so the menu and the popup cannot disagree about what
   exists or in which order - a fix in one, an intention in the other, the fixes first.
3. **The bulb in the margin**, last, because it is the only part that needs geometry the control does
   not have yet: a glyph left of the page aligned with the caret's line, a hit test for it, and a
   repaint policy that does not flicker while the caret blinks. The editing layer supplies the
   geometry (`QuickFixBulb.BoundsOf(layout, caret, measure)`) and nothing about pixels, exactly as
   the intention plan already requires, so a second renderer can draw its own.

   **Delivered.** `QuickFixBulb.BoundsOf` answers a one-line square whose `X` is **negative** - the
   bulb is outside the page - so a renderer translates it into whatever margin it reserved. The
   control reserves `LineHeight + Gap` device pixels in `GutterWidth` whenever `ShowBulb` is on, and
   reserves it **whether or not a line offers anything**: a margin that appeared on demand would shift
   every cell sideways, and a page that jumps because the caret moved onto a marked line is worse than
   a blank that is sometimes empty. The glyph is two primitives (a circle and its stem) rather than a
   font glyph, so it cannot depend on the host's font family; the hit test uses the same rectangle the
   painter uses, and it runs before the document's own hit test so a press on the bulb does not also
   move the caret. "Does this caret have anything to offer" is kept between paints, keyed on the layout,
   the caret and both sets, because the caret blinks and a repaint per tick must not walk the document.
   Since slice C of [context-actions.md](context-actions.md) it asks `IntentionList.For(session)`, so a
   language's intention lights the bulb exactly as a finding's repair does.

   The bulb is a target, so the pointer says so: hovering it sets `Cursor` to `Hand`, exactly as a fold
   twisty does, and the control resolves both from the one place that also answers the press
   (`BulbOnScreen(session)`, `FoldAt(point)`). That matters more here than in the twisty column, because
   the bulb shares its margin with the row numbers and their blank space, neither of which reacts to a
   press - without a distinct shape the only sign that the glyph is a button is the colour of a circle.
   The rest of the gutter stays an arrow and the page stays an I-beam, which is what
   [editing.md](editing.md) promises for the surface as a whole.

The tooltip closes an open thread at the same time. [markers.md](markers.md) said the tip does not
promise a fix it cannot deliver - the severity, the message and the code, and nothing else. With fixes
existing, the tip gained that line: `1 quick fix - Alt+Enter` or the plural, and nothing at all for a
finding nobody repaired, because "0 quick fixes" is a sentence that exists to say nothing. It is a
change in `MarkerTip` and not in the marker model, and the control keys its text cache on the counts
so a republished set cannot leave a stale sentence on screen.

## How the list is painted

`OfferPresenter` (`Projectional.Avalonia/Offers/`) draws an open `Offered` onto the control's
own surface, exactly as `CompletionPresenter` draws the proposal list. Five decisions are worth
keeping, because each of them is a place where the obvious alternative is wrong:

| Decision | Why |
| --- | --- |
| **One palette for both lists** (`CompletionPalette`), not a second one. | A list drawn on the document is one kind of surface. Two palettes for two boxes would have to be kept looking alike by hand, and a host that themes one would forget the other. |
| **One placement helper** (`ListBounds`) for both lists. | Both belong to the caret's line, both flip above when they would hang off the bottom, and both are clamped into the control. Two copies of that arithmetic would eventually flip at different heights. |
| **A disabled offer is dimmed, not hidden.** | `FixAvailability` exists so the user learns *why* something is not offered; the menu has room for the reason, the popup does not, so it says "not this one" and the menu says why. A disabled intention dims the same way, because `IntentionAvailability` keeps the same reason. |
| **A click is select-then-accept.** | The same two actions the keyboard runs, so a row cannot be taken through a path the keyboard would have refused - including the edit layer's own refusal of a disabled offer. |
| **A caret that moves closes the list.** | The list describes one caret. It is drawn beside the caret, so leaving it open while the caret moves would show that place's offers over a different one. `EditorSession.Select` therefore clears it, beside the `Completion` it already cleared. |

The popup is painted **after** the completion list and **before** the marker tip, which is the same
"what covers what" order the two of them already had: the list belongs to the caret, the tip belongs
to the pointer, and the pointer wins because it is what the reader is looking at.

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

## Standard checks a language can include

Most languages have the same small problems, and none of them needs to know what the language *means*.
That is the filter for what can ship with the framework: **a standard check is one that can be computed
from the declarations and the model's own structure** - the name slot, the child slots, the property
types, the reference targets, the `resolveInfo` a reference keeps - and never from an analysis the
language alone could do.

A check that passes that filter should not be written again per language. The catalogue below is what
the framework can provide; a language includes the ones it wants and inherits their repairs.

### Names: whatever declares a name slot

| Id | The finding | The repair | Class |
| --- | --- | --- | --- |
| `STD001` | the name still equals the concept's declared default | `Name it "Rule1"` | plain - **shipped** in `Projectional.Checks`; the workbench's `SHELL001` was the first hand-written copy of it |
| `STD002` | two children of one slot share a name | `Rename to "Check2"` | plain only while nothing references the name - see the rule below |
| `STD003` | the name has leading or trailing whitespace | `Trim to "Rule"` | plain - **shipped** |
| `STD004` | the name breaks the identifier rule the concept declares | `Use "myRule"` | plain |

`STD004` needs one new declaration rather than a new mechanism: a concept says what a legal name is
(`NameRule.Identifier`, `NameRule.AnyText`), and the check reads it. Without that declaration the check
is not offered, which is the honest default for a language whose names may be sentences.

### Structure: whatever the declarations already say

| Id | The finding | The repair | Class |
| --- | --- | --- | --- |
| `STD010` | a single-child slot is empty | `Insert "Rule"` | plain - **shipped**; the same concept Enter would create, from `DefaultInstantiableFor` |
| `STD011` | a list holds fewer children than its declared minimum | `Add the first rule` | plain - needs the minimum of open question 64 |
| `STD012` | a node holds nothing but its defaults | `Delete the empty rule` | **destructive** |

`STD010` and `STD011` are the two fixes a user meets most often in a fresh document, and both are one
`InsertChildAction` away from what the keyboard already does - which is the argument for shipping them:
the fix and the keystroke cannot disagree, because they are the same action (`InsertNodeAction` runs it
too).

### References: whatever a reference slot knows

| Id | The finding | The repair | Class |
| --- | --- | --- | --- |
| `STD020` | a reference does not resolve, but kept a `resolveInfo` | `Point to "X"`, one per candidate from the scope | plain - but only after the user picks |
| `STD021` | a reference does not resolve and no candidate is close | `Clear the reference` | **destructive** |
| `STD022` | a reference points outside the model | `Remove the reference` | **destructive** |

`STD020` is the one that pays for the scope chain being a language aspect already: the candidates are
`IScope.CandidatesOf`, ranked by how close each name is to the `resolveInfo`. It is also the fix that
most clearly needs several offers rather than one, which is what `Offered` exists for.

### Values: whatever the property type knows

| Id | The finding | The repair | Class |
| --- | --- | --- | --- |
| `STD030` | the text is not a value of the property's type | `Use the default` | plain |
| `STD031` | a number written with spaces or a comma as the decimal mark | `Use "1000"` | plain |
| `STD032` | a date in another format | `Use "2026-11-16"` | plain |

These three are the closest thing to a formatter the framework should own: they are deterministic,
local, and they cannot change what the model means.

### Expressions: shipped with `Projectional.Expressions`

| Id | The finding | The repair | Class |
| --- | --- | --- | --- |
| `STD040` | an operand grouped by precedence rather than by the drawing | `Wrap in parentheses` | plain - **shipped**, in the expression group |
| `STD041` | a negation of a negation | `Remove both` | plain |
| `STD042` | an operator that is not one of the declared symbols | `Use ">="` | plain |

A language that composes the expression sub-language gets these for free, which is the same argument
that makes the sub-language itself worth shipping. `STD040` is the one check here that needs an operator
table, and it keeps it: `ExpressionOperators` says precedence is not part of the language, so the
analyser owns it. The finding names the *operand* whose grouping is not drawn, and the repair wraps that
operand - wrapping its parent would again mean the same finding survives its own repair.

### Loading: whatever the reader had to preserve

| Id | The finding | The repair | Class |
| --- | --- | --- | --- |
| `STD050` | a node whose concept the language does not know, preserved by the loader | `Delete the Foo node` | **destructive** |
| `STD051` | a property the concept does not declare, preserved by the loader | `Remove the property` | **destructive** |

These exist because loading never drops data: an unknown concept and an unknown property survive a load
and become diagnostics, so the only way to get rid of them is a repair. Deleting them is the whole fix -
anything cleverer would guess what the author of the other model meant.

### How a language includes them

A standard check is a *class parameterised by the language's declarations*, never by its code. The
package is `Projectional.Checks`; `ICheck` is one rule, `CheckSet` is the ordered list a language
publishes, and `StandardChecks` is the catalogue grouped by subject:

```csharp
// The language's own publishing point, written once.
public static CheckSet Checks { get; } = StandardChecks.All.With(new ExactlyOneAcceptRule());

// Or, when it wants some of the catalogue and not all of it:
public static CheckSet Checks { get; } =
    new CheckSet([.. StandardChecks.Names, .. StandardChecks.Structure, new ExactlyOneAcceptRule()]);
```

A host runs the whole set with one call - `checks.Find(model)` - and derives its marker set from the
fixes it gets back, exactly as it does for a check of its own.

That shape is what keeps the catalogue a library rather than a policy: `STD001` reads the name slot from
the concept, `STD010` reads the child slot's concept, `STD020` reads the scope. A language that declares
none of the slots a check needs simply does not get that check, and nothing has to be switched off.

### What Q7 shipped

| Id | Where | What it needed |
| --- | --- | --- |
| `STD001` | `PlaceholderNameCheck` | nothing beyond the declarations |
| `STD003` | `SurroundingWhitespaceCheck` | nothing beyond the declarations |
| `STD010` | `EmptyChildSlotCheck` | `InsertChildAction`, the address-based half of Enter |
| `STD040` | `BinaryGroupingCheck` | an operator table of its own, and `WrapInAction` |

`STD040` needed the one genuinely new structural action: a single-valued slot cannot hold a node and its
wrapper at the same time, so `Model.Wrap` detaches, creates and re-attaches inside one transaction
(the repair is one undo step and the node keeps its id). `WrapInAction` is that operation as an edit,
and the intention catalogue's "wrap in" entry will reuse it rather than invent a second one.

The standalone app still reports placeholder names with its own `DefaultNames` (`SHELL001`) rather than
with `STD001`, and that is deliberate for now: including `StandardChecks.Names` beside it would report the
same node twice, with two identical repairs, and replacing a delivered host check is a separate decision
from shipping the package. The two findings are the same check written twice - one by the framework, one
by hand to prove the public surface is enough - so the app can move over whenever the host's copy stops
being worth its demonstration.

### Two rules the catalogue forces

**A repair that destroys something may never run from a single gesture.** Q4 ran a lone plain repair
immediately, which was right for `Trim to "Rule"` and wrong for `Delete the empty rule`: the same
keystroke would delete a node the user was only asking about. Q7 answered that by putting a safety class
on the offer (`EditSafety` - named for the offer rather than for the fix, because the intention catalogue
needs the same classification and one enum serves both) and by running a lone repair only when it was
plain. **The shortcut is gone now**: the gesture always opens the list, so the hazard cannot arise at all,
and `EditSafety` is carried for a surface rather than read as a rule - `DiagnosticFix.Safety` still
defaults to `Plain`, and `OfferList` still accepts one entry, which is now the ordinary case. Open
question 73.

**A standard rename may not break another model.** `STD002` is safe only while nothing references the
name it changes: in a language where a rule's name is what other models point at, renaming it silently
leaves dangling references that the fix never saw. So the standard version of the check looks for
referrers and stays silent when there are any, and updating them is the M6 rename refactoring, which is
allowed to know how to find them (open question 74).

### Which four to ship first

`STD003` (trim), `STD010` (insert into an empty slot), `STD001` (name after the concept, already written
as a host check) and `STD040` (wrap in parentheses). All four are plain, all four are one property write
or one existing action, and between them they cover what a user actually hits while typing: a name, a
hole, and an expression the parserless projection cannot group.

## Slices

Each slice is its own branch and its own merge, per the working rules.

| Slice | Branch | Delivers |
| --- | --- | --- |
| Q1 ✓ | `feat/quick-fix-model` | `FixAvailability`, `DiagnosticFix`, `FixContext`, `FixSet`, `EditorSession.Fixes`, `QuickFixes.At/Of`, tests for keying, duplicate rejection, order, disabled, and a marker on a wrapped line |
| Q2 ✓ | `feat/quick-fix-apply` | `EditorSession.Apply(DiagnosticFix)`, `EditProgram`, re-resolution by id, one-undo-step, stale rejection, rollback |
| Q3 ✓ | `feat/quick-fix-sample` | `RULES001` and its rename fix in `Projectional.Samples.Checks`, a fix for the workbench's `SHELL001`, both published to the editor at open and on `Model.Changed` |
| Q4 ✓ | `feat/quick-fix-keys` | `Alt+Enter`/`Ctrl+.` in `EditorKeyMap` and `KeyRouter`, run-when-single, and the list of several as `Offered`; verified in a real window with a probe |
| Q5 ✓ | `feat/quick-fix-menu` | the list navigable from the keyboard (`SelectOfferAction`, `AcceptOfferAction`), the "Quick fixes" submenu in `EditorMenu` with the reason on a disabled entry, and the offer line in `MarkerTip` |
| Q8 ✓ | `feat/quick-fix-popup` | the painted popup for `Offered`: `OfferPresenter` beside `CompletionPresenter` with its own measure and row hit test, one shared placement helper for both lists, and the control wiring that draws it and turns a click into select-then-accept |
| Q6 ✓ | `feat/quick-fix-bulb` | `QuickFixBulb.BoundsOf` (headless geometry, one line square left of the page), the margin the control reserves for it, the two-primitive glyph, its hit test and the answer kept between paints |
| Q7 ✓ | `feat/standard-checks` | `EditSafety` on the offer and the gesture that honours it, then `Projectional.Checks` with the first four standard checks (`STD001`, `STD003`, `STD010`, `STD040`) and the tests that a language including them needs no code of its own - plus `InsertChildAction`, `WrapInAction` and `Model.Wrap`, which two of the four repairs needed |

Q1 and Q2 hold the invariants and are testable without a window; Q4 to Q6 are presentation and are
the first customers of the headless Avalonia test project, with a probe standing in until it exists.

After Q7 the intentions plan continues at `feat/intentions-model` with a source already in place,
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
| Fixes on save, or automatic repair | the model changes only when the user says so; a character typed at a node's edge is a [concept trigger](triggers.md), which is a language gesture rather than a repair |
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
- **The popup, verified in a real window.** A probe opened the standalone shell, marked the node under
  the caret with one finding carrying two repairs, raised `Alt+Enter` and rasterised the control: the
  list opened with two rows, `Measure` answered 54 units of height for two 23-unit rows plus the inset,
  `RowAt` named row 1 for a point in its middle and `null` for a point outside the box, and the picture
  showed a bordered white box with the first row highlighted. Pointer input is still not raisable, so
  the click path is proven by `RowAt` plus the two actions it calls rather than by a synthetic press.
- **The standard catalogue, headless.** `StandardChecksTests` builds a document with one problem per
  shipped check and asserts the codes in catalogue order with usable repairs - the "no code of its own"
  claim in one test. `PlaceholderNameCheckTests`, `SurroundingWhitespaceCheckTests`,
  `EmptyChildSlotCheckTests` and `BinaryGroupingCheckTests` cover each check's positive and negative
  cases and apply its repair through a session, so every shipped fix is proven to clear its own finding
  and to undo in one step. `CheckSetTests` pins order, composition and the null guards.
- **The new structural edits, headless.** `WrapInActionTests` proves identity survives a wrap, the
  wrapper takes the node's place among its siblings, undo restores it in one step, and a root or a
  concept that cannot stand there is refused; `InsertChildActionTests` proves the fix and Enter land the
  caret in the same place. `ModelTests` covers `Model.Wrap` directly, including the many-valued slot.
- **The safety class, through the gesture.** `QuickFixKeysTests` pins that a lone destructive repair
  opens a one-entry list instead of running, that it still runs from that list, and that a lone disabled
  repair is shown rather than silently refused; `DiagnosticFixTests` pins the plain default.

## Open questions

Rows 31 and 32 in [open-questions.md](../open-questions.md) settle the order of the sources and the
bulb's trigger; rows 58 to 60 are the ones this document opens.
