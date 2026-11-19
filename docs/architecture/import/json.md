# `Projectional.Json`

> Part of [import.md](../import.md), the design of record for text import. This file is one
> reference language; the contract, the failure discipline, the conversion rule, the slices and the
> open questions stay in the parent.

| Concept | Slots | Notes |
| --- | --- | --- |
| `Json.Value` | - | abstract; every slot typed over a value uses it |
| `Json.Member` | - | abstract; one entry of an object |
| `Json.Object` | `members`: many `Json.Member` | member order is model order |
| `Json.Property` | `name`: string, `value`: single `Json.Value` | duplicate names are kept and reported |
| `Json.Comment` | `text`: string | a note JSON cannot express; generation leaves it out |
| `Json.Array` | `items`: many `Json.Value` | |
| `Json.String` | `text`: string | the unescaped value |
| `Json.Number` | `text`: string | **the literal text**, not a double |
| `Json.Bool` | `value`: enum `true`, `false` | an enum property, so completion offers the two values |
| `Json.Null` | - | a concept, not an absent value |

Two decisions worth stating:

- **A number keeps its source text.** `1.0`, `1`, `1e3` and `0.1000000000000000055511151231257827`
  are four different files. A `double` property would silently rewrite all of them on the next
  generation, and JSON consumers do care (a `1.0` is a float to some, an integer to others).
- **`true`/`false` is an enum property**, so the delivered enum completion (see
  [completion.md](../completion.md#values)) offers exactly those two symbols in the cell, and the
  notation can render them as keywords.
- **A comment is a model-only note.** JSON has no syntax for one, so `Json.Comment` is an entry of an
  object that the generator deliberately leaves out - the empty template, not a missing one, because
  a silence somebody decided is not a hole (see
  [semantics.md](../semantics/types.md#what-is-implemented) and `TextGen.Ignore`). It is kept in the model
  because a note is what makes a generated document reviewable, and an editor that dropped it would
  delete the author's explanation of their own model. An import never produces one: a JSONC file is
  still refused rather than half-understood, because a round trip would silently lose its comments.
  One cosmetic consequence is recorded rather than solved: the two spaces inside the braces are chosen
  by whether the members slot holds a child, so an object whose only entry is a note prints `{  }` -
  valid JSON, and the price of not having a second answer to "is this object empty".

Notation: the file's own line, because the layout engine does not wrap and a projection that re-broke the lines would
show a shape the file does not have - an object as `{ name: value, ... }`, an array as `[ 1, 2 ]`, with `Punct(",")`
and a plain space as the separator. The gap inside a bracket is a `WhenFilled` cell, which is how an object with a member
gets its spaces while an object with none still shows the cell the first member is typed into. So the drawing matches the
generated text everywhere except an empty object or array, which draws `{ }` / `[ ]` and prints `{}` / `[]` - the
brackets are unconditional, only their inner space is conditional. No new cells.

