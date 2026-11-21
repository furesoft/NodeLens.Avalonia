# Declared editor: a notation written as a model

The second half of "a language is data": how a concept is drawn, written as nodes and lowered into the same
`Notation` objects a language author writes by hand.

Status: **delivered** as slices P2 and P5 of [language-authoring.md](language-authoring.md): the surface reads the
way MPS writes an editor document - brackets for the cell model and a glyph per cell (`-word-`, `%slot%`,
`( slot )`, `{ slot }`, `< slot >`, `;`). What is still missing are MPS' *cell* chrome (borders, collapsible folds,
the checkbox of an option), which no notation needs yet.

## One language per aspect, on purpose

A definition that says "a rule is a name, a condition and an outcome" *and* "the name sits after the keyword `rule`"
cannot be reviewed: the first half is the domain, the second is presentation. So the notation is its own model
(one document per notation), written in its own language, and a definition names it separately. That is MPS' split, and it is the
shape the whole plan is built on.

## The language

Seventeen concepts, and the split between them is the interesting part: it is the one rule the C# builder expresses
with its **type hierarchy** - `ed.Line(ed.Indent(...))` does not compile - carried by **slot declarations** instead.

| Concept | Holds | Kind |
| --- | --- | --- |
| `Editor.Notations` | `notations` | the document |
| `Editor.Notation` | `conceptKey`, `variant`, `root` | one concept's notation |
| `Editor.Cell` | - (abstract) | anything drawable |
| `Editor.InlineCell` | - (abstract) | runs inside a line |
| `Editor.BlockCell` | - (abstract) | occupies whole lines |
| `Editor.Line` | `cells` over `Editor.InlineCell` | horizontal |
| `Editor.Sequence` | `cells` over `Editor.BlockCell` | stacked |
| `Editor.Indent` | `cells` over `Editor.BlockCell` | stacked deeper |
| `Editor.Horizontal` | `cells` over `Editor.BlockCell` | the same blocks, in one row |
| `Editor.Space` | - | a gap |
| `Editor.Keyword` | `text` | a word the notation provides |
| `Editor.Punct` | `text` | a symbol the notation provides |
| `Editor.Property` | `slot` | a property's text |
| `Editor.Reference` | `slot` | a reference's target name |
| `Editor.ChildInline` | `slot`, `emptyText` | one child, inline |
| `Editor.Children` | `slot`, `emptyText`, `spacing` | many children, as blocks |
| `Editor.Collection` | `slot`, `separator`, `emptyText`, `spacing` | many children between separators |
| `Editor.Conditional` | `slot`, `value`, `cells` over `Editor.InlineCell` | drawn only while `slot` holds `value` |
| `Editor.WhileFilled` | `slot`, `cells` over `Editor.InlineCell` | drawn only while the child slot `slot` holds a child |
| `Editor.Table` | `slot`, `emptyText`, `columns` over `Editor.Column` | many children as a grid |
| `Editor.Column` | `slot`, `header` over `Editor.InlineCell`, `emptyText` | one column of a table |

**A block inside a line therefore cannot be written.** The model refuses it with `ModelInvariantException` before
lowering runs, which is why the lowering has no inline/block check of its own: the rule moved from the compiler into
the declarations, and there is exactly one place it lives.

`Editor.Conditional` is what keeps the two paths level for a cell that disappears. It carries the same three things
the C# cell carries - a property, the value to compare it with, and the inline cells to draw - and it lowers to the
same `ConditionalNotationCell`, so a notation that says `abstract concept Rule` can be written as a model or by
hand. Its own notation reads `[> if {slot} = "value"` with the cells indented below it.

`Editor.WhileFilled` is its sibling for a slot instead of a value, and it lowers to the same
`WhenFilledNotationCell` the hand-written notation uses: `[> while {slot} is filled`, cells indented below. It
exists for the same reason - a notation that can be written in C# and not as a model would make the two authoring
paths different languages (ADR 0014) - and it is what lets the structure language's header be declared rather than
built when P7 defines the definition languages in themselves.

