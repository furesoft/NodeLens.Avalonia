# Clipboard

Copy, cut and paste are the first edits whose result does not always land in the document.
`Ctrl+C` writes to a place the editor does not own, `Ctrl+V` reads from a place the editor did
not write, and both of those places are shared with every other application on the machine.
This document states how the editing layer keeps that exchange honest without learning what a
platform clipboard is.

## Why text and not structure

A projectional editor could copy a subtree - a whole `Rule`, a whole `Step` - and paste it as
a structural fragment into another slot. That is a real feature and it is *not* this one. M1
copies **text**, for the same reason every text action edits one property cell at a time:

- the model is the only source of truth, and a fragment would have to survive leaving the
  process as text and coming back as a node, which means a serialisation format, a concept
  resolution step, and a repair path for a fragment pasted into a language that does not
  declare its concept;
- a selection is a range inside one cell, so a structural copy would have to answer "what did
  the user select" for a range that spans two nodes - a question the selection type does not
  currently answer;
- text is what an editable cell *holds*, so copying text needs no new model concept at all.

Structural fragments are planned for M3, where substitution rules exist and a pasted fragment
has somewhere to be validated. Until then the clipboard carries characters.

## The seam: `ITextClipboard`

`Projectional.Editing` must not reference Avalonia ([ADR 0010](../adr/0010-public-api-surface.md)),
so the clipboard is an interface in the editing layer and an adapter in the frontend:

```csharp
public interface ITextClipboard
{
    string? Text { get; set; }
    Task RefreshAsync() => Task.CompletedTask;
}
```

Three decisions are in that tiny surface.

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

`InMemoryClipboard` is the fallback an `EditorSession` gets when no host supplies one: one
string and nothing else. It is what makes every clipboard test headless, and what a host
without a clipboard (an embedded editor in a test, a headless build) gets for free.

## The four actions

| Action | Key | Effect |
| --- | --- | --- |
| `SelectAllAction` | `Ctrl+A` | widen the selection: the property, then the node, then the document |
| `CopyAction` | `Ctrl+C` | put the selected text on the clipboard, leave the document alone |
| `CutAction` | `Ctrl+X` | copy, then delete the selection |
| `PasteAction` | `Ctrl+V` | type the clipboard text at the caret |

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
than doing nothing. Cut stays text-only for now: it would have to write `SelectedText`,
which is `null` for a node selection, so it would silently lose data.
[editing.md](editing.md#selection) records the action; structural copy and cut are M3.

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
- `Copy` and `Cut` are enabled only when the selection has text **and** the caret's cell is a
  `PropertyCell` - the same predicate, `CanCopyText`, for both, because a selection that cannot
  be copied cannot be cut either. A node selection has no text, so those two entries go grey
  while `Select All` has widened past the value - structural copy is a later change;
- `Paste` is enabled only when `session.Clipboard.Text` is non-empty.

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

## What this does not do

| Missing | Consequence | Arrives with |
| --- | --- | --- |
| Structural copy | only text inside one cell can be copied; a whole node cannot | M3 |
| Rich clipboard payloads | only text is exchanged; no formatting, no concept reference | not planned |
| Cross-editor fragment paste | a fragment pasted from another document has no concept resolution | M3 |
| Clipboard history | one value at a time, as the platform provides | host's business |

## Testing

All of it is headless, because the clipboard is an interface and the actions are plain types:

- `InMemoryClipboardTests` covers the fallback clipboard;
- `SelectAllActionTests`, `CopyActionTests`, `CutActionTests`, `PasteActionTests` cover the
  four actions against a real session with an `InMemoryClipboard`;
- `EditorKeyMapTests` proves the four gestures reach the editor and that a bare letter, Alt
  and Meta do not;
- `EditorMenuTests` proves availability and that picking an entry runs the same action.

The one thing a headless test cannot prove is that the platform clipboard actually receives
the text. That is what the E2E walkthrough app (`samples/Projectional.Walkthrough`) is for:
it drives the real control in a real window and prints what each step did.
