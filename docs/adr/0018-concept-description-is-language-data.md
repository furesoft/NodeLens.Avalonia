# ADR 0018 — A concept description is language data, not model data

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR 0002](0002-model-as-source-of-truth.md), [ADR 0007](0007-reflection-free-language-definition.md),
  [ADR 0010](0010-public-api-surface.md),
  [concept-descriptions.md](../architecture/concept-descriptions.md),
  [language.md](../architecture/language.md), [markers.md](../architecture/markers.md),
  [completion.md](../architecture/completion.md)

## Context

A user who meets a language for the first time has one question about a node: what is this thing?
The notation answers it partly - a `rule` keyword suggests a rule - but not what the language means by
it, which slots it carries, or why it exists. MPS answers with a documentation string on the concept
and shows it where the user is: on hover over a node and beside the completion entry that offers it.

The repository had no place for such a text. It also had three plausible places to put one, and they
are not equally good:

- The **node**, as a note the user could edit. Tempting because it needs no language change at all.
- The **notation**, next to the cells that draw the concept. Tempting because the notation is already
  the thing a language author writes.
- The **concept declaration**, where the key, slots and name slot already live.

The choice is expensive to reverse: a text that ends up on the node is persisted in every document
that contains the concept, and one that ends up on the notation splits in two the moment a concept is
rendered more than one way.

## Decision

**A description is declared once per concept, on the concept declaration, and is read by every
surface that shows the concept. It is never stored per node and never inherited.**

| # | Rule | Why |
| --- | --- | --- |
| 1 | `ConceptDeclaration` takes an optional `description`; `Concept` exposes it as `string?`. | One text per concept, owned by the language, next to the key it describes. |
| 2 | It is **not model data**: not editable in the editor, not persisted per node, no undo step. | The model is the program ([ADR 0002](0002-model-as-source-of-truth.md)). A documentation string on a node would be a hundred copies of one sentence, versioned and undoable. |
| 3 | It is **not on the notation**. | A concept drawn as a block and as a table has one description, not two; an abstract concept, or one whose notation is not written yet, still has one. |
| 4 | It is **not inherited** from super concepts. | The super's prose describes the super, not the node the user is looking at. |
| 5 | An absent description means **no surface is shown at all**. | An empty box that only repeats the concept key teaches the user that hovering is pointless. |
| 6 | The declaration **normalizes**: trim, and empty becomes `null`. | "Declared nothing" and "declared a blank" must be one state, or a language ships a tip that shows nothing. |

The description is documentation, so it lives where documentation lives in this design: with the
language, in the layer that already holds notation ([ADR 0007](0007-reflection-free-language-definition.md)), and
it reaches a surface only through a headless type in `Projectional.Editing`
([ADR 0010](0010-public-api-surface.md)) - the same path the marker tip takes.

## Consequences

### Positive

- One field feeds every surface: the hover tip today, the completion panel next, and the language
  browser and model browser later. A language author writes the sentence once.
- A concept with no notation yet can still document itself, which matters because a language is
  written concept by concept and notation is the last thing to arrive.
- Adding it is additive: an optional constructor parameter means no existing language changes and
  `null` is the safe default.
- Because it is not model data, it cannot go out of sync with a document, cannot be restored by undo
  and cannot make a saved file larger.

### Negative / costs

- The text is compiled into the language, so a host that wants to document a borrowed concept must
  redeclare it or wrap it. That is consistent with how the repository already treats borrowed
  declarations, but it is a real limit.
- Two surfaces now read the same field, so a change to how it is displayed is a change in two places.
  The shared wrapper (`TipText.Wrap`) keeps the *wrapping* in one place; the *layout* still differs.
- A hover over ordinary text needs a delay to be usable, so the control gains a second hover rule
  beside the marker tip's immediate one, and a precedence order between them.

### Rules

1. A description never appears in a document, a diff or a change event.
2. The marker tip wins over the concept tip whenever both would apply at one pointer position.
3. A concept tip never shows for a concept with no description, and never shows more than the key and
   the text.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **A per-node note the user can edit** | It is model data, so it is persisted, versioned and undoable - and identical on every node of the concept, which is a hundred copies of one sentence. It also cannot document a concept the document has no instance of. |
| **On the notation** | A concept with two representations would carry two descriptions that must be kept in agreement, and a concept with no notation yet would carry none. |
| **Inherited from the super concept** | The text would describe an abstract base while the user is looking at a concrete node, which is worse than saying nothing. |
| **In a side file or resource, keyed by concept** | A language would no longer be one self-contained declaration, and a typo in the key would silently lose the documentation. |
| **A tooltip generated from the slots** ("has name, condition, outcome") | It is derivable and therefore always right, but it says nothing the notation does not already show, and it cannot say *why* the concept exists. |
