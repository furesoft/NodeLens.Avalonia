# Concept descriptions

A language author writes one sentence about a concept. The editor shows it where a user
needs it: hovering a node of that concept, and next to the completion entry that offers it.
The text is documentation, so it never becomes model data, never enters an undo step and is
never stored per node.

Status: planned. Slice D1 is delivered (the declaration and `Concept.Description`); D2 to D5 are
listed at the end.

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

|  | Marker tip | Concept tip |
| --- | --- | --- |
| Target | a marker band | any cell of a node |
| Delay | none | 600 ms |
| Precedence | wins | never shown while a marker band is hovered |

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

## The headless half

`Projectional.Editing` decides what the tip says and where it goes; `Projectional.Avalonia` only
paints it. Same split as the marker tip.

- `ConceptTip` (new folder `Tips/`) answers `Of(Node) -> ConceptTipContent?` and
  `Lines(ConceptTipContent, width) -> IReadOnlyList<string>`. It reads the node's concept from the
  model and needs no tree, so it is testable without a window.
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
| D2 | `feat/concept-tip` | `TipText.Wrap` extraction, `ConceptTip` (content and lines), tests | planned |
| D3 | `feat/concept-tip-hover` | control: hover over any node, 600 ms delay, marker precedence, shared dismissal | planned |
| D4 | `feat/concept-tip-completion` | `CompletionItem.Description`, description line in `CompletionPresenter` | planned |
| D5 | `docs/concept-descriptions-samples` | descriptions on the sample and expression languages, `<<shortDescription>>` lowering note | planned |

D1 and D2 need no window and are pure library work; D3 is the only slice that needs the runtime
probe. D4 depends on D1 only, so it can be taken before or after D3.

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
