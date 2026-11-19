# Text import

A projectional editor has no parser, on purpose: the model is the only source of truth
([ADR 0002](../adr/0002-model-as-source-of-truth.md)). Import is the one place where text
enters a model, and it does so **once**, from a string the host supplies, after which the
string is thrown away. Nothing writes text back into the model, so the invariant is intact:
there is still no text buffer and no parser in the editing path.

Status: slices **I0 (the contract), I1 (JSON), I2 (XML), I5 (appending to a non-empty model) and I7 (File ▸
Import)** are delivered, with the intentions and checks of the two shipped languages in the same project as them; TOML and HOCON,
the raw-text tier and the sample corpus are not. Generation is the opposite direction and is
planned in [semantics.md](semantics.md#generation).

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
    ImportSeverity.cs       // refused, or kept as raw text
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
  [completion.md](completion.md#values)) offers exactly those two symbols in the cell, and the
  notation can render them as keywords.
- **A comment is a model-only note.** JSON has no syntax for one, so `Json.Comment` is an entry of an
  object that the generator deliberately leaves out - the empty template, not a missing one, because
  a silence somebody decided is not a hole (see
  [semantics.md](semantics.md#what-is-implemented) and `TextGen.Ignore`). It is kept in the model
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

### `Projectional.Xml`

| Concept | Slots | Notes |
| --- | --- | --- |
| `Xml.Node` | - | abstract: the children of an element |
| `Xml.Element` | `name`: string, `attributes`: many `Xml.Attribute`, `children`: many `Xml.Node` | |
| `Xml.Attribute` | `name`: string, `value`: string | order preserved |
| `Xml.Text` | `text`: string | the **canonical escaped spelling**, whitespace-only runs included |
| `Xml.Comment` | `text`: string | |
| `Xml.Raw` | `text`: string | tier 2: processing instructions, CDATA, DOCTYPE |

- **Namespaces are text.** `p:name` is the name; there is no namespace resolution, no prefix
  table, no `xmlns` handling beyond "it is an attribute like any other". Resolving them is a
  schema feature, not an editor feature.
- **Escaping happens on the way in, not on the way out.** The parser hands back a decoded value,
  and the importer writes the canonical spelling into the model (`&amp;` `&lt;` `&gt;` `&quot;`, and
  `&#x9;`/`&#xA;`/`&#xD;` in an attribute, where a literal one would be normalised to a space). So a
  template emits a slot verbatim, exactly as the JSON language emits a string, and the framework's
  template vocabulary needs no XML-specific part. The price is the same one JSON pays: the editor shows
  the spelling, so a `&` is typed as `&amp;`. A file that wrote `&#38;` comes back as `&amp;` - one
  canonical spelling for one character, which is what a byte-exact corpus can compare against.
- **An element with no content is written `<a></a>`.** A model holds one shape for it, so it cannot
  also remember which of the two spellings the file used, and a reference language must not have two
  spellings for one tree. A file written `<a/>` comes back in the long form, which every XML consumer
  reads as the same document.
- **A document is its top-level nodes.** XML allows comments and processing instructions outside the
  root element and they are content, so the importer answers several roots in document order rather
  than inventing a container. Only the XML declaration is not modelled: it describes the bytes a host
  read, not the tree, and the host that writes the generated text states it again.
- **The notation hides an empty collection.** In JSON an empty slot can be drawn as a blank; in XML it
  cannot, because whitespace inside an element *is* content - a placeholder would make an empty element
  indistinguishable from one holding a space, and that distinction is the language's whole job. The
  price is that the first attribute and the first child have no caret stop, which is exactly the pair
  the notation owes an intention for (see below).

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

### Every reference language ships its own intentions and checks

A reference language is a language like any other, so it brings behaviour of its own and not only a notation:
the intentions and checks of the two shipped formats live in `Projectional.Json/Intentions/`,
`Projectional.Json/Checks/`, `Projectional.Xml/Intentions/` and `Projectional.Xml/Checks/` - folders in the
language's own project ([ADR 0021](../adr/0021-one-project-per-language.md)), with the namespaces they had when
they were separate. The framework's own catalogue (`Projectional.Checks`) stays a package of its own, because it is
shared by every language and belongs to none. TOML and HOCON get the same two folders when they land, and never a
shared "reference languages" package - that would have to ask which language it is looking at.

The split against the standard intentions is the usual one: the framework's catalogue derives what the *place* implies
(rename, delete, move, wrap, replace, fill from the declarations), and a language adds what a declaration cannot say.
For a notation of a real file format that means the operations of that format:

| Language | Intentions | Checks, each with its repair |
| --- | --- | --- |
| JSON | add a member to an object, add an item to an array | a string or name no longer spelled as JSON requires (escape it), a number slot that no longer holds a number (restore the declared default), one member name used twice (rename the later one) |
| XML | add an attribute, add a child element - the two slots the notation cannot draw empty | text or an attribute value no longer well-formed (escape it), a comment containing `--` (separate the dashes) |
| TOML | a table or an array of tables where one is needed, a key insertion | a duplicate key (the format rejects one), a date/time whose text no longer parses |
| HOCON | add a property, add an element to an array | an unquoted value that is not a legal token |

TOML and HOCON are not implemented yet, so their rows are the requirement rather than a report: each of the four is a
language, so each ships the packages a language has. A format whose rules were only enforced by a shared catalogue would
be a second opinion about what that format allows - and the rule itself would have to ask which language it is looking
at, which is exactly what this design refuses to build.

What a language keeps is what a *declaration* cannot express. `Projectional.Expressions` has no intention package entry
for wrapping an expression in parentheses, because `Projectional.Intentions` derives that row from the declarations; what
it keeps is the `Unary` that is born with `!`. The syntax languages above follow the same line, at the scale of their
grammar.

## Import as a declaration (planned)

Every other aspect of a language can be *declared* - its concepts, its notation, its types, its generator, its
constraints, its scopes, its intentions - and one cannot: **how to read its own files**. `ITextImporter` is a C#
interface a package implements, so a language written as a model (a `LanguageProject` folder) is a one-way street: it
prints its documents and cannot read them back. That asymmetry is what this section plans away.

### Why it has to be a language and not a hook

The same argument every declared aspect makes ([ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md)): a
declared importer must produce the same model as its hand-written twin on the corpus, compared as text through the
generator, because "both authoring paths are permanent" is only true if the second one can do the whole job. A hook -
"a language may point at a .NET type that parses" - would leave the declared path weaker at exactly the seam that makes
a language usable, which is the line the type rules, the generator and the constraints all refused to cross.

**Where it lives:** `Projectional.ImportRules`, beside `Projectional.TypeRules`, `Projectional.TextGen`,
`Projectional.ConstraintRules`, `Projectional.ScopeRules` and `Projectional.IntentionRules` - a new aspect is a new
language plus its lowering. A language folder gains an `import` folder next to `structure`, `editor`, `types`,
`generator`, `constraints`, `scopes` and `intentions`, one document per importer (one root per document, as everywhere
else), and `LanguageProject.Importers` hands the lowered `ITextImporter`s to a host. The C# path keeps `ITextImporter`
exactly as it is: the declared form lowers *into* it, so nothing above import learns a second contract.

### The rules are the declaration

Two layers, and the split is the design rather than an implementation detail:

| Layer | Answers | Vocabulary (closed) |
| --- | --- | --- |
| Tokens | what counts as a token, and where it started | `Import.Literal` (an anchor), `Import.Word` / `Import.Number` / `Import.Quoted` / `Import.Fixed` (the lexical classes a config format needs, one concept each), `Import.Skipped` (whitespace, comments: text that produces no node) |
| Rules | which concept a run of tokens becomes | `Import.Node` (concept + parts), `Import.Sequence`, `Import.Alternative`, `Import.Repeat` (with separator and minimum), `Import.Capture` (a run into a property, spelled as stored), `Import.Nest` (a child slot taking another rule), `Import.Bind` / `Import.Matches` (a run remembered, then required to repeat), `Import.Raw` (a run the model does not map, kept verbatim), `Import.Fail` (the message a rule gives instead of "expected") |

- **No delegate, no expression, no "compute".** A rule either matches, captures, nests, repeats or chooses - the same
  small set of verbs a template has, for the same reason: a rule that could calculate is a programming language hiding
  in a model, and it would need an interpreter this framework does not have.
- **Every token carries its offset and length**, because `ImportDiagnostic` promises a line and a column and a parser
  that only answers a tree cannot keep that promise - the same requirement that decides which third-party TOML or
  HOCON reader may be used ([open question 98](../open-questions.md#text-import)).
- **Ambiguity is a load error, not a runtime surprise.** `Alternative` branches must start with distinct anchors where
  the author used anchors at all, and a repeat whose separator cannot be told from its item is refused - the earliest
  moment both halves exist, which is the rule `NotationRegistry` and `TextGenLowering` already follow.
- **Tier 2 has a place in the vocabulary.** `Import.Raw` is what lets a declared importer be honest about a construct
  the model does not model: the run survives as a raw node and regenerates to the same text, exactly as `Xml.Raw` does
  today.
- **Four words came from the proof cases, not from imagination.** Each is the smallest verb that makes a format the
  framework already ships declarable, and the case that demands it is written down here so the vocabulary cannot grow
  into a grammar by accident:

| Word | Is | Because |
| --- | --- | --- |
| `Import.Bind` + `Import.Matches` | a run remembered under a name, then required to be the same run again | XML's `<a>...</a>`: the closing tag must equal the opening one, and nothing else in the vocabulary compares two runs |
| `Import.Fixed` | a run between two marks in which nothing is escaped | CDATA. `Import.Quoted` resolves the escapes it declares, so a section that must not is a different class rather than a flag |
| `Import.Fail` | the message a rule gives when it is the one that should have matched | "a trailing comma is not allowed" is what the file's author needs; "expected `}`" is what the parser happened to think. It carries a code and a message and never branches |
| `Import.Importer.target` | the generator this importer reads back | the inverse of a template is the inverse of *one* template set, and a language has as many generators as it declares ([One importer per target](#one-importer-per-target)) |

### The shape of the declaration

The vocabulary above says what an importer *means*; this is what its author writes. Projectional means there is no text
syntax to type, so every line below is a drawing the notation layer can already build from `Line`, `Sequence`, `Indent`,
`Collection`, `Conditional` and `WhenFilled`. **No new cell kind is introduced** - a shape that needed one would make
the declared path need C# after all, which is the line
[ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md) draws for every aspect. Whether the drawings themselves
are the C# builder or an `editor` document is the usual order of work: the builder first, the declared notation once a
second language wants to describe one.

One document holds one importer and the importer *is* the document root, because there is no container concept anywhere
else either: a container with one child is a second place to say which definitions exist. Inside it there is one rule
per concept, the way a generator holds one mapping per concept - the concept key identifies the rule, which is what
gives the inverse an owner.

```text
importer Ini                                        // Import.Importer
  target   Ini                                      // the generator whose templates this importer inverts
  suffixes .ini .cfg                                // a fallback only: the file's content decides first
  default  inverse                                  // Import.Inverse - "read what the generator writes"

  token Word    plus . -                            // Import.Word: letters, digits, underscore, then these
  token Number  no leading zero, fraction, exponent // Import.Number: which spellings are legal
  token Text    quoted  open "  close "  escape \   // Import.Quoted: resolves the escapes it declares
  token Block   fixed   open <![CDATA[ close ]]>    // Import.Fixed: a run in which nothing is escaped
  skip  Comment from ; to end of line               // Import.Skipped: consumed between parts, no node

  rule  Ini.Pair                                    // Import.Node: the concept, then its body
    capture key   from Word
    literal "="
    capture value from Text
```

Five things about that shape are decisions rather than formatting:

- **One concept per token class, so no concept carries a parameter it does not use.** `open`/`close` on a `Word` would
  be a state the lowering had to reject; four concepts make it unrepresentable, the same reason `Structure.Concept`
  holds `properties`, `children` and `references` as three slots instead of one.
- **`skip` is not a rule part.** It is consumed greedily between any two parts, because a file's layout is not a
  construct - and a format whose whitespace *is* content (`Xml.Element`'s text today) reads it in a rule instead
  ([question 107](../open-questions.md#text-import)).
- **A literal may stand inline** (`literal "="`) because it is an anchor, while a named token exists where a class is
  reused. That is what keeps a short rule short without adding a second way to write a keyword.
- **Escaping lives in the token** ([question 102](../open-questions.md#text-import)): a capture writes what the token
  resolved, so the template vocabulary stays free of an escaping part and cannot disagree with the importer.
- **Consecutive parts are a sequence.** `Import.Sequence` exists only to group, for a branch of an alternative.

The parts, and the one thing each says:

| Part | Drawn as | Means |
| --- | --- | --- |
| Literal | `literal "="` | this anchor must be here |
| Capture | `capture key from Word` | the run becomes the property `key`, spelled as the token resolved it |
| Nest | `nest value from Ini.Value` | the child in slot `value` is read by that concept's rule, and the slot decides which concepts may stand there |
| Repeat | `repeat pairs from Ini.Pair separated by "," minimum 0` | a list of that rule; the separator is a literal or a token, and `minimum` is what the file must hold |
| Alternative | `alternative { literal "{"; ... \| literal "["; ... }` | one branch, chosen by its anchor |
| Bind | `bind open from Word` | remember this run under a name, fill no slot |
| Matches | `matches open` | the next run must be the same run again (XML's closing tag) |
| Raw | `raw until "</"` | tier 2: keep the run verbatim, generate it back |
| Fail | `otherwise fail "INI0140" "a trailing comma is not allowed"` | this rule's own message when it is the one that should have matched |

**Every refusal is a load error**, and `ImportLowering` shares the discipline `ConceptRegistry.Create` and
`TextGenLowering` already follow: a concept the language does not have, a slot named as the wrong kind, a property
filled twice, a `target` nobody declares, two `Alternative` branches that start alike, a repeat whose separator cannot
be told from its item, a `matches` whose `bind` is not earlier in the same rule, a `skip` that can match nothing (an
endless loop written as a model), and a `raw` whose terminator no token can produce. None of them is a surprise at
import time, because none of them is knowable only at import time.

### The default is the inverse of the template

This is the part that makes the feature tractable rather than a second grammar language, and it is the recommendation
recorded as an open question rather than assumed:

- **A language already says how it is written twice** - the notation says how it is drawn, the generator says how it is
  printed - and a separate grammar would be a *third* statement of the same syntax, free to drift from both.
- **The generator's parts invert one for one**: `TextPart` → an anchor, `PropertyPart` → a capture, `ChildPart` → a
  nest, `ChildrenPart` → a repeat with the separator template as the separator, `WhenFilledPart` → an optional branch,
  `ConditionalPart` → an alternative chosen by a property, `LoopPart` → a repeat of the body.
- **So the first reading of a document is "the same text the generator would write"**, derived from its templates, with
  a declared rule only where the file spells something the generator cannot produce: a comment, `<a/>` next to
  `<a></a>`, a number in a spelling the generator would not choose.
- **The places where the inverse does not exist are exactly the declared exceptions**, and naming them is part of the
  design rather than a gap: escaping (the reference languages settle spelling on the way *in*), insignificant
  whitespace (a file's layout is not a construct), and precedence (no part asks how an operand binds, which is why the
  C# generator parenthesises). A declared importer says what it does about each of them, or refuses the file.
- **"The generator" here always means the one the importer names**, because a language may declare several and their
  inverses are different readings of the same model ([One importer per target](#one-importer-per-target)).
- **Resolution is per slot, not per document.** The slot a rule fills declares which concepts may stand there, and each
  candidate's template begins with an anchor, so the dispatch table is built once at load and the parser has no state
  but the model and the slot it is filling. Two candidates with one anchor in one slot is a load error, the same check
  the `Alternative` branches get - which is why the inverse can be a table rather than a search with backtracking.

### One importer per target

A language has as many generators as it declares - `RulesTextGenerators` ships a JSON one and a C# one - and the inverse
of one template set is not the inverse of another, so no single document can read a language back into two spellings at
once. **The importer names the generator it inverts**, and a language that prints two formats ships two `import`
documents.

- **`target` is a name, not a concept**, the same kind of name `TextGen.Generator` has, resolved against the language's
  generator set at load. A name nobody declared is a load error rather than an importer that reads nothing.
- **The shell asks in the other order**, which is what keeps this from becoming a compatibility table: detection is the
  file's *content* first and the importer's `suffixes` only as a fallback
  (`ReferenceLanguages.Detect` already works that way), so an importer that declares no suffix is reachable exactly by
  what it can read.
- **Two importers of one language may accept different files**, and that is the point rather than a defect: each answers
  for one printed form. A test that demanded identical acceptance from both would be asking the framework to have one
  reading of two spellings.

### What must not happen

- **No second source of truth.** An importer still runs once and throws the string away; the model is what the editor,
  the checks and the generator see ([ADR 0002](../adr/0002-model-as-source-of-truth.md)).
- **No parser in the editing path.** No re-parse per keystroke, no error recovery into a live document: tier 1 refuses
  the whole text and tier 2 keeps a run as raw, which are the two outcomes the contract already has.
- **No round trip as a promise for free.** A declared importer that reads a construct differently from the way the
  generator writes it breaks `generate(import(text))`, and the corpus test is where that is caught - not a note in a
  manual.
- **No pattern language.** No regular expression, no character ranges, no lookahead beyond `Import.Matches`, no
  backtracking across alternatives, no left recursion, no semantic action: token classes are parameterised data and the
  rules are the verbs above. A format the vocabulary cannot describe is a gap in the vocabulary
  ([question 101](../open-questions.md#text-import)), not a reason to embed an engine.

## Slices

- **I0 - the contract.** *Delivered.* `ITextImporter`, `ImportResult`, `ImportDiagnostic`, `ImportSeverity`
  in `Projectional.Language/Import/`, plus `TextImportTests` with a `ProbeImporter` - a language that
  exists only to prove the contract - pinning: success commits one undo step and one change event, a
  refusal and a mid-way throw both leave the model byte-identical, and the result says which of the
  two outcomes it is.
- **I1 - JSON.** *Delivered.* `Projectional.Json`: eight concepts, notation, `JsonImporter` and the
  generator. Tests: `JsonCorpusTests` (each corpus file imported and regenerated equals the input byte for
  byte), `JsonImporterTests` (the number literals, the kept escapes, duplicate members by value, the exact
  line and column of a bad character, a comment and a second top-level value refused, a truncated file that
  imports nothing), `JsonTextGeneratorsTests` and `JsonNotationTests` (the spelling of the file, and the one
  place where the drawing and the file differ).
- **I2 - XML.** *Delivered.* `Projectional.Xml`: six concepts, a notation that draws the markup of the file,
  `XmlImporter` (the platform's reader, a prohibited DTD, preserved whitespace, prefixes kept as text) and a generator
  that escapes nothing because the importer already did. Tests: `XmlImporterTests`, `XmlNotationTests`,
  `XmlTextGeneratorsTests` and `XmlCorpusTests` - whitespace-only nodes, attribute order, CDATA and processing
  instructions as `Xml.Raw`, prefixes and namespace declarations, a refused DOCTYPE with no file read, an error's line,
  and the byte-exact round trip.
- **I3 - TOML.** `Projectional.Toml`: same shape, plus a document whose order is its structure
  (`[a]` then `[[b]]` then back to the root), the four string kinds, the four date/time kinds, a
  dotted key that stays text, and a duplicate key that imports nothing.
- **I4 - HOCON.** `Projectional.Hocon`: same shape, plus brace-less objects, the three separators,
  unquoted and triple-quoted strings, a `Concat` with a substitution inside it, two repeated keys
  that stay two, an `include` that is kept and **never followed** (asserted by making the target
  a file that does not exist and demanding no diagnostic and no read).
- **I5 - appending to a non-empty model.** *Delivered.* `JsonImporterTests.AnImportAppendsToANonEmptyModel`:
  the existing root is untouched and stays first, the imported one is last, and one `Ctrl+Z` removes only
  the import.
- **I6 - the raw-text tier.** A construct the language does not map survives as `Xml.Raw` /
  a JSON-preserving node and regenerates to the same text.
- **I7 - the shell.** *Delivered.* **File > Import...** in the workbench: the picker offers `.json` and `.xml`, the
  file's *content* decides which language claims it (the extension only when the text starts like neither, since the
  two shapes cannot both match), the text is imported into a fresh model named after the file, and the tab opens with
  that language's notation, generators, intentions and checks. A refused file opens no tab at all and reports the
  importer's line and column. Nothing is linked to the file it came from: the document starts clean because nothing was
  edited, and a save writes a `.mps` document or the generated text. `ReferenceLanguages`/`ReferenceImport` hold the
  decision and are tested headlessly; `MainWindow` only wires the dialog. One limit is inherited rather than
  introduced: a model with several roots still shows its first one (one session per model), so an XML file whose first
  top-level node is a comment opens on that comment - the whole document is in the model, and saving and generating use
  all of it.
- **I8 - the corpus.** The reference languages' corpus grows with the formats:
  `tests/Projectional.Core.Tests/Corpus/Text/` holds one file per format shape, and every file is what the generator
  prints - a byte that moves is a language bug or a deliberate change with a note in the commit, never a number edited
  until the test went green. Covered so far: JSON (nested objects, an array of values, a number in four spellings) and
  XML (attributes in order, mixed content, CDATA, a processing instruction, a namespace prefix); TOML and HOCON join it
  with their languages.
- **I9 - the declared importer, as a language.** `Projectional.ImportRules`: the two-layer vocabulary of
  [The shape of the declaration](#the-shape-of-the-declaration) - six token concepts (`Literal`, `Word`, `Number`,
  `Quoted`, `Fixed`, `Skipped`) and ten rule words (`Node`, `Sequence`, `Alternative`, `Repeat`, `Capture`, `Nest`,
  `Bind`, `Matches`, `Raw`, `Fail`) - with their notation, and `ImportLowering` into `ITextImporter` with every load-time
  refusal (a concept the language has not, a slot named as the wrong kind, a property filled twice, an undeclared
  `target`, alternative branches that start alike, a separator that cannot be told from its item, a `matches` before its
  `bind`, a `skip` that matches nothing, a `raw` whose terminator no token produces).
- **I10 - the inverse of the template.** The lowering that reads a document the way the generator writes it: parts
  invert one for one, the dispatch table is built per slot at load, and the escape hatch is a declared rule where the
  inverse does not exist. Proven by the parity rule per target - a declared importer whose twin is C# produces the same
  text through the named generator, over the corpus, and an importer with nothing but `default inverse` reads back what
  that generator prints.
- **I11 - the first declared language that needs it.** `samples/declared-pipeline` gets an `import` folder, so the
  M2-gate language becomes readable as well as printable - the moment a language really exists without C#.

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

Two of this plan's questions are asked in [open-questions.md](../open-questions.md) rather than
here, because they touch another band: **98** (may the TOML and HOCON readers be package
dependencies, and what must such a reader answer?) and **104-108** (the shape of the declaration
itself: character classes, compared runs, `Import.Fail`, whitespace as content, and what `target`
means when a language prints twice).
