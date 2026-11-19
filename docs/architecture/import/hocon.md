# `Projectional.Hocon`

> Part of [import.md](../import.md), the design of record for text import. This file is one
> reference language; the contract, the failure discipline, the conversion rule, the slices and the
> open questions stay in the parent.


HOCON is JSON's brace-light cousin and the config format of the .NET actor and streaming world.
It is the hardest of the five to be honest about, because a HOCON file is *evaluated*: keys merge,
substitutions resolve, includes pull in other files.

| Concept | Slots | Notes |
| --- | --- | --- |
| `Hocon.Document` | `members`: many `Hocon.Member` | the file: the object every file is, written without braces |
| `Hocon.Member` | - | abstract: one entry of a document or an object |
| `Hocon.Value` | - | abstract: anything a property can hold |
| `Hocon.Object` | `members`: many `Hocon.Member` | every nested object, between braces |
| `Hocon.Property` | `key`: string, `operator`: enum, `value`: single `Hocon.Value` | `key = value`, `key: value`, `key value`, `key += value` |
| `Hocon.Array` | `items`: many `Hocon.Value` | commas optional in the source, model order kept |
| `Hocon.String` | `quoted`: enum, `text`: string | quoted, triple-quoted or unquoted |
| `Hocon.Number` | `text`: string | the literal |
| `Hocon.Bool` | `value`: enum `true`, `false` | |
| `Hocon.Null` | - | a concept, as in JSON |
| `Hocon.Concat` | `parts`: many `Hocon.Value` | `hello ${name}` - juxtaposition is concatenation |
| `Hocon.Substitution` | `path`: string, `optional`: enum | `${a.b}` and `${?a.b}`, kept as written |
| `Hocon.Include` | `target`: string | `include "x.conf"`, kept as written |
| `Hocon.Comment` | `marker`: enum `#`, `//`, `text`: string | the marker is a slot, because the file used one of two |

Two unions and a document rather than one object, and both follow from the format: a note and an include stand where a
property stands but are not values, so `Hocon.Member` names that set - and a file is one brace-less object, which a
document concept says without letting a nested object be written without braces.

- **A key keeps the text it was written with.** Dotted paths, quoting and unquoted keys are three
  spellings of one path, and resolving them into nesting would rewrite the file - the TOML decision
  and the XML decision, for the third time, which is what makes it a rule rather than a choice. The
  quotes of a quoted key stay in the slot for the same reason: `<a>` in XML is not the only name
  whose spelling matters.
- **Repeated keys are kept, in order.** HOCON's merge semantics fold them into one object at
  resolution time; the text has two, and the model is the text's owner, not the resolver's. This is
  the opposite of the TOML decision and follows from the formats, not from a preference.
- **Sound-spelling is a slot, not a guess.** `Hocon.Property.operator` keeps `=`, `:` or the
  whitespace separator, and `+=` is a fourth value rather than a resolved append; `Hocon.String.quoted`
  keeps `foo` unquoted, `"foo"` quoted and `"""foo"""` triple-quoted apart.
- **Nothing is evaluated, and nothing is read.** A substitution stays a substitution, an `include`
  stays an `include`, and the framework **never follows an include**: import takes one string from
  the host (`Import(Model, string)`), and a file read would be both a purity break and the same
  class of vulnerability XML's refused external entities exist to prevent. That is not a limitation
  to lift - a host that wants the resolved configuration evaluates it and imports *that*.
- **Concatenation is its own concept** because HOCON makes juxtaposition meaningful
  (`greeting = hello ${name}`), and flattening it into one string would lose the substitution
  inside it. `Hocon.Concat` is the honest shape; an unquoted multi-word string is a `Concat` of
  unquoted strings, which is what HOCON says it is.

Notation: an object as an `Indent` block of `key = value` lines under a `{` and `}`, the operator a
property's own cell (so `=`, `:` and `+=` are visible as written), a substitution and an include as
the one line they are - which is what makes a HOCON document readable in the editor as the thing a
user typed rather than as a resolved tree. The drawing of an object that starts inside a line hangs,
because the layout indents a vertical notation from the cell it begins at; the file a template writes
is indented from the line start, and that is the template's business.

