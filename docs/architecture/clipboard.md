# Clipboard

Status: **delivered** - text for M1, structure for M3. Copy, cut, paste, select all and `Delete` are
routed in the control and covered by tests; the clipboard carries either characters or a fragment of a
model.

Copy, cut and paste are the first edits whose result does not always land in the document.
`Ctrl+C` writes to a place the editor does not own, `Ctrl+V` reads from a place the editor did
not write, and both of those places are shared with every other application on the machine.
This document states how the editing layer keeps that exchange honest without learning what a
platform clipboard is.

## Two payloads, one clipboard

A projectional editor can copy a `Rule` or a `Step` and paste it somewhere else, and it can copy the word
inside one property. Both are real, so the clipboard carries either **text**, which is what an editable cell
holds and what another application can receive, or a **fragment** (`ModelFragment`), which is a subtree with
its values, references and children and which can only become nodes again in a model whose language declares
its concepts.

One at a time, never both: the platform clipboard holds one value, and a paste must act on what the user
copied last. Which payload a gesture means is decided at the moment it is pressed (`KeyRouter`), not stored:
`Ctrl+C` over a selection that covers nodes copies structure, and over a selection inside one value it copies
text.

The fragment stays **inside the process**. Text is the only thing that crosses to another application, so a
fragment is not serialized here - a wire format would be a second format beside the document format, a
concept-resolution step, and a repair path for a fragment whose language does not declare its concepts
([open question 82](../open-questions.md)). [llm-language-description](llm-language-description.md) plans to
reuse that shape when a generated document has to arrive as text.

## The seam: `IEditorClipboard`

`Projectional.Editing` must not reference Avalonia ([ADR 0010](../adr/0010-public-api-surface.md)),
so the clipboard is an interface in the editing layer and an adapter in the frontend:```csharp
public interface IEditorClipboard
{
    string? Text { get; set; }
    ModelFragment? Fragment { get; set; }
    Task RefreshAsync() => Task.CompletedTask;
}
```

The name is `IEditorClipboard` rather than `IClipboard` because Avalonia owns that name for its own
abstraction, and an adapter is exactly where the two meet.

Four decisions are in that surface.

**Synchronous read and write.** A key arrives synchronously and an action applies
synchronously, so an asynchronous contract would put an `await` into every action that touches
text - and an `EditAction.Apply` cannot await. The interface is therefore synchronous and the
platform's asynchrony is hidden by caching.

**`RefreshAsync` is the cache's refresh, not the action's.** A platform clipboard can only be
read asynchronously, so the adapter keeps the last text it saw. `RefreshAsync` is where the
host pulls the real text at a moment where waiting costs nothing: the editor gaining focus,
and just before a paste is applied. It returns a `Task`, not a `ValueTask`, because the
callers that cannot await - a focus handler, a pointer handler, a menu that opens
synchronously - fire and forget, and a discarded `ValueTask` is an analyzer error.

**`null` means empty.** `Text` is `string?`; `null` is "the clipboard holds no text", which is
what a paste checks before doing anything. An empty string is a real, if uninteresting, value.

**One payload at a time.** Writing `Text` clears `Fragment` and writing `Fragment` clears `Text`. An
implementation that kept both would let a paste act on something the user did not just copy, and the
platform adapter additionally drops its fragment when a refresh finds text this editor did not write -
another application has put something on the clipboard since, so the fragment is no longer the last thing
copied.

`InMemoryClipboard` is the fallback an `EditorSession` gets when no host supplies one: one
string and nothing else. It is what makes every clipboard test headless, and what a host
without a clipboard (an embedded editor in a test, a headless build) gets for free.

## The seven actions

| Action | Key | Effect |
| --- | --- | --- |
| `SelectAllAction` | `Ctrl+A` | widen the selection: the property, then the node, then the document |
| `CopyAction` | `Ctrl+C` | put the selected text on the clipboard, leave the document alone |
| `CutAction` | `Ctrl+X` | copy the text, then delete the selection |
| `PasteAction` | `Ctrl+V` | type the clipboard text at the caret |
| `CopySelectionAction` | `Ctrl+C` over nodes | put the nodes the selection covers on the clipboard as a fragment |
| `CutSelectionAction` | `Ctrl+X` over nodes | copy the fragment, then remove the nodes |
| `PasteFragmentAction` | `Ctrl+V` with a fragment | create the fragment's nodes where the caret says |

