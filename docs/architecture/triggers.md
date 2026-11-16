# Concept triggers

Typing an operator next to an expression that is already there. `5` then `+` must give
`5 + <empty>`, not a refusal and not the text `5+` stored in a number.

This is the other half of text entry. [language.md](language.md#typing-rules) has the first half: an
*empty* slot asks the language which concept the user is describing. A trigger answers the
neighbouring question - the caret is at the edge of a node that already exists, and the character
typed there says what should surround it.

## The two halves

| Where the caret is | Who answers | Result |
| --- | --- | --- |
| in an empty slot | `ITypingRules` | a new node is created and the text written into it |
| in a property's text, which accepts it | the property | the text is written |
| in a property's text, which refuses it | the concepts' triggers | that node is wrapped, or the keystroke is refused |
| anywhere else (a keyword, a gap) | the concepts' triggers | that node is wrapped, if a concept says this character means it |

Both are declarations rather than inferences from the model, because neither can be derived: a slot names
a *set* of concepts, and no inspection of a tree says whether a user typing `+` wants an addition or a
syntax error. They differ in where they live, and deliberately: a typing rule is about a concept that does
not exist yet, so it is a list beside the language; a trigger is a property *of* the concept it creates, so
it is declared on that concept.

## What a language declares

A trigger is declared **on the concept it creates**, in that concept's `triggers` list:

```csharp
new ConceptDeclaration(
    ExpressionsConceptKeys.Binary,
    [
        new PropertySlot("operator", ExpressionOperators.BinaryType),
        new ChildSlot("left", ExpressionsConceptKeys.Expression, Cardinality.One),
        new ChildSlot("right", ExpressionsConceptKeys.Expression, Cardinality.One),
    ],
    [ExpressionsConceptKeys.Expression],
    triggers:
    [
        new ConceptTrigger(
            ExpressionsConceptKeys.Expression,
            TriggerSide.Right,
            text => ExpressionOperators.Binary.Contains(text),
            anchorSlot: "left",
            propertySlot: "operator",
            caretSlot: "right"),
    ]);
```

| Member | Answers |
| --- | --- |
| `AnchorConcept` | which concepts it applies to - the node's concept or any of its super concepts |
| `Side` | which end of the node the caret has to stand at |
| `AnchorSlot` | the single-child slot of this concept that receives the node |
| `PropertySlot` | where the typed text goes, or `null` when the text only selects the trigger |
| `CaretSlot` | where the caret continues, or `null` when it stays inside the wrapped node |

**The concept key is absent on purpose**: a trigger always creates the concept that declares it. That is what
makes the two reasons for this location work:

- **Locality.** Every slot it names - the one that receives the node, the property the text goes into, the slot
  the caret continues in - is a slot of the declaring concept, so it sits beside the slots it describes.
- **Reuse.** A language borrows another's concepts with `[.. TheirLanguage.ConceptDeclarations, .. own]`, and the
  gestures travel with them. A registry beside the language would have to be forwarded by hand, and forgetting it
  would silently drop a gesture from a language that looks complete. `RulesLanguage` needs no `Triggers`
  property at all now.

Triggers are **not inherited**: a subtype that reused its super concept's gesture would create the super concept.
A language that wants the gesture below itself declares it again, which also keeps the intent visible.

`ConceptRegistry.TriggersFor(Concept anchor)` answers with every trigger declared for that concept or one of its
super concepts, paired with the concept it would create, **in concept declaration order**. That order is the only
tie-break when two concepts claim the same character on the same side, and it is the same order that ranks
completion entries and the default fill: the language's answer to "what is the user most likely to want here". A
language that wants a narrower reading to win declares that concept first.

## The rule that shapes everything: accepted text wins

A trigger is asked **only where typing would otherwise do nothing**. Inside `TypeTextAction`:

1. the caret is in a property cell and the property accepts the result - it is typed, no trigger is
   consulted;
2. the property refuses it - the trigger is asked, and a match wins over the refusal;
3. the caret is in an empty slot - the typing rules answer, never a trigger;
4. the caret is on any other cell - the trigger is asked.

Case 1 is the one that matters. Typing `-` in front of `5` produces `-5`, which is a number, so the
negation trigger is never reached: a language cannot take a character away from a cell that would
have accepted it. The order is not a nicety either - it is what lets one character mean different
things in different places, and it is why the trigger is *inside* the typing action rather than a
second action under it. A router that asked triggers first would have to re-derive "would the
property accept this", which is the validation the model already owns.

The same rule keeps an operator cell editable: the `operator` of a `Binary` is an enumeration whose
symbols include `*`, so a `*` typed *on that cell* replaces the operator, while the same character
typed after an operand wraps it in a new binary. One character, two meanings, decided by where the
caret is.

## Which node, and which end

`TriggerAnchors.Of` answers "what could this caret be growing from" from the cells, because a
projectional document has no character *between* two nodes:

| Caret place | Anchor | Side |
| --- | --- | --- |
| a property cell, offset `0` | the cell's node | `Left` |
| a property cell, offset `length` | the cell's node | `Right` |
| a property cell, interior offset | none | - |
| a cell the node owns, first in document order | that node | `Left` |
| a cell the node owns, last in document order | that node | `Right` |
| an intermediate cell (a keyword in the middle of a notation) | none | - |
| a node drawn as a single cell | that node | `Right`, then `Left` |

**Ownership, not containment.** The cells counted are the ones whose `CellAddress.Owner` is the node
itself: a child's projection root carries the *parent's* element address and a child's own cells carry
the child's id, so a node's own first and last cell are exactly the edges of the thing the user sees.
A node drawn as one cell has the same place at both ends, so both sides are candidates with the right
one first - "and then continue" is what a user typing after such a node means.

An empty value is both ends for the same reason, and an empty *slot* is no end at all: it has no node
of its own to grow from.

## What a match does

- **A wrap, not a replacement.** `Model.Wrap` puts the node into the new concept and keeps its
  identity, so every reference into its subtree and every caret address still names the same node,
  and one undo puts it back. The new node takes the old one's place in its parent's slot.
- **The typed text is born with the wrapper** (`Model.Wrap(..., presets)`), because the wrapper's id
  does not exist before the wrap runs. A property the wrapper does not declare, or a value its type
  refuses, drops the text and still creates the wrapper: a keystroke must not throw, and "the right
  concept appeared, unfilled" is the visible diagnostic.
- **The caret goes where the user continues.** The trigger's `CaretSlot` when it names one, so
  `5` then `+` leaves the caret in the right operand and the next digit fills it. Without one it stays
  inside the wrapped node, so `!` in front of a condition does not move the user away from it.
- **One undo step, and no coalescing.** `TypeTextAction.CoalescingKey` returns `null` for a keystroke
  that will restructure, so typing `5`, then `+`, then undo gives back `5` and a second undo gives
  back nothing - the operator does not drag the five it was typed after into its own step.

## Guards: a wrong declaration is caught at load, not at the keystroke

**At registration.** `ConceptRegistry.Create` checks every trigger, because none of what it names is visible to
the compiler - the anchor concept is a foreign key and the three slots are strings:

- the anchor concept is declared;
- `AnchorSlot` is a single-child slot the concept declares, and the anchor concept conforms to it;
- `PropertySlot` is a property and `CaretSlot` a child slot the concept declares;
- the concept is concrete, since no node of an abstract concept can exist and triggers are not inherited.

A typo is therefore a `ConceptDefinitionException` when the language loads.

**At the keystroke.** Two questions remain, because registration cannot know where the anchor will stand:
`TypeTextAction` skips a trigger whose concept does not conform to the slot the anchor occupies, and one that its
[`Applicability`](language.md#applicability) forbids there - so a declared-but-impossible gesture does nothing at
all instead of reaching `Model.Wrap`, which would throw. Everything the model would refuse about the wrap itself
was already checked at load.

## What the shipped languages declare

Declared on `Binary`, `Unary` and `Parenthesized` in `ExpressionsLanguage`, so `RulesLanguage` inherits them with
the concepts it borrows:

| Typed | At the edge of | Result |
| --- | --- | --- |
| `+` `-` `*` `/` `==` `&&` ... | any expression, right | `Binary` with the expression on the left, operator filled, caret in `right` |
| `+` `-` `*` ... | any expression, left | `Binary` with the expression on the right, caret in the empty `left` |
| `-` `!` | any expression, left | `Unary` with the expression as its operand |
| `(` | any expression, left | `Parenthesized` around it, caret inside |

A leading minus is a negation and nothing else, so `Binary`'s left trigger does not claim `-`. Leaving the tie to
declaration order would have forced `Unary` above `Binary` in the concept list, and that same order ranks the
completion menu - where a binary is the more likely choice. `-5` is a number, so a minus in front of a literal
never reaches either gesture anyway.

## Testing

Everything is headless, because the decision lives in the editing layer: `TypeTextTriggerTests` builds
`((5))`, puts the caret on a property offset, on the first and on the last cell of a group, and on the
document root, and applies `KeyRouter.Type("+")` - the same call the control makes. It asserts the
model shape, the caret address, the undo depth, and finally that the drawing reads
`((5 + <empty>))`: the wrap is proven visible, not merely present in the tree.

`ConceptRegistryTriggerTests` covers the declaration surface: which concepts a trigger is asked about, the order
the answer comes in, that a subtype does not inherit the gesture, and every way a declaration can be impossible.
`ConceptTriggerTests` covers the argument guards.

## Limits, deliberate

- **One node, one keystroke.** A trigger wraps the node the caret is at the edge of; it cannot wrap a
  multi-cell selection (a selection is typed over, as it always was) or several siblings.
- **No transformation menus.** MPS' "substitution" is a list the user picks from; this is the
  keyboard half of it. The list half is the intention list
  ([context-actions.md](context-actions.md)), which already exists.
- **No visible side-transform affordance.** MPS draws a small clickable region at a node's edge; here
  the caret itself says which side it is on, so there is nothing extra to draw - and nothing extra to
  hit-test.
- **A trigger cannot replace or delete the anchor**, only surround it.
- **A trigger is asked once per keystroke, not re-evaluated on undo.** An undo restores by replaying
  journal closures, which never consult a language.

## Status

Delivered in `feat/concept-triggers` as a registry beside the language, and **moved onto the concept** in
`feat/triggers-on-concepts`: `TriggerSide`, `ConceptTrigger` and `TriggeredConcept` live in
`Projectional.Core/Language/`, `ConceptDeclaration.Triggers` and `Concept.Triggers` carry them, and
`ConceptRegistry.TriggersFor` answers the lookup. That move deleted more than it added - `ITriggerRules`,
`TriggerRegistry`, `EditorSession.Triggers`, the `triggers:` constructor parameter, the `DocumentTab` and
`MainWindow` plumbing and `RulesLanguage.Triggers` all went away, because a language that borrows
`ConceptDeclarations` now gets the gestures with them.

`TriggerAnchor`/`TriggerAnchors` and the trigger path in `TypeTextAction` are unchanged in shape and live in
`Projectional.Editing`. A real-window probe confirmed the whole path: `1000` + `+` in the sample's rule draws
`amount > 1000 + <empty>` with the caret in the new operand and one undo step.