**The block form of the while-filled cell has no declared twin yet.** `WhenFilledBlockNotationCell` was added when
the concept notation needed a row that disappears - the `implements` list - and the editor language has no
`Editor.WhileFilledBlock` to go with it. ADR 0014 rule 2 is not broken by that direction, since it forbids a
capability only the workbench can express, but the two paths are unequal here and P7 would meet it when the
definition languages are written in themselves.

## The shape: MPS' brackets

A notation is drawn with MPS' own markers, because they are a reading aid that costs nothing to copy: `[/ ... /]`
opens and closes a container that stacks, and `[>` marks one cell of a line.

```text
editor for concept Rules.Rule variant: default
node cell layout:
  [/
    [-rule- ; %name% ; ( body )]
    [>
      [/ table steps empty: + step
        column text as -send- empty: <empty>
      /]
    <]
  /]
```

The brackets say what a reviewer needs to know before running anything: `[/ ... /]` stacks, `[> ... <]` stacks one
level deeper, `[ ... ]` is one row, and each cell carries its own glyph - `-word-` for a keyword, `%slot%` for a
property, `( slot )` for a single child, `{ slot }` for a list, `< slot >` for a reference, `;` for a gap. A
structure that deep reads top to bottom, and the definition of a language looks like the language it defines.

**One compromise, and P5 removed it.** In MPS the inline cells of a line sit *on* the line. Here a line's
`cells` slot is a `Many` slot, and a many-child cell is a block cell, so `ed.Children(cells)` draws one row per
inline cell - `[> constant "rule" [> space [> {name}` above is three cells side by side because they happen to be
drawn by a collection, not because they are nested. What the notation could not do was put a marker around each
cell *and* keep them on one row. `Editor.Horizontal` answers exactly that: the same blocks a sequence holds,
arranged in one row, so a line is drawn as `[` beside its cells beside `]`, each cell keeping its own glyph. Its
C# twin is `ed.Horizontal`, and it is the one cell where the parent decides a direction instead of inheriting it
from the child.

## The lowering

`EditorLowering.Lower(document, concepts)` answers the notations; `CreateRegistry(document, concepts)` answers the
registry a session draws with.

- **It goes through `NotationEditor`, not around it.** The declared path ends up making the same builder calls a C#
  author makes, so a declared notation is not a second kind of notation - the same reason the structure lowering
  produces real `ConceptDeclaration`s. It also means the builder's defaults (an empty child's text, a list's
  spacing) are decided in one place.
- **It checks only what the model cannot**: that a notation names a concept the target registry declares, because
  then the message can say *which* notation is about a concept that does not exist.
- **Everything else belongs to the layers that already own it.** A property cell naming a slot the concept does not
  declare, two notations for one concept and variant, a child cell on a many-valued slot - all of it is
  `NotationRegistry.Add`'s answer, and a second copy here would eventually disagree with it.

## The namespace is `Projectional.Editors`

The plan says `Projectional.Editor`, and the .NET name is plural for two reasons: CA1716 treats `Editor` as a
reserved word in VB, and a namespace `Projectional.Editor` would shadow every `Editor*` type of
`Projectional.Editing` (`EditorTree`, `EditorFrame`, `EditorCell`) from inside its own files. The classes keep the
singular names (`EditorLanguage`, `EditorLowering`, `EditorConceptKeys`), and the notation of the notation lives in
`Projectional.Editors.Rendering` because a namespace called `…Notation` shadows the `Notation` type.

## What is deliberately not here yet

| Missing | Arrives with |
| --- | --- |
| a *block* conditional, hiding a whole line | when a notation needs one |
| MPS' cell glyphs: borders, folds, an option's checkbox | P5 |
| variants beyond the name (`variant` property) | when a language needs a second drawing of one concept |
| lowering a *whole* language project (structure + editor + dependencies) | P3 |

## Testing

`EditorLoweringTests` declares a notation, lowers it, and then draws a node of the language it is about - the
declared path is only proven by the picture. Covered: the keyword/property/gap line renders as `rule Approved`; a
block inside a line is refused by the model; a notation about an undeclared concept is refused by the lowering; an
undeclared slot is refused by `NotationRegistry`; a variant travels; a named slot keeps the `emptyText` its author
wrote; a declared conditional draws its cells only while the property holds and is validated like a hand-written one; a
definition of a notation is drawn like any other model; and a document that is not a notation is refused.