Which of the two copies a gesture means is `KeyRouter`'s decision, and the menu runs the same routing
(`KeyRouter.For(session, EditorKey.Copy)`) so an entry and its key cannot disagree about what they do.

### Select all is a ladder

`Ctrl+A` cannot mean one thing. A projectional document nests three units, and which one the
user means depends on how far the selection has already widened, so the gesture climbs one
rung per press:

| Rung | Range | Why that rung exists |
| --- | --- | --- |
| the value | the whole text of the property cell at the caret | it is what the next keystroke replaces and what `Copy` takes |
| the node | every cell the node under the caret shows, and those of its descendants | it is what `Delete` removes and `Alt+Up` moves |
| the document | the first caret place at offset 0 to the last at its length | it is what a host saves or copies as a whole |

A rung with nothing to offer is skipped, not treated as an obstacle. A caret in a keyword or
in an empty slot has no value, so the first press selects the node; a concept whose notation
is a single property produces the same range for the value and the node, so the second press
reaches the document. A caret in an empty `ChildCell` or `CollectionCell` names the node that
owns the slot, so the press selects the enclosing node - which is the unit that slot belongs
to. The top rung is a no-op, because the document is already selected.

The node rung is what makes a multi-cell selection a first-class state rather than a
degraded one. `SelectionRects` already drew a range across cells, so no rendering work was
needed: a node's cells are contiguous in document order, and its range is the run from the
first caret place whose `CellAddress.Owner` belongs to the node's subtree to the last.

