# Declared editor: a notation written as a model

The second half of "a language is data": how a concept is drawn, written as nodes and lowered into the same
`Notation` objects a language author writes by hand.

Status: **delivered** as slice P2 of [language-authoring.md](language-authoring.md), and the surface now reads the
way MPS writes an editor document - brackets for the cell model. What is still missing are MPS' *cell glyphs* (borders,
collapsible folds, the checkbox of an option), which is P5.

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
| `Editor.Space` | - | a gap |
| `Editor.Keyword` | `text` | a word the notation provides |
| `Editor.Punct` | `text` | a symbol the notation provides |
| `Editor.Property` | `slot` | a property's text |
| `Editor.Reference` | `slot` | a reference's target name |
| `Editor.ChildInline` | `slot`, `emptyText` | one child, inline |
| `Editor.Children` | `slot`, `emptyText`, `spacing` | many children, as blocks |
| `Editor.Collection` | `slot`, `separator`, `emptyText`, `spacing` | many children between separators |
| `Editor.Conditional` | `slot`, `value`, `cells` over `Editor.InlineCell` | drawn only while `slot` holds `value` |
| `Editor.Table` | `slot`, `emptyText`, `columns` over `Editor.Column` | many children as a grid |
| `Editor.Column` | `slot`, `header` over `Editor.InlineCell`, `emptyText` | one column of a table |

**A block inside a line therefore cannot be written.** The model refuses it with `ModelInvariantException` before
lowering runs, which is why the lowering has no inline/block check of its own: the rule moved from the compiler into
the declarations, and there is exactly one place it lives.

`Editor.Conditional` is what keeps the two paths level for a cell that disappears. It carries the same three things
the C# cell carries - a property, the value to compare it with, and the inline cells to draw - and it lowers to the
same `ConditionalNotationCell`, so a notation that says `abstract concept Rule` can be written as a model or by
hand. Its own notation reads `[> if {slot} = "value"` with the cells indented below it.

## The shape: MPS' brackets

A notation is drawn with MPS' own markers, because they are a reading aid that costs nothing to copy: `[/ ... /]`
opens and closes a container that stacks, and `[>` marks one cell of a line.

```text
notations:
  editor for concept Rules.Rule variant: default
  node cell layout:
    [/
      [> line
        [> constant "rule" [> space [> {name}
    /]
```

The brackets say what a reviewer needs to know before running anything: the root of this notation stacks, the thing
inside it is one line, and that line is three inline cells - a constant, a space and a slot. A structure that deep
reads top to bottom.

**One compromise, and it is the engine's.** In MPS the inline cells of a line sit *on* the line. Here a line's
`cells` slot is a `Many` slot, and a many-child cell is a block cell, so `ed.Children(cells)` draws one row per
inline cell - `[> constant "rule" [> space [> {name}` above is three cells side by side because they happen to be
drawn by a collection, not because they are nested. The model is unchanged and so is the reading (`[>` still means
"one cell of a line"); what the notation cannot do is put a marker around each cell *and* keep them on one row. A
`Line`'s children would have to be drawn by a cell type that lays siblings out horizontally *and* brackets each -
that is P5's cell glyphs, not a change to this model.

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
| a line's inline cells drawn on one row *with* their own brackets | P5, with a horizontal sibling cell |
| variants beyond the name (`variant` property) | when a language needs a second drawing of one concept |
| lowering a *whole* language project (structure + editor + dependencies) | P3 |

## Testing

`EditorLoweringTests` declares a notation, lowers it, and then draws a node of the language it is about - the
declared path is only proven by the picture. Covered: the keyword/property/gap line renders as `rule Approved`; a
block inside a line is refused by the model; a notation about an undeclared concept is refused by the lowering; an
undeclared slot is refused by `NotationRegistry`; a variant travels; a named slot keeps the `emptyText` its author
wrote; a declared conditional draws its cells only while the property holds and is validated like a hand-written one; a
definition of a notation is drawn like any other model; and a document that is not a notation is refused.
