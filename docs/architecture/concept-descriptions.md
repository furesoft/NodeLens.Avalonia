# Concept descriptions

A language author writes one sentence about a concept. The editor shows it where a user
needs it: hovering a node of that concept, and next to the completion entry that offers it.
The text is documentation, so it never becomes model data, never enters an undo step and is
never stored per node.

Status: **delivered.** All five slices are in: the declaration and `Concept.Description` (D1), the headless
tip (D2), the hover in the control (D3), the completion pane (D4) and descriptions on both shipped
languages (D5). D6 extends the same mechanism to **slots**, which is where the documentation is needed most:
a property is the one place in a document where an empty value is a visible state - the notation draws the
cell whether or not it holds text. D7 lets a slot ask for the same sentence **over a filled value**, for the
texts that do not explain themselves.

## The text is a property of the concept

`ConceptDeclaration` gains an optional `description` parameter and `Concept` gains
`Description`:

```csharp
new ConceptDeclaration(
    "Rules.Rule",
    slots: [RulesLanguage.NameSlot, RulesLanguage.ConditionSlot, RulesLanguage.OutcomeSlot],
    superConcepts: [],
    nameSlot: RulesLanguage.NameSlot,
    description: "One rule of a ruleset: a name, an optional condition and an outcome.")
```

Three placements were possible and only one of them is right:

| Placement | Why not |
| --- | --- |
| `Node` | It is not model data. It would be persisted, versioned and undoable, and identical on every node of the concept - a hundred copies of one sentence. |
| `Notation` | A concept drawn as a block and as a table would have two descriptions, and an abstract concept - or one whose notation is not written yet - would have none. |
| `ConceptDeclaration` | One text per concept, owned by the language, readable by every consumer. |

`Concept.Description` is `string?`. The declaration normalizes: the text is trimmed, and an
empty one becomes `null`, so "declared no description" and "declared a blank one" are the same
state and a language cannot ship a tip that shows nothing.

**A description is not inherited.** `NotationRegistry` inherits notation because a concept that
only extends a language wants its super's shape; a description describes *this* concept, and the
super's prose would describe something the user is not looking at. The practical consequence is
small: an abstract concept cannot be instantiated, so no node can hover it, and it is not
offered in completion, so nothing would show its text anyway. Whether a hover should list the
whole ancestry is open question 79.

## What the tip says

Three lines at most, in the same width budget as the marker tip:

```
Rules.Rule
One rule of a ruleset: a name, an optional condition
and an outcome.
```

- Line one is the **concept key**, not a friendly label. The notation never shows the key, and a
  language author reading the tip needs the name they can grep for. A short label ("Rule") would
  read better for the language's *user*; that is open question 80.
- The description is word-wrapped, not truncated: it is prose the author wrote to be read.
- Nothing else. No "Name: Check" - the name is already on screen, and a tip that repeats the
  screen is noise.

A concept that declares no description shows **no tip at all**. An empty box with a key in it
would be worse than nothing, because it would teach the user that hovering is pointless.

## When the tip appears

A hover over any cell of a node whose concept declares a description. That is a different target
from the marker tip, which hovers a squiggle, so the two rules differ in one place on purpose:

|  | Marker tip | Concept tip | Property tip |
| --- | --- | --- | --- |
| Target | a marker band | any cell of a node | an **empty** property cell, and any property cell whose slot opted in |
| Delay | none | 600 ms | 600 ms |
| Precedence | wins | never shown while a marker band is hovered | beats the concept tip, loses to the marker |

- **The delay exists because the target is everything.** A wave is a small shape a user aims at;
  ordinary text is what the pointer crosses on its way somewhere else. Without a delay every
  mouse move would flash a box.
- **The marker wins.** A squiggle is a specific finding at a specific place; a description is
  ambient. Two boxes under one pointer is never right, and the finding is the one that asks for an
  action.
- **The anchor is the node, not the cell.** The outermost cell carrying the node address decides
  the box, so it does not jump while the pointer crosses a line of the same node.
  `MarkerTip.Place` already flips the box above or below to keep it inside the viewport; the
  concept tip reuses it instead of growing a second placement rule.
- **Dismissal is shared with the marker tip**: pointer exit, a click, any key, and any rebuild
  (the node may not exist afterwards). The control already has one `ClearHover` path; the concept
  tip hangs off that one rather than adding a second dismissal story.

## Property descriptions: the same sentence, on a slot

A concept description answers "what kind of node is this". A user staring at an empty box needs the
other answer - "what goes in here" - and no node can carry it, because the node exists and the value
does not.

So `PropertySlot` gains the same optional `description`:

