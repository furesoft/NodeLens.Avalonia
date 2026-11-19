# `Projectional.Yaml`

> Part of [import.md](../import.md), the design of record for text import. This file is one
> reference language; the contract, the failure discipline, the conversion rule, the slices and the
> open questions stay in the parent.


YAML is the format whose **layout is part of its syntax**, and that is what makes it the hardest of the five to mirror
honestly: indentation decides where a mapping ends, so the reader has to know block structure rather than see it emerge
from a tree. `YamlDotNet`'s `Scanner` answers that - it resolves indentation into block tokens - and this importer owns
the mapping only, taken with `skipComments: false` so a note is visible at all.

| Concept | Slots | Notes |
| --- | --- | --- |
| `Yaml.Document` | `entries`: many `Yaml.Entry` | the file: its notes, its markers and its node, in order |
| `Yaml.Node` | - | abstract: anything that is a YAML node |
| `Yaml.Entry` | - | abstract: one line of the document or one item of a block sequence |
| `Yaml.Member` | - | abstract: one line of a block mapping |
| `Yaml.FlowValue` | - | abstract: what may stand inside a flow collection |
| `Yaml.Scalar` | `text`: string | the **whole spelling**: quotes, escapes, an anchor, a tag, a block body |
| `Yaml.Alias` | `name`: string | `*base`, kept as text and never resolved |
| `Yaml.Comment` | `text`: string | what the note says, without the hash |
| `Yaml.Raw` | `text`: string | a directive, a marker, a complex key - or an anchored collection |
| `Yaml.Mapping` | `members`: many `Yaml.Member` | one `key: value` per line |
| `Yaml.Sequence` | `items`: many `Yaml.Entry` | one `- value` per line |
| `Yaml.SequenceItem` | `value`: single `Yaml.Node` | the dash and the node it introduces |
| `Yaml.Pair` | `key`: single `Yaml.Node`, `value`: single `Yaml.Node`, `layout`: enum | one line of a block mapping |
| `Yaml.FlowMapping` | `members`: many `Yaml.FlowPair` | `{ a: 1 }` |
| `Yaml.FlowSequence` | `items`: many `Yaml.FlowValue` | `[ 1, 2 ]` |
| `Yaml.FlowPair` | `key`, `value`: single `Yaml.FlowValue` | one member between braces |

Decisions worth stating, because each one is what stops the editor from rewriting a user's file:

- **Block and flow are separate concepts, not one concept with a style.** Notation is chosen per *concept* and a
  variant is view state, so a `style` slot could not have given one node two drawings. Splitting them costs a concept
  each and buys the guarantee: `Yaml.FlowMapping.members` is typed over `Yaml.FlowPair` and its keys and values over
  `Yaml.FlowValue`, which no block collection conforms to - so a block collection inside a flow collection, which YAML
  forbids, is **unrepresentable** rather than merely discouraged.
- **A scalar keeps the whole spelling.** One step further than JSON, XML, TOML and HOCON take it, because YAML has more
  ways to write one value than any of them: `plain`, `'single'`, `"double"`, `|` and `>`, each with anchors and tags in
  front. The slot holds everything, `Notation`/`Generator` emit it verbatim, and no escaping part exists. A cell whose
  text stops being one scalar is `YAML002`, whose repair quotes it as a double-quoted scalar.
- **A pair remembers where its value stands.** A block collection cannot be written on its key's line, and a template
  cannot ask what kind of node its child is, so `Yaml.Pair.layout` records `inline` or `nested` - the file's own
  placement, the shape TOML uses for "a table remembers whether it is a header or inline". Replacing a scalar value with
  a sequence is what breaks it, and `YAML001` offers the placement the child now needs.
- **Anchored and tagged collections are raw text.** A template cannot put a prefix in front of a child whose spelling it
  does not know, so `&base` (or `!!map`) in front of a block or flow collection keeps the **whole node** as
  `Yaml.Raw` with `yaml.kept-as-raw` - tier 2 of the design: the file survives, and the construct is not pretended to
  be modelled. In front of a *scalar* the prefix is one run of text with it, so it stays inside the scalar's spelling
  and nothing is reported.
- **A note is a node, and where it stands is a fact of the file.** A note above a mapping is an entry of the document, a
  note between two pairs is a member of the mapping, and a note that stands between a key's colon and the collection it
  introduces is **held until that collection starts**, so it stays inside the nested list instead of moving out to the
  list that asked for the value. A note inside a *flow* collection has nowhere to live, so it is hoisted to the
  surrounding block list with `yaml.comment-moved`; a note written beside a value is kept, moved to its own line, and
  reported with the same code - the text is never dropped (invariant 8), only its line moves.
- **The line break that ends a line belongs to the file's line structure, not to a spelling.** A block scalar and a kept
  raw span both run to the end of their line, so the importer strips exactly that one break: the model's separators write
  it back, and a spelling that kept it would print the next member one line too low.
- **A complex key is one raw line.** `? [a, b]` has no implicit spelling in a block mapping and no template could express
  one, so the key and its value are stepped over and kept verbatim - built by skipping, never by creating a pair and
  deleting it again, because that would be a second undo step and a second way to build a pair.
- **Line breaks are normalized to LF before the tokens are read**, because a block scalar's body is a slice of the text:
  a carriage return inside it would travel into the model and out into a file the corpus no longer matches.
- **YAML's shape question is broader than HOCON's, so it is asked after it.** Both formats answer yes to `a: value`, and
  YAML also answers yes to `a: { b = 1 }`, which HOCON writes with equals signs inside an object. The file name breaks
  the tie for a named file; `ReferenceLanguages.All` puts YAML before HOCON for the unnamed fallback, because YAML's
  claim is the broader one and importing a colon document as HOCON can refuse a file YAML would have read.

Notation: the document and both block collections as stacks of one line per entry, a sequence item as `-` beside its
value, a pair as `key:` beside its value (inline, because the drawing is not the file), and a flow collection in braces
or brackets with commas between its members. A block collection drawn *inside* a pair's line hangs from the cell it
begins at - `server: port: 8080` then its later lines indented from that column - which is the documented consequence of
asking layout for a vertical notation inside an inline child, the same one HOCON's object shows.

