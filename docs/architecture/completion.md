# Completion

*Slice A — concepts into an empty slot — and slice A2 — the values of an enumeration — are
implemented. The rest of this document is the plan.*

In a text editor, completion guesses what word the user is typing and offers vocabulary from
the file, the project or a language server. Here there is nothing to guess: the caret is in an
**empty slot**, and the slot already says which concepts may fill it. Completion therefore does
not offer words, it offers *the model's own possibilities*, and the list is derived from the
language rather than assembled from text.

The one place where a cell *does* have text and the list still offers no guess is a property
whose type names its values. An enumeration is closed, so "what may be here" is answered by the
type alone — no vocabulary to collect, no prefix to infer, nothing to score
(see [Values](#values)).

That difference is the whole design. Everything below is either a consequence of it or a
deliberate limit of the first slice.

## What a proposal is

A slot declares the concept its children must be:

```csharp
ChildSlot("condition", ExpressionsConceptKeys.Expression, Cardinality.One)
```

`Expression` is abstract, so no node can be an instance of it, and seven concrete concepts sit
below it. **Four** of them are the proposals for that slot, in **declaration order** — the order
the language author wrote them in, which is the author's ranking of what a user most likely
wants. The other three are the literals, hidden from the list on purpose (see below).

Enter without a list still creates the first concrete concept, which for the rules sample is
`IntLiteral` — so the top entry and the default disagree whenever a hidden concept precedes a
shown one. That is deliberate: the two answer different questions. The list asks "which of these
would you pick from a menu", and Enter asks "what does a slot take if the user just wants
something to type into". A literal belongs only to the second answer.

```csharp
public sealed class CompletionItem
{
    public CompletionItem(string conceptKey, CompletionKind kind);
    public string ConceptKey { get; }                                  // the payload
    public CompletionKind Kind { get; }
    public string Label => field ??= Kind == CompletionKind.Value
        ? ConceptKey
        : LabelOf(ConceptKey);
    public static CompletionItem ForValue(string symbol);
}
```

- **The item carries the payload and nothing derived.** `ConceptKey` is that payload, and its
  meaning is the kind's: a concept key for `Node`, a symbol for `Value`. Two names for one field
  would be a second thing to keep in step, and the kind is already the discriminator.
- **`Label` is the last segment of a concept key** — `Expressions.IntLiteral` becomes
  `IntLiteral` — because a list of fully qualified keys reads as noise, and the short name is
  what the declaration itself calls the concept, so no language needs a second, display-only
  name. A **value** keeps its symbol verbatim, dots included: it is already the exact text the
  property stores, so shortening it would show a word the user cannot type.
- **`Kind` is the vocabulary a host picks an icon by.** Two members today: `Node`, for a concept
  that fills a slot, and `Value`, for a symbol that replaces a cell's text. References and
  keywords slot into the same enum later without changing the item.
- **No colour, no glyph, no picture.** What an item looks like is the host's decision, for the
  same reason the syntax palette lives above this layer: a model carrying a picture would have
  to be persisted with one.

`CompletionSource.Proposals(concepts, owner, slot)` walks the registry and keeps every concept
that is a kind of the declared one **and is offered in completion**. It is a read-only query: no
cache across edits, so a language that gains a concept needs no rebuild of anything.

Two concepts are filtered out, for two different reasons:

- **abstract ones**, because no node can be an instance of them, so there is nothing to create;
- **those declared `isOfferedInCompletion: false`**, because the language author decided they
  do not belong in a menu. The flag is a statement about the list only
  (`docs/architecture/language.md`): such a concept is still a legal child, and it is still
  what `ConceptRegistry.DefaultInstantiableFor` may answer with, so Enter can create it.

That second distinction is the reason `Proposals` is not simply "what
`DefaultInstantiableFor` would accept". The two used to be the same query and no longer are;
keeping them separate is what lets `Expressions` hide its literals without making them
unreachable. A slot declared **directly** over a hidden concept therefore proposes nothing at
all, and the user fills it with Enter — a slot that can offer no choice is a slot where a list
would have nothing to say.

## Values

The second kind of proposal is not a concept at all. A property whose type is an enumeration
knows **every legal value without parsing anything**, so the list can name the whole answer:

```csharp
PropertySlot("outcome", PropertyType.Enum(RuleOutcomes.Accept, RuleOutcomes.Review, RuleOutcomes.Reject))
```

```csharp
public abstract class PropertyType
{
    public virtual IReadOnlyList<string> Symbols => [];   // declaration order
}
```

- **`Symbols` is on the base type, empty by default.** Only an enumeration has a closed set, so
  every other type answers with nothing and needs no override. Reading the symbols out of an
  `EnumPropertyType`'s private store was the alternative; asking the type is the one shape that
  does not depend on knowing which subclass it is.
- **Declaration order is the ranking**, exactly as it is for concepts, and it is the same order
  the type uses when it validates. No second sort, no frequency table.
- **The current value is preselected**, so Enter without choosing rewrites what is already there
  and changes nothing — the list opens *agreeing* with the model instead of proposing an edit.

A value list is opened by the same `CompletionSource` and claimed by the same keys, but it is a
different kind of question, which shows in three places:

| | Node proposals | Value proposals |
| --- | --- | --- |
| The slot | empty child / collection slot | a property cell, which usually **has** text |
| Accepting | creates a node, resolves the caret into it | writes the property, leaves the caret alone |
| Everything else | unchanged: prefix, arrows, wrap, Escape, click | |

Because the cell already holds text, the two gestures the node path does not need become the
whole point: typing **filters** and Backspace **widens**. That is not a convenience here, it is
the repair of a cell that could not be retyped at all: every prefix of a symbol is an illegal
value, so a user typing `A` over `Review` was refused, and the list is what turns those
keystrokes into a narrowing filter (`docs/architecture/editing.md`).

### The list opens when the caret arrives

A node list needs a request: the slot is empty, so there is nothing to be curious about until the
user asks. An enumeration is the opposite — arriving in the cell *is* the request, because the
editor knows the whole answer and a user cannot type any of it character by character. So
`EditorSession` offers the values by itself:

- **`Apply` compares the caret before and after the action**, and offers when an action that is
  not a `CompletionAction` left it somewhere else. Arrival is therefore a property of what
  happened, not a guess about intent.
- **`Select` offers unconditionally**, because a pointer click is a move the session was told
  about rather than one an action reported.
- **The session constructor and `Refresh` do not offer.** Opening a document must not pop a list,
  and neither must an undo: a restored model is not a caret the user moved.
- **A dismissed list stays dismissed.** Escape closes it, and a keystroke that is refused leaves
  the caret where it is, so nothing reopens what the user closed until the caret really leaves
  the cell and comes back.
- **A selection inside the cell does not suppress the list**, because selecting the value is how
  a user says "I am about to retype this" — and for an enumeration that is now a working gesture.
  A selection that spans cells does suppress it: the caret then stands for a range.

## The list is view state

An open list is not part of the document. It is never serialized, it creates no undo step, and
two sessions over one model may hold different lists — so it lives on `EditorSession.Completion`
next to `Choice`, which is the existing precedent for view state that survives an edit cycle
but not a save.

```csharp
public sealed class CompletionList
{
    public IReadOnlyList<CompletionItem> Items { get; }   // declaration order, never reordered
    public string Prefix { get; }                         // what the user has typed into the list
    public IReadOnlyList<CompletionItem> Matches { get; }  // Items filtered by Prefix
    public int Count { get; }
    public bool IsEmpty { get; }
    public int SelectedIndex { get; }
    public CompletionItem? Selected { get; }
}
```

The list is **immutable**: every gesture answers with a new list, so a stale reference cannot
be mutated into disagreeing with the caret. `Items` is kept alongside `Matches` because the two
answer different questions — "what could be here" and "what does the filter leave" — and because
widening a filter must be able to restore what it removed.

`Prefix` is held on the list rather than in a cell. An empty slot has no text to type into, so
the characters the user types while the list is open are not an edit at all; they exist only to
filter, and they disappear with the list. Closing the list hands the next character back to
`TypeTextAction`, which may then create a concept from it
([editing.md](editing.md#typing-into-an-empty-slot)) — the prefix itself is still never stored.
Over a value list the same rule buys something a text editor cannot offer: a prefix that is not
itself a legal value still filters the list, so the half-typed word costs the model nothing.

Routing an edit through the session is what keeps this honest. `EditContext.Completion` is the
handle an action uses, `EditorSession.Apply` copies it back onto the session after every apply,
and **any action that is not a `CompletionAction` closes the list**. The list therefore cannot
outlive the caret that justified it, and no action has to remember to dismiss it.

## Gestures

| Key | Effect | `EditorKey` |
| --- | --- | --- |
| Ctrl+Space | open the list for the slot or property at the caret | `ControlSpace` |
| (none) | the list also opens by itself when the caret arrives in a property whose type names its values | — |
| Up / Down | move the highlight, wrapping at both ends | `Up` / `Down` |
| Enter / Tab | take the highlighted item: create the concept, or write the symbol | `Enter` / `Tab` |
| Escape | close the list, leave the model alone | `Escape` |
| Backspace | drop one character of the prefix, which widens the filter | `Backspace` |
| any printable character | append to the prefix, which narrows the filter | (text input, not a key) |

Two of those need explaining, because the plain table cannot decide them.

**Enter and Tab are ambiguous while the list is open**, exactly as Enter is ambiguous in
`KeyRouter` generally. `KeyRouter.For(session, key)` therefore asks the list first and falls
back to the plain table; the plain `For(key)` stays for a host with no document to ask.

**Text is not a key.** A character typed into an open list narrows it and does not reach the
model, so it can never be a letter in `EditorKeyMap`. That is why the control's `OnTextInput`
calls `KeyRouter.Type(session, text)` rather than `Type(text)` — the session-aware overload is
the one that knows a filter is being typed into.

**A key the list does not claim closes the list and falls through.** Left, Home, a letter that
the platform reports as a key rather than as text — none of them may be swallowed by a list that
happens to be open, because the user's intent was to edit. `Escape` is the one exception that is
consumed unconditionally: a host that sees `Handled` may not want to close its own dialog, so
it can check `session.Completion is null` to tell "dismissed a list" from "nothing to dismiss".

**Backspace with an empty prefix** has nothing to widen, so it falls through and closes the
list — which is what makes Escape optional rather than required.

`EditorKeyMap` maps `Ctrl+Space` to `EditorKey.ControlSpace` and `Escape` to `EditorKey.Escape`.
Ctrl+Space joins the short list of documented exceptions to the input layer's rule that
Control-modified keys belong to the host: no host can offer completion on the editor's behalf,
and losing Ctrl+Space would lose the only way to open a list. Escape is not a Control
combination at all; it is claimed because a list a user cannot dismiss with the key they dismiss
everything with is a trap.

## How the list is shown

The control paints it **inside its own surface**, at the end of `Render`, positioned from the
caret's own bounds.

A `Popup` was tried and rejected: its `Open()` requires a visual-tree parent, and the editor is
a single-surface `Control` that owns no children, so a popup would have to be a child control
or a separate top level. Painting in-surface costs one thing — a list near the right or bottom
edge cannot extend past the control — and buys two: it can never detach from the document, and
it needs no second layout path. The presenter flips the list above the caret when it would
overflow the bottom and clamps it horizontally.

```csharp
public sealed class CompletionPresenter
{
    public int VisibleRows { get; set; }                       // twelve
    public Size Measure(CompletionList list);
    public void Render(DrawingContext context, CompletionList list, Rect bounds);
    public int? RowAt(CompletionList list, Rect bounds, Point point);  // for a click
}
```

It is a plain painter with no widget and no state: it is handed a list and a rectangle and draws
into it. The control owns the rectangle — it measures the caret, places the list one row below
it, flips it above when that would overflow the bottom, and clamps both axes into the control's
own bounds — and asks `RowAt` *before* the document hit test, so a press on a row does not also
move the caret into whatever is underneath. A clicked row runs the two actions the keyboard
would (`SelectCompletionAction` then `AcceptCompletionAction`), so the mouse cannot accept
something the keyboard would have refused.

**An icon is an `IImage`, supplied by the host.** `CompletionPalette` mirrors `SyntaxPalette`:
a `FrozenDictionary<CompletionKind, CompletionIcon>`, `Light`/`Dark` presets, `Create`, `IconOf`
and `With`, and a host that wants its own pictures passes its own palette to the control. A PNG
`Bitmap` works, which is what a host with a real icon set will do.

The **built-in** set is vector: `CompletionIcon.FromPath` parses a `StreamGeometry` and wraps it
in a `GeometryImage`, a small `IImage` that draws the geometry with its own translate/scale/clip
transform. Two reasons it is not a `DrawingImage`, which is the type that looks like it should
do this:

- a `DrawingImage` handed to `DrawImage` in one drawing context repeatedly came out **translated
  about four pixels further right on every row**, clipped by its own destination rectangle and
  eventually invisible — which is exactly how the icon column appeared empty for seven rows;
- a `DrawingImage` has to be built from a `GeometryDrawing`, which has no `Bounds` and no
  convenient constructor, so the path's own origin (a diamond hand-written as
  `M 8,1 L 15,8 L 8,15 L 1,8 Z` starts at `1,1`) had to be subtracted somewhere anyway.

`GeometryImage` subtracts the geometry's own `Bounds.TopLeft`, scales it into the destination
rectangle and clips to it, and therefore behaves the same on the first row and the seventh. It
is `internal`: a host passes an `IImage` and never needs to know how the built-in one is made.

Keeping the built-in icons as geometry is what lets the repository ship **no binary asset** for
them — no licence question, no per-DPI variant, no file that has to be copied to output — while
a host is free to use pictures instead.

## A description beside the entry

A concept may carry one sentence about itself
([concept-descriptions.md](concept-descriptions.md),
[ADR 0018](../adr/0018-concept-description-is-language-data.md)), and the completion list is its
second reader: `CompletionItem.Description` is resolved from the registry when the item is built,
and the presenter paints the **selected** entry's description as a dimmed line under the list.

That is why the text belongs to the concept rather than to the hover tip: two surfaces read one
field and a language author writes it once. The line is absent - not blank - when the concept
declares nothing, and it never changes a row's height, so the row hit test is unaffected.

## Filling the slot

`AcceptCompletionAction` is the only completion gesture that changes the model. For a concept it
changes it the way Enter would: one child in the slot, the caret resolved into what was created.
Filling a slot is one undoable step whether the user picked from a list or let Enter take the
default.

It re-derives both the slot and the proposal when the key is pressed rather than trusting the
list, because **the list survives between keystrokes and the model does not**: an undo, a
variant switch or a reload can leave a list naming a slot that is gone or a concept the slot no
longer accepts. A proposal the slot refuses closes the list instead of creating a node the
language would reject — the model's invariants are not re-checked here, they are simply never
violated.

With nothing matching the filter there is nothing to accept, so the key dismisses the list and
leaves the model alone. Pressing Enter again then takes the ordinary path and creates the slot's
default, which is what the user would have had without a list open — a graceful degradation and
not a special case.

**A value is written, not created.** `AcceptCompletionAction` branches before the node path:
when the list describes a property cell, accepting is `EditContext.SetProperty` with the symbol,
guarded by `property.Type.IsValid` so a stale list cannot store what the type would reject. It
is the same single undoable step, and the caret does not move — a value replaces the text it
stands for, so there is nothing created to resolve the caret into.

## Slices

| Slice | Branch | Delivers |
| --- | --- | --- |
| A | `feat/completion` | `CompletionKind`/`Item`/`List`/`Slot`/`Source`, the six actions, `EditorSession.Completion`, `EditorKey.ControlSpace`/`Escape`, the two mappings, `CompletionPalette`/`Icon`/`GeometryImage`/`Presenter`, the control's painting and click handling, tests |
| A2 | `feat/enum-completion` | `CompletionKind.Value`, `PropertyType.Symbols`, `CompletionSource.Values`, the value branch of accept, arrival-based offers from `EditorSession`, tests |
| B | `feat/completion-references` | `Kind.Reference` and reference targets as proposals, which needs a *scope* to answer "what may be referenced here" — the first completion that reads the model rather than the registry |
| C | `feat/completion-fill` | completing a **filled** slot: replacing the child a caret is inside, which is the MPS `Substitute` action and needs the substitution rules a language declares |
| D | `feat/completion-words` | narrowing over a *typed* prefix in a property cell, which is the VS Code behaviour users expect and which slice A deliberately does not do |
| E | `feat/completion-tooltip` | a detail pane: the concept's documentation and its declared slots, which needs the language to carry documentation |

Slice B is the one that makes the feature feel finished, and it is deliberately second: proposals
from a scope need a scope, and a scope is the semantics layer
([semantics.md](semantics.md)), not the editing layer. A slice that faked it by listing every
node in the document would have to be thrown away.

## What this plan deliberately leaves out

| Left out | Why |
| --- | --- |
| Completing a slot that already has a child | it destroys what is there, so it is a substitution with replacement rules, not an insertion |
| Word-based completion inside a property cell | the model has no vocabulary to complete from; the text of a cell is a value, not a token stream. A cell whose *type* names its values is the opposite case and is implemented — see [Values](#values) |
| Fuzzy matching | the filter is `Contains` over a short name; a scoring function is a user-experience feature that needs a list long enough to matter |
| Sorting by relevance | declaration order *is* the relevance, and reordering it would contradict what Enter does without a list |
| A list that follows the caret while it is open | any edit closes the list, so the caret cannot move under it |
| Insertion at the caret's index in a many-child slot | `Model.NewChild` adds to the end and there is no insert-at-index API; that arrives with node moves slice B |
| Auto-open while *typing* | an editor that pops a list on the first character needs a suppression heuristic, which is a policy the host should own. Opening on caret arrival is a different thing and is implemented, because arrival is a move the session observed and can therefore recognise exactly |
| Remembering a "recently used" order | it would be per-user state the session does not have, and it would contradict declaration order |

## Decisions this plan proposes

| # | Decision |
| --- | --- |
| D1 | An open list is view state on the session, never model state, and never its own undo step |
| D2 | Proposals are concept-driven: the concrete concepts of the slot's declared concept, in declaration order |
| D3 | Accepting fills the slot the way Enter does, re-deriving both slot and proposal when the key is pressed |
| D4 | A key the list does not claim closes it and falls through to the model; only Escape is consumed unconditionally |
| D5 | Text typed into an open list is a filter, not an edit, and is routed through the session-aware `Type` overload |
| D6 | An item's icon is an `IImage` from a host palette; the built-in set is vector geometry drawn by `GeometryImage`, so no binary asset is committed |
| D7 | The list is painted inside the editor's surface, never in a toolkit popup |
| D8 | A property type may name its legal values (`Symbols`), and such a cell offers them **without a request**: arriving in the cell is the request. The offer is derived from a caret move the session observed, never from a timer, and never on open, undo or redo |

## Testing

Three files, all in the single test project, and none can reach the internal
`CompletionSlot`/`CompletionSource` (`Projectional.Editing` grants no internals), so all drive
the public path — `EditorSession` plus `KeyRouter`:

- `CompletionListTests` covers the pure list: filtering, the prefix, wrapping `Move`, widening
  one character at a time, and the selection resetting to the first match after a change of
  filter.
- `CompletionItemTests` covers the two labelling rules a value introduces: a symbol is its own
  label with its dots intact, and the kind survives construction.
- `CompletionActionTests` covers the gestures end to end over the rules sample, whose `condition`
  slot is declared over an abstract concept with four offered concrete ones: Ctrl+Space opens
  four in declaration order; Down wraps; typing narrows; Backspace widens and then closes; Enter
  creates the concept and leaves the caret inside it; Escape closes without changing the model; a
  key the list does not claim closes it and the key still reaches the model. One test pins the
  asymmetry the flag introduces: the three literals are absent from the list *and* still created
  by a plain Enter in the same slot.

The same file carries the six value tests, over the rule's `outcome` property, which names
`Accept`/`Review`/`Reject` in that order and starts on `Review`:

| Test | What it pins |
| --- | --- |
| `ArrivingInAPropertyThatNamesItsValuesOffersThem` | the list opens by itself, in declaration order, with the current value preselected |
| `AClickIntoAPropertyThatNamesItsValuesOffersThemToo` | `Select` offers as well, so a pointer move behaves like a caret move |
| `TypingFiltersTheValuesAndEnterWritesTheMatch` | typing selects without editing, Enter writes the symbol, and it is one undo step that undoes back to the old value |
| `AValueIsWrittenInPlaceAndLeavesTheCaretInTheCell` | Down then Enter rewrites the property and moves nothing |
| `ADismissedListStaysClosedUntilTheCaretLeavesAndComesBack` | Escape is not undone by a refused keystroke; a real move away and back reopens it |
| `ArrivingInAPropertyThatNamesNoValuesOpensNothing` | the offer is a property of the type, not of "being in a property" |

Arriving is done by a **walk**, not by a counted number of presses: a helper presses Right until
the caret's address is the target's. A fixed count would turn every future notation edit into a
test failure, and the path to a cell in the middle of a line is already four stops.

`EditorKeyMapTests` covers the input side: Ctrl+Space and Escape map, and neither is claimed
under a different modifier.

The list's own painting has no unit test — it is a `DrawingContext` consumer with no headless
harness — and was verified with a throwaway Avalonia window that captured the control to a
bitmap after each gesture. That is also how the icon-column bug was found: the labels were
indented correctly while the icons painted nothing, so the layout was right and the *image* was
wrong. Geometry assertions would have missed it.

## Open questions

- **What should Ctrl+Space do inside a property cell?** In an enumerated property it opens the
  values, because `CompletionSlot.At` resolves a property cell and the offer is the same one
  arrival makes. In a *free-text* property it still does nothing, because the model has no
  vocabulary to offer; slice D is what would answer it, and until then a host that wants the key
  free can check `session.Completion` after the key.
- **Does the arrival offer need a delay?** It opens synchronously, so a caret that passes through
  a property cell on its way somewhere else opens a list the user did not ask for. Today every
  move is one cell, so a pass-through is a deliberate press; if word-jump or scrolling movement
  ever lands mid-line, a deferral becomes necessary.
- **Should a value list be reachable when the caret cannot be in the cell?** A cell hidden by a
  variant has no caret place, so its values are unreachable by design. A host that wants to edit
  a hidden slot needs a form, which is a different feature.
- **Should the list filter on the caret's surrounding text when it opens?** A cell next to an
  empty slot often holds the beginning of what the user meant. It is a guess, and a wrong guess
  hides the entry the user wanted.
- **Should a many-child slot propose at the end of the list only?** Today yes, because
  `NewChild` appends. Once insertion at an index exists, "insert here" and "append" become two
  different gestures and this document has to say which key each gets.
- **What is the maximum useful list length?** `VisibleRows` is twelve, with no scrolling. A slot
  with forty concrete concepts would need a scroll region and therefore a wheel gesture inside a
  control that currently has none.
- **Should the host be able to suppress the built-in items?** `CompletionPalette.With` replaces
  one kind's icon, not the vocabulary. A host that wants a concept hidden needs a filter the
  editing layer does not offer — while the *language* can hide one with
  `ConceptDeclaration(..., isOfferedInCompletion: false)`. A host-level filter is still open.
