# Text import

A projectional editor has no parser, on purpose: the model is the only source of truth
([ADR 0002](../adr/0002-model-as-source-of-truth.md)). Import is the one place where text
enters a model, and it does so **once**, from a string the host supplies, after which the
string is thrown away. Nothing writes text back into the model, so the invariant is intact:
there is still no text buffer and no parser in the editing path.

Status: **planned**, M6. Generation is the opposite direction and is planned in
[semantics.md](semantics.md#generation).

## Two directions, and only one of them is a parser

| Direction | When | Who calls it | Where the bytes come from |
| --- | --- | --- | --- |
| Generation (model to text) | M4 | the host, on demand | the framework returns a string; the host writes the file (open question 12) |
| Import (text to model) | M6 | the host, once | the host reads the file and passes the string; the framework never opens a file |

The round-trip property that makes this testable is stated over **text**, not over models:

> `generate(import(text))` equals `text`

and it holds for text that the reference languages map completely. It cannot be equality of
models, because import mints fresh node ids (identity is a stable GUID,
[ADR 0004](../adr/0004-stable-node-ids.md)) and because a model may legitimately be re-imported
into a different project.

What import is **not**, and this is the part that keeps the architecture honest:

- **Not a merge.** Importing into a model that already has content appends new roots; it does
  not reconcile, diff or match nodes.
- **Not a sync, and not a watcher.** Once import succeeds the model is the source of truth.
  The file on disk is not linked to it, editing it does not change the model, and saving
  writes a `.mps` document (or generated text), never the original file. A host that wants
  "the file changed on disk, now what?" behaviour owns that decision (open question 23).
- **Not a second source of truth.** The imported text is not retained anywhere except in the
  nodes that carry it (see the raw-text tier below).

## Where the code lives

The contract belongs next to the other language-level statements in `Projectional.Language`,
for the same reason `ICellEmphasis` lives there: a language package must be able to declare
one without referencing the projection layer.

```
Projectional.Language/Import/
    ITextImporter.cs        // Format, CanImport, Import
    ImportResult.cs         // the roots that were added, plus diagnostics
    ImportDiagnostic.cs     // line, column, length, code
```

A language package implements it and exposes an instance next to its concept declarations, the
way `ExpressionsLanguage` exposes `ConceptDeclarations`:

```csharp
public static class JsonLanguage
{
    public static ITextImporter Importer { get; }
}
```

`ITextImporter.Import` fills a `Model` the host already created:

```csharp
ImportResult Import(Model model, string text);
```

**Creating the document stays the host's job.** The framework does not know whether the user
is importing into the current document, a new tab or a scratch model, and `Model`/`Project`
creation is already a host concern in persistence (`ModelFile.Load` creates its own, an empty
document is created by the shell).

### Why no new `InternalsVisibleTo` seam

An importer is a bulk mutation, so the obvious question is whether it needs the two Core seams
persistence uses (`Model.CreateDetached`, `Project.DiscardModel`). It does not:

- **Building uses the public, journaled API.** `Model.NewNode`, `Model.NewChild` and
  `Model.AddRoot` are public and record their own changes, which is exactly what an importer
  needs: the import becomes one undoable step and one change event.
- **Rollback is already free.** `Model.BeginTransaction()` returns a `ModelTransaction` whose
  `Dispose()` without `Complete()` rolls the model back to the mark taken at the start. A
  failed import therefore needs no cleanup code and cannot leave half a document behind:

  ```csharp
  using var transaction = model.BeginTransaction();
  // build; may throw or return a failure result
  transaction.Complete();   // only on success
  ```

- **Import never needs a detached node or a throwaway model.** It appends to a model the host
  owns, and it never has to pick an id from a file.

Keeping the seams closed matters: `CreateDetached(id, concept)` deliberately skips journaling,
which is correct for loading and wrong for a mutation.

## Failure discipline

Two tiers, because they answer different questions.

**Tier 1 - the text does not parse.** The whole import is refused, the transaction is disposed
without `Complete()`, the model is unchanged, and the result carries one diagnostic. Refusing
everything is the only honest answer: a JSON file with a missing brace has no partially
meaningful model, and a half-imported document that looks plausible is worse than no document.

**Tier 2 - the text parses but the language has no concept for a construct.** The construct is
preserved as a `RawText` child (a concept holding the original source slice) so that generation
reproduces the input, and a diagnostic is reported at `Warning`. This is invariant 8 ("never
drop data on load") applied to import: an editor that silently discards a construct it does not
model is an editor that silently rewrites the user's file.

**Diagnostics are not `Diagnostic`.** The existing record targets a `NodeId`
([semantics.md](semantics.md#diagnostics)), and a syntax error has no node - that is the whole
problem. So import has its own small record:

| Field | Meaning |
| --- | --- |
| `Line`, `Column` | 1-based, counted in the *imported text*, so a host can show a caret in the source |
| `Length` | how many characters the construct spans, for an underline |
| `Code` | a stable short string (`json.unexpected-token`, `xml.external-entity-refused`, ...) |
| `Message` | human text |

A host that wants import errors in the problems panel maps them onto its own list. They are
**not** diagnostics on the model, because the model is exactly what does not exist yet.

**One undo step.** The single transaction means `Ctrl+Z` after an import removes the whole
imported document, which is the only behaviour a user can predict.

## The reference languages

Four languages ship as packages, for the same reason `Projectional.Expressions` exists: they
are the executable proof that the layer composes, and they give the editor something to open
that was not authored in it.

They are also **conversion targets**, and that is the role that matters once a user has a model
of their own: a language package that maps its concepts into one of these models gets a correctly
spelled file for free - quoting, escaping, table nesting, key quoting, comment syntax are that
language's own generator's business, not the mapping language's and not a template's. The mapping
is the two-stage pipeline of [model-generation.md](model-generation.md) (reduce into the target's
model, let the target's own text generator print it), and there is deliberately no shortcut:
see [Converting a model into one of these languages](#converting-a-model-into-one-of-these-languages)
for why a template-level escaping part is the wrong answer.

| Language | Package | What it adds beside the other three |
| --- | --- | --- |
| JSON | `Projectional.Json` | the smallest complete format; objects, arrays, four value kinds |
| XML | `Projectional.Xml` | attributes, mixed content, whitespace as data, comments |
| TOML | `Projectional.Toml` | **tables and arrays of tables**, four kinds of string, dates and times as types, comments, a document whose order *is* its structure |
| HOCON | `Projectional.Hocon` | a JSON superset whose braces and commas are optional, unquoted keys and values, value concatenation, and constructs that are *evaluated* rather than read (`${...}`, `include`, `+=`) |

Two of them parse with the platform library, not with a hand-written parser. A hand-written JSON or
XML parser is a liability with no upside here; the point of the exercise is the mapping, not
the grammar. .NET has no TOML or HOCON reader in the box, so those two take a package dependency -
in the reference language's own package, never in the toolkit (see open question 98).

**What a reference language owns.** Every spelling its format has: how a string is quoted and escaped, how a key is
written, how a comment is written, how a date and a number are spelled. That is not a property of a *template* and not
a property of the framework's template vocabulary - it is the business of the language that mirrors the format, in its
own generator, exactly as the notation of a concept belongs to the language that declares it. A second language that
targets the same file format then reuses that language instead of re-deriving its rules.

### `Projectional.Json`

| Concept | Slots | Notes |
| --- | --- | --- |
| `Json.Value` | - | abstract; every slot typed over a value uses it |
| `Json.Object` | `members`: many `Json.Property` | member order is model order |
| `Json.Property` | `name`: string, `value`: single `Json.Value` | duplicate names are kept and reported |
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
  [completion.md](completion.md#values)) offers exactly those two symbols in the cell, and the
  notation can render them as keywords.

Notation: an object as an `Indent` block of `name: value` lines with `Punct(",")` separators, an
array as a `Line` with `Punct(", ")` - the ordinary notation vocabulary, no new cells.

### `Projectional.Xml`

| Concept | Slots | Notes |
| --- | --- | --- |
| `Xml.Node` | - | abstract: the children of an element |
| `Xml.Element` | `name`: string, `attributes`: many `Xml.Attribute`, `children`: many `Xml.Node` | |
| `Xml.Attribute` | `name`: string, `value`: string | order preserved |
| `Xml.Text` | `text`: string | **verbatim**, including whitespace-only text |
| `Xml.Comment` | `text`: string | |
| `Xml.Raw` | `text`: string | tier 2: processing instructions, CDATA, DOCTYPE |

- **Namespaces are text.** `p:name` is the name; there is no namespace resolution, no prefix
  table, no `xmlns` handling beyond "it is an attribute like any other". Resolving them is a
  schema feature, not an editor feature.
- **Whitespace is content.** `XDocument.Load` with `LoadOptions.PreserveWhitespace`, and
  `Xml.Text` stores the run verbatim. XML consumers that care about `xml:space` would otherwise
  see their file rewritten by the editor.
- **External entities are refused.** The reader is configured with `DtdProcessing.Prohibit`
  and `XmlResolver = null`, and a document that needs either is rejected with
  `xml.external-entity-refused`. An editor that dereferences a `SYSTEM` identifier on import is
  an XXE vulnerability in every host that embeds it. This is not a limitation to lift later.
- **Attribute order is preserved**, because `XDocument` preserves it and because some tools
  diff attributes positionally.

### `Projectional.Toml`

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
  a diff a reviewer has to explain. `text` is the unescaped value; the kind is a slot.
- **A number and a date keep their source text**, for the reason `Json.Number` does, and stronger:
  TOML's four date/time kinds are different *types*, and a single `DateValue` property would lose
  the kind, the offset and the sub-second precision.
- **A table remembers whether it is a header or inline.** `[a]` and `a = { }` are not the same
  document even when the trees are equal - a table defined by headers cannot be moved into an
  inline one without changing what an order-sensitive reader sees, and `[[a]]` is a third spelling.
- **A dotted key stays the text it was written as**, exactly as an XML namespace prefix does:
  `a.b = 1` keeps `a.b` as the key, and is *not* resolved into nesting. Resolving it would rewrite
  the file on the first save.
- **A duplicate key is refused, not kept.** This is the one place the four languages disagree on
  purpose: JSON's duplicate member is legal-but-discouraged and the model keeps both (generation
  must reproduce the file), TOML makes it a syntax error, so the reader rejects the document with
  `toml.duplicate-key` and nothing is imported. A language does not get to be more permissive than
  the format it mirrors.
- **A comment is a node.** TOML files are hand-edited settings, and dropping the comments of a file
  the user imported would be data loss (invariant 8). One limitation is recorded rather than
  solved: a comment that trailed a value on the same line becomes its own entry above it, because
  the model has no "same line" notion - the corpus is the generator's own output, so a round trip
  never sees it.

Notation: the document as a stack of entries, a key/value pair as `key = value` in one line, a table
as `[key]` followed by its entries indented under it, `[[key]]` for an array of tables, and an inline
table as `{ ... }` in the line where it stands - the ordinary block vocabulary, because TOML's
structure *is* its layout.

### `Projectional.Hocon`

HOCON is JSON's brace-light cousin and the config format of the .NET actor and streaming world.
It is the hardest of the four to be honest about, because a HOCON file is *evaluated*: keys merge,
substitutions resolve, includes pull in other files.

| Concept | Slots | Notes |
| --- | --- | --- |
| `Hocon.Object` | `members`: many `Hocon.Value` | the root, and every nested object |
| `Hocon.Property` | `key`: string, `operator`: enum, `value`: single `Hocon.Value` | `key = value`, `key : value`, `key value`, `key += value` |
| `Hocon.Array` | `items`: many `Hocon.Value` | commas optional in the source, model order kept |
| `Hocon.String` | `quoted`: enum, `text`: string | quoted, triple-quoted or unquoted |
| `Hocon.Number` | `text`: string | the literal |
| `Hocon.Bool` | `value`: enum `true`, `false` | |
| `Hocon.Null` | - | a concept, as in JSON |
| `Hocon.Concat` | `parts`: many `Hocon.Value` | `hello ${name}` - juxtaposition is concatenation |
| `Hocon.Substitution` | `path`: string, `optional`: enum | `${a.b}` and `${?a.b}`, kept as written |
| `Hocon.Include` | `target`: string | `include "x.conf"`, kept as written |
| `Hocon.Comment` | `text`: string | `# ...` and `// ...` |

- **A key keeps the text it was written with.** Dotted paths, quoting and unquoted keys are three
  spellings of one path, and resolving them into nesting would rewrite the file - the TOML decision
  and the XML decision, for the third time, which is what makes it a rule rather than a choice.
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
user typed rather than as a resolved tree.

All four packages reference `Projectional.Language` only, exactly like `Expressions`; the two that
need a reader take it as an ordinary package dependency (open question 98).

## Converting a model into one of these languages

The reason the four formats are **languages** rather than a printing mode is this: converting a
model into JSON, TOML or HOCON produces a **model of that language**, and that language's own text
generator prints it. Nothing else knows how to spell the format, so the spelling exists once.

That answers the question a conversion always raises - who escapes the value? - with "the language
that owns the format, in the same place it always does". A user's language maps its concepts into
`Json.Object`, `Toml.Table` or `Hocon.Object` nodes, exactly as
[model-generation.md](model-generation.md) describes, and never writes a quote, a backslash, a
table header or a comment marker:

```
Rules.Rule            ->  Json.Object { "name": Json.String, "outcome": Json.String }
Pipeline.Pipeline     ->  Toml.Document [ Toml.KeyValue("name", ...), Toml.Table("stages", ...) ]
```

Three consequences, and the third is the one that keeps the framework honest:

- **The target is validated by the target's own language.** A value that the target cannot hold is
  a mapping defect the target's checks report, at a node, in the editor - not a malformed file that
  someone discovers when a tool refuses to read it.
- **The four formats stay symmetric.** Import and generation are two directions of *one* language,
  so a round trip is a property of that language and a corpus can pin it. A printing mode would
  need its own corpus and would drift from the importer's idea of the same syntax.
- **There is no per-value "as JSON" part in the template vocabulary, and that is deliberate.** Such
  a part would be a second answer to "how is this value written" - one in the language that owns the
  format and one in every language that prints it - which is exactly the duplication the layer
  exists to avoid. It would also be wrong in the ordinary case: a value inside a `Json.String` still
  has to be escaped *when the JSON generator runs*, not when the source template runs, because the
  source model may be converted twice with different targets.

What that costs: a language that wants to emit JSON must map into `Json.Value` rather than print a
string, which is more work than writing `"` … `"` in a template. It is the same cost the C# base
language pays, for the same reason ([csharp.md](csharp.md)), and the plan for the machinery is
already written: `mapping.mps`, the build and link phases and the reverse map in
[model-generation.md](model-generation.md).

## Slices

- **I0 - the contract.** `ITextImporter`, `ImportResult`, `ImportDiagnostic` in
  `Projectional.Language/Import/`, plus tests with a hand-written importer that appends one
  node and fails on demand, proving: success commits one undo step, failure leaves the model
  byte-identical, and a diagnostic is reported at the right line and column.
- **I1 - JSON.** `Projectional.Json`: concepts, notation, importer, generator. Tests: a golden
  text corpus (each file imported and regenerated must equal the input byte for byte), the
  number-literal cases, duplicate members, and a truncated file that must import nothing.
- **I2 - XML.** `Projectional.Xml`: same shape, plus whitespace-only text, attributes in order,
  comments, a CDATA section landing in `Xml.Raw`, and the refused DTD case.
- **I3 - TOML.** `Projectional.Toml`: same shape, plus a document whose order is its structure
  (`[a]` then `[[b]]` then back to the root), the four string kinds, the four date/time kinds, a
  dotted key that stays text, and a duplicate key that imports nothing.
- **I4 - HOCON.** `Projectional.Hocon`: same shape, plus brace-less objects, the three separators,
  unquoted and triple-quoted strings, a `Concat` with a substitution inside it, two repeated keys
  that stay two, an `include` that is kept and **never followed** (asserted by making the target
  a file that does not exist and demanding no diagnostic and no read).
- **I5 - appending to a non-empty model.** Import into a model that already has a root; assert
  the existing root is untouched, the new one is last, and one `Ctrl+Z` removes only the
  import.
- **I6 - the raw-text tier.** A construct the language does not map survives as `Xml.Raw` /
  a JSON-preserving node and regenerates to the same text.
- **I7 - the shell.** File > Import in the workbench: pick a file, create a document from it,
  and land on a clean (not dirty) document, because nothing has been edited yet.
- **I8 - docs and samples.** A sample corpus of `.json`, `.xml`, `.toml` and `.hocon` files under
  `tests/Projectional.Core.Tests/Corpus/Text/`, with the same rule as the `.mps` corpus:
  **never regenerate a corpus file to make a test pass** - a byte that moves is either a
  language bug or a deliberate change with a note in the commit.

## Testing

| What | How |
| --- | --- |
| The text survives a round trip | golden corpus: import then generate, compare bytes |
| Nothing is written back | the importer has no write path; a model built by import is saved as `.mps` and reloaded |
| Failure is total | a file with a syntax error leaves `model.Roots.Count` and the version unchanged |
| Failure is atomic under a mid-way throw | an importer that throws after adding three nodes leaves nothing |
| One undo step | `UndoHistory.UndoDepth` grows by one; `Undo()` removes every imported root |
| Diagnostics point at the text | line/column of a known bad character, asserted exactly |
| Hostile XML is refused | a DOCTYPE with an external entity fails with `xml.external-entity-refused` and no file is read |
| A HOCON include is never followed | the include names a path that does not exist; the import succeeds, reports nothing, and reads nothing |
| TOML's order is the structure | `[a]`, then `[[b]]`, then a root pair again, regenerates in that order - a normalised document would move the pair |
| A conversion produces a model, not a string | mapping a document into `Json.Object` and printing it equals a hand-written JSON document of the same content, and the mapping refuses a value the target cannot hold |

## Open questions

1. **Should import be able to *replace* a document?** Recommend: no. Import appends; replacing
   is "close the document and open a new one", which is a shell decision.
2. **What happens to a dirty document?** The shell asks before replacing it, like any other
   destructive open. Not an import concern.
3. **Do we need a second tier for "valid but semantically wrong"** (a JSON number in a slot
   typed over a string)? Recommend: no, the language is the schema here; a concept that cannot
   hold the value means the mapping is wrong, and the parser tier already catches that.
4. **Should the reference languages be extensible** (a host adds `Json.Property` subtypes)?
   Concepts are open, so a host can already declare one; no extra seam needed.

One question this plan raises is asked in [open-questions.md](../open-questions.md) rather than
here, because it touches another band: **98** (may the TOML and HOCON readers be package
dependencies, and what must such a reader answer?).
