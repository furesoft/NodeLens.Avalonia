# ADR 0012 — A concept may be rendered more than one way

- Status: Accepted
- Date: 2026-11-28
- Related: [0002](0002-model-as-source-of-truth.md), [0009](0009-custom-drawn-editor-control.md),
  [projection](../architecture/projection.md)

## Context

Until now a concept had exactly one notation, and `NotationRegistry` enforced it: a second
notation for the same concept key was an error. That is the simplest rule that can work, and
it is the rule MPS actually uses — one *Editor* aspect per concept — so it is not an
oversight.

It is, however, too narrow for what a DSL author wants in practice. The same `Rule` reads
well as a sentence (`rule Approved when amount > 1000 then Accept`) and badly as a summary;
ten of them read well as a table and badly as ten sentences. A `Ruleset` is a brace-delimited
list of children for editing and a dashboard for reviewing. MPS solves part of this with
conditional cells inside one editor and the rest with custom `EditorCell` implementations,
which means the second representation is written in Java and is not something a language
author can declare.

The question is therefore: **may one concept have several notations, and if so, who decides
which one is used?**

Three answers are possible and all three have been built by somebody:

1. **One notation per concept, with conditionals inside it.** MPS's answer. Cheap, but every
   additional representation multiplies the conditional structure of the one notation, and
   "show me the table" becomes impossible to say.
2. **A second, parallel view outside the projection** — a grid widget that renders the model
   itself. This is what most "diagram views" are. It duplicates layout, hit testing and the
   caret, and it is read-mostly: editing in it means writing a second editor.
3. **Several notations per concept, chosen at projection time.** One mechanism, one code path,
   and a table is a notation like any other, so editing, the caret, selection and hit testing
   keep working for free.

## Decision

**A concept may declare several notations. Each has a variant name, and the projection picks
one per concept when it builds the tree.**

The mechanism is deliberately small:

| Piece | Role |
| --- | --- |
| `Notation.Variant` | the name of one notation of a concept, e.g. `"default"`, `"table"` |
| `NotationRegistry` | keeps every notation per concept, rejects a duplicate `(concept, variant)` pair |
| `INotationChoice` | answers "which variant does *this view* want for concept *X*" |
| `NotationChoice` | an immutable implementation of it, with `With`/`Without` |
| `EditorTreeBuilder` | resolves `concept -> variant -> notation` once per node, as it always did |

The registry used to answer "the notation of `Rules.Rule`"; it now answers "the notation of
`Rules.Rule` *for this choice*". That is one extra argument, threaded exactly where emphasis
was already threaded, and nothing about the projection changes as a result.

### The choice is view state, never model state

A variant is chosen by whoever is looking, not by whoever wrote the model. Nothing about it is
stored in the model, in the XML, or on a node: the same document opens as text in one pane and
as a table in another, and an edit made in either appears in both. This is the same rule as
[ADR 0002](0002-model-as-source-of-truth.md) — the model says what exists, the projection says
what it looks like — and it is what makes a table trustworthy rather than a report: delete a
row in the table and the sentence in the text view loses its rule.

The default is: **no choice, no opinion.** An empty `NotationChoice` means every concept uses
its `"default"` variant, which is what `NotationRegistry.Add(conceptKey, build)` registers, so
every language written before this ADR keeps behaving identically.

### Resolution follows the variant first, then the default

`NotationRegistry.FindFor(concept, choice)` answers in this order:

1. the concept's own notation with the chosen variant,
2. the variant inherited from the nearest super concept that has it,
3. the concept's own default notation,
4. the nearest super concept's default notation,
5. nothing — which the builder turns into a `MissingNotationCell`, exactly as before.

Steps 1–2 before 3–4 is the point: asking for a table must not silently produce a *text*
notation just because the concept's own default happens to exist, or a language could not
declare "only my abstract super concept knows how to be a table".

### A variant may hide what another shows

The invariant is **not** "every variant shows every slot". A table that showed a rule's
condition would not be a table. What is guaranteed instead is that every slot a variant *does*
show keeps its natural address, so the caret, the selection, hit testing, undo and every edit
action work unchanged in every variant, and a slot hidden in one variant stays editable in the
other. A variant that shows a slot differently is a presentation; a variant that shows a
*different model* would be a second model, and that is not what this is.

Consequences worth stating plainly:

- An edit action that assumes a cell exists in the current variant does not. This is the same
  situation as an unset property or an empty child slot today, and the answer is the same: the
  action asks the tree, not the notation.
- A caret position that survives a rebuild may not survive a *variant switch*, because the
  cells it named may not exist in the new variant. `EditorSession` re-resolves it to the
  nearest place, which is the same rule undo already uses.

### The first non-text representation: a table