```csharp
new PropertySlot("value", PropertyType.Integer, description: "A whole number, such as 42.")
```

- **It appears while the slot is empty without being asked for.** Once there is text, the text is the answer and
  a box over it would be in the way - which is why the feature is about properties at all: a property is the only
  place in a projectional document where "nothing entered yet" is a *visible* state, because the notation draws
  the cell whether or not it holds a value.
- **A slot may ask for the sentence over its value too** (`showDescriptionWhenFilled: true`). A language knows
  which of its texts do not explain themselves - a key the notation never spells out, an operator symbol, a flag
  whose two words are the whole meaning - and opts in per slot for exactly those. It is off by default, so
  documentation does not start covering text that already says what it is, and a language that asks for it without
  declaring a description is refused at construction: a box that could never show anything is a mistake, not a
  setting without an effect.
- **The heading is the slot name**, not the concept key: `value`, `operator`, `outcome`. It is what a
  language author greps for, exactly like the concept key in the concept tip.
- **A defaulted property reads as filled.** The model cannot tell a default from a value the user typed
  (`name` = `"unnamed"`), so a slot born with a default shows no tip unless it opted in. That is the honest
  reading of "nothing entered yet" rather than a limitation to work around - and the reason the built-in
  languages opt in on their defaulted slots: their descriptions were otherwise unreachable.
- **The property's sentence wins over the concept's.** When the pointer is over a slot that has something to say -
  empty, or filled and opted in - the more specific answer is the one about the box, not the one about the node.

Two consequences the implementation had to accept:

- **The tip is anchored to the slot's cell, not to the node.** The concept tip anchors to the node so it
  does not jump while the pointer crosses a line; a slot tip is *about* one cell - and a node drawn only
  as a property (an `IntLiteral` is one cell) has no cell carrying its node address at all, so anchoring
  to the node would have drawn nothing. `ConceptTipBounds` falls back to the node address when the slot's
  cell has gone.
- **The hover keys on (node, slot)**, so moving from one property of a node to another restarts the
  delay while moving inside one property does not.

`PropertyTip.Of(session, position)` is the whole decision, headless: the place must be a `PropertyCell` with no
text or with a slot that opted in, the node must still exist, and the concept must declare a description for that
slot. Anything else answers `null` and shows no box - a pointer can outlive its node, and a tooltip must never
throw at the moment the user moves the mouse.
## The headless half

`Projectional.Editing` decides what the tip says and where it goes; `Projectional.Avalonia` only
paints it. Same split as the marker tip.

- `ConceptTip` (folder `Tips/`) answers `Of(Node) -> TipContent?` and `Of(session, nodeId) -> TipContent?`.
  It reads the node's concept from the model and needs no tree, so it is testable without a window.
- **`TipContent` is one shape for both tips** - a heading and a sentence - and `TipLines.Of(content, width,
  measure)` is the one wrapper. A box with a heading and a paragraph is one thing to paint, so a second
  content type would mean a second painter path in the control.
- **`MarkerTip.Lines` already owns word wrap and must not be copied.** The wrapper moves into
  `TipText.Wrap(string, width)` and both tips call it. Two wrappers would drift, and a width that
  fits one box but not the other is a bug nobody would think to blame on wrapping.
- The control caches the wrapped lines per `(concept key, width)`, exactly as `_tipLines` is
  cached today, and drops that cache when the hover changes.

## The second consumer: completion

`CompletionPresenter` already paints the open list, so `CompletionItem` gains `Description`
(resolved from the registry at construction, `null` when the concept declares none) and the
presenter paints the selected entry's description as a dimmed second line.

This is the reason the text belongs to the concept rather than to the tooltip: two surfaces read
one field, and a language author writes it once. MPS shows the same panel, which is where the
expectation comes from.

## The third consumer: the definition language

[language-authoring.md](language-authoring.md) already plans a `<<shortDescription>>` slot in the
MPS-shaped structure language. That slot feeds this field, and the names differ on purpose:

