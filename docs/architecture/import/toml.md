# `Projectional.Toml`

> Part of [import.md](../import.md), the design of record for text import. This file is one
> reference language; the contract, the failure discipline, the conversion rule, the slices and the
> open questions stay in the parent.


TOML is the format a .NET project writes its settings in, and it is the least JSON-like of the
four: a document is an ordered sequence of key/value pairs and headers, and **the order is the
structure** - `[a]` opens a table and every following pair belongs to it until the next header.

| Concept | Slots | Notes |
| --- | --- | --- |
| `Toml.Document` | `entries`: many `Toml.Entry` | model order is document order |
| `Toml.Entry` | - | abstract: one top-level line group |
| `Toml.KeyValue` | `key`: string, `value`: single `Toml.Value` | a pair in the root table |
| `Toml.Table` | `key`: string, `entries`: many `Toml.Entry` | `[a.b]`, followed by its pairs |
| `Toml.ArrayOfTables` | `key`: string, `entries`: many `Toml.Entry` | `[[a]]`, one element |
| `Toml.InlineTable` | `members`: many `Toml.KeyValue` | `{ a = 1 }`, a value rather than a header |
| `Toml.String` | `kind`: enum, `text`: string | the **unescaped** value |
| `Toml.Integer` | `text`: string | the literal, so `0x2A` and `42` stay different files |
| `Toml.Float` | `text`: string | the literal, `inf` and `nan` included |
| `Toml.Bool` | `value`: enum `true`, `false` | an enum property, as `Json.Bool` is |
| `Toml.DateTime` | `kind`: enum, `text`: string | offset date-time, local date-time, local date, local time |
| `Toml.Array` | `items`: many `Toml.Value` | may span lines; the model keeps the order |
| `Toml.Comment` | `text`: string | `# ...` |

Decisions worth stating, because each one is what stops the editor from rewriting a user's file:

- **`Toml.String` keeps the kind of string it is.** Basic, multi-line basic, literal and
  multi-line literal are four spellings of one value, and `'C:\temp'` rewritten as `"C:\\temp"` is
  a diff a reviewer has to explain. The kind is a slot and the text is what sat **between the
  delimiters**, so the four spellings are one concept and a template emits the text verbatim - the
  same decision JSON and XML make, and the reason no escaping step exists anywhere in the layer.
  A string whose text can no longer sit between its delimiters is `TOML004`, whose repair re-spells
  it as a basic string.
- **A number and a date keep their source text**, for the reason `Json.Number` does, and stronger:
  TOML's four date/time kinds are different *types*, and a single `DateValue` property would lose
  the kind, the offset and the sub-second precision.
- **A table remembers whether it is a header or inline.** `[a]` and `a = { }` are not the same
  document even when the trees are equal - a table defined by headers cannot be moved into an
  inline one without changing what an order-sensitive reader sees, and `[[a]]` is a third spelling.
- **A dotted key stays the text it was written as**, exactly as an XML namespace prefix does:
  `a.b = 1` keeps `a.b` as the key, and is *not* resolved into nesting. Resolving it would rewrite
  the file on the first save.
- **A duplicate key is refused, not kept.** This is the one place the reference languages disagree on
  purpose: JSON's duplicate member is legal-but-discouraged and the model keeps both (generation
  must reproduce the file), TOML makes it a syntax error, so the reader rejects the document with
  `toml.duplicate-key` and nothing is imported. A language does not get to be more permissive than
  the format it mirrors.
- **A comment is a node.** TOML files are hand-edited settings, and dropping the comments of a file
  the user imported would be data loss (invariant 8). One limitation is recorded rather than
  solved: a comment that trailed a value on the same line becomes its own entry above it, because
  the model has no "same line" notion - the corpus is the generator's own output, so a round trip
  never sees it. A comment that stood **inside an array** has nowhere to live in this model at all,
  so it becomes an entry of the table the pair belongs to and the import reports
  `toml.comment-moved`; the text is kept and only its line moves.

Notation: the document as a stack of entries, a key/value pair as `key = value` in one line, a table
as `[key]` followed by its entries indented under it, `[[key]]` for an array of tables, and an inline
table as `{ ... }` in the line where it stands - the ordinary block vocabulary, because TOML's
structure *is* its layout.