Multiple variants would be a promise without a second kind of cell if everything were still a
line of text. So this ADR also adds one block notation cell, `TableNotationCell`, and the
runtime `TableCell` it produces:

```csharp
notations.Add(RulesConceptKeys.Ruleset, "table", ed => ed.Sequence(
    ed.Line(ed.Keyword("ruleset"), ed.Space, ed.Property("name")),
    ed.Table(
        "rules",
        ed.Column("Name", "name"),
        ed.Column("Outcome", "outcome"))));
```

Three decisions in that cell are worth their own paragraph.

**Columns name slots of the row concept, they do not contain notations.** A column is a
`(header, slot)` pair and the slot must be a property or a single-child slot of the child
concept of the table's slot. A column that accepted an arbitrary notation would be a notation
inside a notation, and the table would have to arrange arbitrary trees in a grid — the point of
a table is that its rows are regular. A cell that needs something irregular uses the row's own
notation through the `"default"` variant, which is what the text view shows.

**The grid is a layout concern, not a model one.** `CellLayouter` measures every cell first,
takes the widest cell per column, then places each cell in its column and row. Column widths
are therefore shared across rows — that is the whole difference between a table and a stack of
lines — and a row is as tall as its tallest cell. Because the placement list is still flat and
in document order, painting, hit testing, the caret, the selection rectangles and the headless
snapshot all work on a table without knowing that tables exist. Nothing in the editing layer
learns a new word.

**Addresses are the row's own.** A table cell for the `name` property of the third rule carries
exactly the address that property carries in the text view. This is what makes the two views
two views: the same cell address is reachable through both, so undo, selection and every edit
land in the same place regardless of how the user got there.

## Consequences

### Positive

- A language author can offer text *and* a table without writing a renderer, and a host can
  offer both at once (two panes, two choices) because the choice is not global state.
- Every existing mechanism — caret, hit test, selection, undo, emphasis, highlighting,
  snapshots — works in the new representation with no per-representation code. The test for
  "does a table edit" is an ordinary edit test.
- A table is testable headlessly through `LayoutSnapshot`, like every other notation.
- Nothing about the model changed, so persistence, change events and the XML format are
  untouched.

### Negative / costs

- **Two views of one document can disagree about the caret.** Switching a variant rebuilds the
  tree and the caret is re-resolved to the nearest cell, which reads as a small jump. There is
  no shared "editing context per node" yet.
- **A grid is not a row of text.** Arrow-key navigation still walks document order, which for a
  row-major table means left-to-right and then down across the row boundary. Moving *down a
  column* needs column-aware navigation, which does not exist yet.
- **A table normalises.** A rule whose condition is a two-line expression cannot be a table row,
  so the table variant has to hide it. Hiding is allowed and documented, but a language author
  who forgets that a hidden slot is uneditable in that view will get a bug report.
- **Column widths are computed per table, not per column of the whole document**, so two
  separate tables in one document line up only with themselves.
- The registry API grew a variant parameter on five methods. Callers that do not care keep
  calling the old overloads, so the cost is in implementation, not in usage.

### Open follow-up

Recorded in [open-questions.md](../open-questions.md) with decide-by milestones:

- choosing a variant **per node** rather than per concept (M4), and what that means for a
  choice that has to be keyed by node id;
- persisting a document's choice per file (workspace settings, M3);
- column-aware caret navigation and a "next row" gesture (M3);
- custom, host-implemented cells for representations a declarative notation cannot express —
  a chart, a map, an image — with the contract that such a cell must still report addresses
  (M5);
- column widths shared across a document, and column resizing (M3).

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| **Conditional cells inside one notation** (MPS's `if`/`else` cells) | Composes badly: ten representations make one notation whose branches nobody can read, and the user still cannot *ask* for the table. Conditionals are still worth having for small variations, and are not excluded by this ADR. |
| **A second widget that renders the model itself** | It re-implements layout, hit testing and the caret, and it either is read-only or becomes a second editor. Everything the projection already solved would be solved twice, differently. |
| **A "view" that replaces the whole tree from outside the notation system** | Loses the addresses. A table built outside the projection would have to invent node-to-cell mapping, and undo/selection would no longer name the same place. |
| **Storing the chosen variant in the model** | Presentation in the model is exactly what [ADR 0002](0002-model-as-source-of-truth.md) forbids: the same document could no longer be read by two people differently, and a saved file would carry a decision that has nothing to do with its content. |
| **One variant per document instead of per concept** | Too coarse. A DSL usually wants the text view for the concept being edited and a summary for its container; a document-wide switch forces an either/or. |
| **Variant chosen by the notation itself (a root-level delegate)** | Would make the registry impure and the projection non-deterministic given `(node, notation)`, which is what makes rebuilds harmless today. |