| Layer | Name |
| --- | --- |
| Structure model slot | `shortDescription` (what an MPS user types) |
| `ConceptDeclaration` and `Concept` | `description` (what a C# author writes) |
| Structure-to-Core lowering | `shortDescription` becomes `description` |

Do not rename either side to match the other: MPS users know the long name, and `Description` is
the honest name for the field in a .NET API.

## Slices

| Slice | Branch | Content | Status |
| --- | --- | --- | --- |
| D1 ✓ | `feat/concept-description` | `description` parameter, `Concept.Description`, normalization, registry plumbing, tests | delivered |
| D2 ✓ | `feat/concept-tip` | `TipText.Wrap` extraction, `ConceptTip` (content and lines), tests | delivered |
| D3 ✓ | `feat/concept-tip-hover` | control: hover over any node, 600 ms delay, marker precedence, shared dismissal | delivered |
| D4 ✓ | `feat/concept-tip-completion` | `CompletionItem.Description`, description line in `CompletionPresenter` | delivered |
| D5 ✓ | `docs/concept-descriptions-samples` | descriptions on the sample and expression languages, `<<shortDescription>>` lowering note | delivered |
| D6 ✓ | `feat/property-descriptions` | `PropertySlot.description`, `TipContent`/`TipLines` as the shared shape, `PropertyTip`, the hover keyed on (node, slot), slot descriptions on both shipped languages | delivered |
| D7 ✓ | `feat/property-tip-over-a-value` | `PropertySlot.ShowDescriptionWhenFilled`, the same hover over a filled value, `showDescriptionWhenFilled` in the structure language and the notation, descriptions and opt-ins on every built-in slot | delivered |

D1 and D2 need no window and are pure library work; D3 is the only slice that needs the runtime
probe. D4 depends on D1 only, so it can be taken before or after D3.

## Decisions frozen with the slices

What the implementation settled that the plan had left open, and the reason each one is a rule now:

| Decision | Why |
| --- | --- |
| **One presenter for both tips.** `MarkerTipPresenter` paints a finished list of lines, so the concept tip reuses it instead of growing a second box painter. | The two boxes must look alike, and two painters would drift the moment one of them gained a border radius. The *content* is decided in the editing layer; only the pixels are shared. |
| **`MarkerTip.Place` is reused, not copied.** | Flipping above when there is no room below, and clamping into the surface, is one rule about one kind of box. |
| **The anchor is the node's own address** (`new CellAddress(nodeId)`), not the caret place the pointer resolved to. | A box anchored to the caret place jumps while the pointer crosses a line of a node that spans several; the node's outermost cell is stable. |
| **The description is asked for again when the delay elapses**, not remembered from when the hover started. | The pointer can move onto a squiggle while the timer runs, and the marker must win. |
| **`ConceptTip.Of(session, nodeId)` resolves the id and answers `null` when it is gone.** | A pointer can outlive the node it was over; a tooltip must not be the thing that throws. |
| **`TipText.Wrap` is `internal` in `Editing`, not public.** | A host that wants a box calls `ConceptTip.Lines`; the wrapper is an implementation detail that must stay single. |
| **The completion pane's height is reserved when *any* entry has a description**, not when the highlighted one does. | A box that grew and shrank while the arrow keys moved the highlight would make the whole list jump under the pointer. |
| **The pane is one line, in the palette's line colour.** | A list palette has no secondary-text colour, and the same choice is already made for a quick-fix row that cannot run; wrapping the pane would change the list's height per entry. |
| **The opt-in over a value is a slot flag, not a host setting.** `PropertySlot.ShowDescriptionWhenFilled` says it once, beside the sentence it shows. | Whether a text explains itself is a fact about the language, not about the window: the same document opened in two hosts must show the same boxes. A host switch would also have to be threaded through the headless tip for no gain. |
| **The flag is refused without a description.** | A slot that asks for a box it can never show is a language mistake, and the declaration is the earliest moment it can be named - the same reason an unparseable `defaultValue` throws there. |

Verified in a real window: hovering a node of the sample language paints `Rules.Ruleset` and its sentence
below the node, and setting a marker hover on the same node paints only the finding's box. The picture the
README shows for the slot tip is produced the same way - the screenshots tool raises one pointer move onto the
filled `outcome` of a rule, and the box that comes back is the control's own paint.

The completion pane is verified headlessly at the seam that matters - `CompletionActionTests` asserts the
proposal carries the concept's sentence - and its drawing reuses the presenter path the quick-fix list
already exercises. A probe could not reach it: the sample documents have no empty slot that accepts a
described concept, so no such list opens without editing the document first.

## What this must not become

- Not a per-node note. A description is not editable in the editor, not undoable and not saved.
- Not inherited from super concepts.
- Not a second tooltip under the same pointer: the marker tip wins, always.
- Not a place for a validation message. A diagnostic is a finding with a code and a repair; a
  description is what the concept is.
- Not a font or a colour of its own. The tip is painted with the existing tip palette so it looks
  like the marker tip.

## Related

- [language.md](language.md) - the concept and its declarations.
- [markers.md](markers.md) - the tip this reuses, its placement and its dismissal.
- [completion.md](completion.md) - the list that gains the second consumer.
- [language-authoring.md](language-authoring.md) - the `<<shortDescription>>` slot.
- [ADR 0018](../adr/0018-concept-description-is-language-data.md) - the decision above.
