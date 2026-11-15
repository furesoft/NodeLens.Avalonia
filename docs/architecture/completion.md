# Completion

*Slice A — concepts into an empty slot — is implemented. The rest of this document is the plan.*

In a text editor, completion guesses what word the user is typing and offers vocabulary from
the file, the project or a language server. Here there is nothing to guess: the caret is in an
**empty slot**, and the slot already says which concepts may fill it. Completion therefore does
not offer words, it offers *the model's own possibilities*, and the list is derived from the
language rather than assembled from text.

That difference is the whole design. Everything below is either a consequence of it or a
deliberate limit of the first slice.

## What a proposal is

A slot declares the concept its children must be:

```csharp
ChildSlot("condition", ExpressionsConceptKeys.Expression, Cardinality.One)
```

`Expression` is abstract, so no node can be an instance of it, and seven concrete concepts sit
below it. Those seven are the proposals for that slot, in **declaration order** — the order the
language author wrote them in, which is the author's ranking of what a user most likely wants.
Enter without a list already creates the first of them (`ConceptRegistry.DefaultInstantiableFor`),
so the top entry and the default never disagree.

```csharp
public sealed class CompletionItem
{
    public CompletionItem(string conceptKey, CompletionKind kind);
    public string ConceptKey { get; }
    public CompletionKind Kind { get; }
    public string Label => field ??= LabelOf(ConceptKey);
}
```

- **The item carries the concept key and nothing derived.** `Label` is the last segment of
  `Expressions.IntLiteral` — `IntLiteral` — because a list of fully qualified keys reads as
  noise, and the short name is what the declaration itself calls the concept, so no language
  needs a second, display-only name.
- **`Kind` is the vocabulary a host picks an icon by.** Slice A has exactly one member, `Node`,
  because concepts are the only thing it proposes. References, slots and keywords slot into the
  same enum later without changing the item.
- **No colour, no glyph, no picture.** What an item looks like is the host's decision, for the
  same reason the syntax palette lives above this layer: a model carrying a picture would have
  to be persisted with one.

`CompletionSource.Proposals(concepts, owner, slot)` walks the registry and keeps every
**non-abstract** concept that is a kind of the declared one. It is a read-only query: no cache
across edits, so a language that gains a concept needs no rebuild of anything.

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
filter, and they disappear with the list.

Routing an edit through the session is what keeps this honest. `EditContext.Completion` is the
handle an action uses, `EditorSession.Apply` copies it back onto the session after every apply,
and **any action that is not a `CompletionAction` closes the list**. The list therefore cannot
outlive the caret that justified it, and no action has to remember to dismiss it.

## Gestures

| Key | Effect | `EditorKey` |
| --- | --- | --- |
| Ctrl+Space | open the list for the empty slot at the caret | `ControlSpace` |
| Up / Down | move the highlight, wrapping at both ends | `Up` / `Down` |
| Enter / Tab | create the highlighted concept in the slot and close the list | `Enter` / `Tab` |
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

## Filling the slot

`AcceptCompletionAction` is the only completion gesture that changes the model, and it changes
it the way Enter would: one child in the slot, the caret resolved into what was created. Filling
a slot is one undoable step whether the user picked from a list or let Enter take the default.

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

## Slices

| Slice | Branch | Delivers |
| --- | --- | --- |
| A | `feat/completion` | `CompletionKind`/`Item`/`List`/`Slot`/`Source`, the six actions, `EditorSession.Completion`, `EditorKey.ControlSpace`/`Escape`, the two mappings, `CompletionPalette`/`Icon`/`GeometryImage`/`Presenter`, the control's painting and click handling, tests |
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
| Word-based completion inside a property cell | the model has no vocabulary to complete from; the text of a cell is a value, not a token stream |
| Fuzzy matching | the filter is `Contains` over a short name; a scoring function is a user-experience feature that needs a list long enough to matter |
| Sorting by relevance | declaration order *is* the relevance, and reordering it would contradict what Enter does without a list |
| A list that follows the caret while it is open | any edit closes the list, so the caret cannot move under it |
| Insertion at the caret's index in a many-child slot | `Model.NewChild` adds to the end and there is no insert-at-index API; that arrives with node moves slice B |
| Auto-open while typing | an editor that pops a list on the first character needs a suppression heuristic, which is a policy the host should own |
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

## Testing

Two files, both in the single test project, and neither can reach the internal
`CompletionSlot`/`CompletionSource` (`Projectional.Editing` grants no internals), so both drive
the public path — `EditorSession` plus `KeyRouter`:

- `CompletionListTests` covers the pure list: filtering, the prefix, wrapping `Move`, widening
  one character at a time, and the selection resetting to the first match after a change of
  filter.
- `CompletionActionTests` covers the gestures end to end over the rules sample, whose `condition`
  slot is declared over an abstract concept with seven concrete ones: Ctrl+Space opens seven in
  declaration order; Down wraps; typing narrows; Backspace widens and then closes; Enter creates
  the concept and leaves the caret inside it; Escape closes without changing the model; a key
  the list does not claim closes it and the key still reaches the model.

`EditorKeyMapTests` covers the input side: Ctrl+Space and Escape map, and neither is claimed
under a different modifier.

The list's own painting has no unit test — it is a `DrawingContext` consumer with no headless
harness — and was verified with a throwaway Avalonia window that captured the control to a
bitmap after each gesture. That is also how the icon-column bug was found: the labels were
indented correctly while the icons painted nothing, so the layout was right and the *image* was
wrong. Geometry assertions would have missed it.

## Open questions

- **What should Ctrl+Space do inside a property cell?** Nothing today, because only empty slots
  have proposals. Slice D would answer it with word completion; until then a host that wants the
  key free can check `session.Completion` after the key.
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
  editing layer does not offer.