The text actions are unaffected. `Selection.HasText` is false for a range across cells, so
`Copy`, `Cut`, `Paste` and typing keep their one-cell behaviour and a node selection simply
has no text to hand them. `Delete` does act on it: `DeleteSelectionAction` asks
`SelectionRanges.NodesIn` which nodes the range fully covers and removes them in one
transaction, so from a property value `Ctrl+A Ctrl+A Delete` removes the node that value
belongs to, and the document rung removes every node it covers in one undo step - the
document root itself stays, because it is the document, not its own child. A range that
covers a node only partly removes nothing, because guessing which half was meant is worse
than doing nothing. Copy and Cut act on it too, through the structural actions below: a selection that
covers nodes is copied as a fragment rather than as text, because there is no text to hand them.
[editing.md](editing.md#selection) records the selection actions.

`SelectionRanges` (`Projectional.Editing/SelectionRanges.cs`) is the single place that
answers "what range covers this value, this node, this document", so a host that wants to
offer only the document rung can ask for it directly instead of pressing three times. Its
inverse, `NodesIn`, is the same for the other direction.

### Copy leaves the document alone

`CopyAction` writes to the clipboard and returns `EditResult.Unchanged`. That is not a
formality: `Unchanged` means the host has no tree to rebuild and no undo step is produced for
a copy. A copy that produced an undo step would be a bug the user notices immediately.

Only a selection is copied. With nothing selected there is no entity to copy - a cell is a
piece of notation, not a document object, and "copy the word under the caret because the user
pressed `Ctrl+C`" is a behaviour no editor of text has.

### Cut is copy followed by delete

`CutAction` delegates both halves. Writing the text to the clipboard again would be a second
answer to "what does the selection say", and deleting by hand would be a second answer to
"what does deleting a range mean" - including the type check that decides whether the
shortened value is still valid. So the action only orders the two:

```csharp
context.Clipboard.Text = text;
return new DeleteTextAction(DeleteDirection.Forward).Apply(context);
```

The refused case is the interesting one. A property whose value would become invalid cannot
be cut - the copy has already happened by then, so the clipboard holds text the document still
contains. That is the honest outcome: the user can paste it somewhere it fits. The refusal is
passed through as the rejection it is, not reported as a successful cut.

`CoalescingKey` returns `null` so a cut is one undo step of its own, independent of whatever
the delegated delete decides.

### Paste types the text

`PasteAction` is `new TypeTextAction(text).Apply(context)` when the clipboard holds text, and
`EditResult.Unchanged` when it does not. Reusing the type action is the whole point: a paste
goes through exactly the validation a typed character goes through, so a number cell refuses
`"abc"` and a value that fits its type is inserted as one edit. There is no second paste path
that could accept what typing rejects.

`CoalescingKey` returns `null`: pasting is a deliberate action, not a run of typing, so it
never merges into the surrounding text.

## Structural copy, cut and paste

`CopySelectionAction`, `CutSelectionAction` and `PasteFragmentAction` are the same three gestures over
nodes instead of characters. They are separate types rather than a flag on the text actions, because
nothing about them is shared: one writes characters into a cell, the other creates nodes in a slot.

**The payload.** `ModelFragment` holds one or more `FragmentNode`s, each with the concept key, the id it came
from (`Origin`), the properties that were **written** (`Node.IsSet`, so a value the model defaults stays
defaulted rather than being copied as materialized), its references with both halves of the stored
reference, and its children paired with the slot they stood in. `Origin` is not an identity - a pasted node
gets a new one - it exists so that a reference *between* copied nodes can be rebound to the copy.

**Copy and Cut** ask `SelectionRanges.NodesIn`, the same computation Delete uses: a range that fully covers a
node copies that node, and a range that covers nothing whole copies nothing. Cut delegates the removal to
`DeleteSelectionAction`, so the caret lands where deleting the range leaves it and the whole cut is one undo
step.

**Paste** decides its place from the caret, and never replaces anything:

| Caret is | Paste |
| --- | --- |
| in an **empty slot** | fills it - that is what an empty slot is for, including an empty single-valued one |
| in a node of a **list** | inserts directly after that node |
| in a node of a **filled single-valued slot** | refused with a reason: there is no "next to it", and replacing is a labelled entry, not a paste |
| on a **root** | refused: the fragment has no place to take |

Before anything is created, the paste checks that the language declares every concept in the fragment
(`ModelFragment.MissingConcept`, which names the first one it does not know) and that each root conforms to
the slot and may stand there. A fragment from another language is the ordinary case, and a refusal that
names the concept is more useful than a half-built tree.

**The insertion** is `EditContext.InsertFragment`: one transaction, nodes created top-down (attached, then
filled), references written in a second pass because a fragment may point forward. A reference between two
copied nodes is rebound to the copy; one that pointed outside the fragment keeps its target id and its
`resolveInfo`, so it resolves if the target exists in the new model and otherwise draws as unresolved and can
be re-linked - the degradation ADR 0004 asks for. Nothing is dropped: a value the target concept refuses is
an exception rather than a skipped value.

**The caret** lands on the first pasted node, the same landing `InsertNodeAction` uses for what it creates.
A paste never coalesces, so it is one undo step.

## The shared text read

`SelectedText.Of(context)` is one implementation rather than one per action, because "what
does the selection say" has one honest answer: the characters between the selection's two ends
inside the property cell the caret stands in, and `null` when those two ends are not in the
same cell. Both ends are clamped, because a selection may outlive a shortening of the text it
pointed at - a rebuild is not required to keep a range valid.

It is `internal`: no host needs it, and `CopyAction`/`CutAction` are its only callers. The
public behaviour is proved through those actions.

## The key mapping

`Ctrl+A/C/X/V` are the third exception to "Control belongs to the host" in
`EditorKeyMap.Map` (the others are `Ctrl+Left`/`Ctrl+Right` and `Alt+Up`/`Alt+Down`). Every
platform spells these four the same way, and a clipboard reachable only through a menu is a
clipboard nobody finds. Shift does not change them, so `Ctrl+Shift+V` maps to the same
`EditorKey.Paste` - plain-text paste is a later refinement.

A bare letter is text input and never reaches the editor as a key, so `EditorKeyMap.Map`
returns `null` for `A`/`C`/`X`/`V` without Control. Alt and Meta leave them to the host.

## The menu

`EditorMenu.For(session)` offers `Select All`, `Copy`, `Cut` and `Paste` after the history and
the structural edits. Availability is decided before the entry is shown:

- `Select All` is always enabled: the ladder always has a rung left until the document is
  selected, and a host that hides the entry could not reach the node rung at all;
- `Copy` and `Cut` are enabled when the selection has text **or** when it covers nodes -
  `CanCopy`, which asks the same two questions the routing asks: a `PropertyCell` under a text
  selection, or `KeyRouter.CopiesNodes`. A selection that spans cells but covers no whole node is
  neither, so those entries go grey instead of doing nothing;
- `Paste` is enabled when the clipboard holds text **or** a fragment, because both are something to insert.

The commands *are* the actions, so the gesture the menu prints cannot drift from the binding.
`EditorMenuItem.Execute` refuses a disabled entry as well, so a stale menu cannot edit.

## The Avalonia adapter

`AvaloniaClipboard` (`Projectional.Avalonia/Input/`) bridges the synchronous interface to the
platform's asynchronous clipboard through `TopLevel.Clipboard`:

- a write stores the text in the cache and hands it to the platform in the background;
- a read answers from the cache;
- `RefreshAsync` pulls what the platform really holds.

The clipboard is resolved lazily through a function rather than captured in the constructor,
because a control has no top level until it is attached to one, and the same adapter has to
survive re-attachment. Both directions swallow a failing platform call: a clipboard is shared
with every other process on the machine, so an application holding it open is normal and must
not surface as an editor error.

`ProjectionalEditorControl` refreshes the adapter when it gains focus and again just before a
paste is applied, so a `Ctrl+V` sees what another application put there. It also intercepts
the platform's own paste gesture so the text goes through `PasteAction` rather than being
inserted behind the editor's back.

The shell hands the **same** adapter to every tab (`MainWindow._clipboard`, passed into each
`DocumentTab` and assigned to each control). Two consequences, both intended: a subtree copied in one
document can be pasted in another, and the adapter the control refreshes is the one the session reads -
a per-tab adapter would refresh its own cache while the session pasted from a different one.

## What this does not do

| Missing | Consequence | Arrives with |
| --- | --- | --- |
| A fragment outside the process | a copied subtree cannot cross to another application, because a fragment is not text | not scheduled; the wire format is [open question 82](../open-questions.md) |
| A fragment into another language | the paste is refused, naming the first concept that language does not declare; nothing is half-built | not planned - a mapping between two languages needs decisions no declaration makes |
| Rich clipboard payloads | no formatting, no images, no MIME types | not planned |
| Clipboard history | one value at a time, as the platform provides | host's business |

## Testing

All of it is headless, because the clipboard is an interface and the actions are plain types:

- `InMemoryClipboardTests` covers the fallback clipboard, including that writing one payload clears the
  other;
- `ModelFragmentTests` covers what a fragment keeps: written values, children with their slots, both halves
  of a reference, and the first concept its target language lacks;
- `SelectAllActionTests`, `CopyActionTests`, `CutActionTests`, `PasteActionTests` cover the text actions
  against a real session with an `InMemoryClipboard`;
- `CopySelectionActionTests`, `CutSelectionActionTests`, `PasteFragmentActionTests` cover the structural
  ones: where a paste lands, that identities are new, that a reference between copied nodes is rebound,
  that one outside the fragment degrades to unresolved, and that each is one undo step;
- `KeyRouterTests` proves which gesture is which - structure over nodes, text inside a value, and a paste
  that follows the payload;
- `EditorKeyMapTests` proves the four gestures reach the editor and that a bare letter, Alt
  and Meta do not;
- `EditorMenuTests` proves availability and that picking an entry runs the same action.

The one thing a headless test cannot prove is that the platform clipboard actually receives
the text. That is what the E2E walkthrough app (`samples/Projectional.Walkthrough`) is for:
it drives the real control in a real window and prints what each step did.
