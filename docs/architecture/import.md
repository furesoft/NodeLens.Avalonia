# Text import

A projectional editor has no parser, on purpose: the model is the only source of truth
([ADR 0002](../adr/0002-model-as-source-of-truth.md)). Import is the one place where text
enters a model, and it does so **once**, from a string the host supplies, after which the
string is thrown away. Nothing writes text back into the model, so the invariant is intact:
there is still no text buffer and no parser in the editing path.

Status: slices **I0 (the contract), I1 (JSON), I2 (XML), I3 (TOML), I4 (HOCON), I4b (YAML), I5 (appending to a non-empty
model), I7 (File ▸ Import) and I8 (the corpus)** are delivered, with the intentions and checks of the shipped languages in
the same project as them; the declared importer (I9-I11) is not. Generation is the opposite direction and is
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

Five languages ship as packages, for the same reason `Projectional.Expressions` exists: they
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

| Language | Package | What it adds beside the others |
| --- | --- | --- |
| JSON | `Projectional.Json` | the smallest complete format; objects, arrays, four value kinds |
| XML | `Projectional.Xml` | attributes, mixed content, whitespace as data, comments |
| TOML | `Projectional.Toml` | **tables and arrays of tables**, four kinds of string, dates and times as types, comments, a document whose order *is* its structure |
| HOCON | `Projectional.Hocon` | a JSON superset whose braces and commas are optional, unquoted keys and values, value concatenation, and constructs that are *evaluated* rather than read (`${...}`, `include`, `+=`) |
| YAML | `Projectional.Yaml` | a format whose **layout is its syntax**: block *and* flow collections as separate concepts, three scalar spellings plus block scalars, anchors, aliases, tags and comments |

Three of them parse with the platform library, not with a hand-written parser. A hand-written JSON or
XML parser is a liability with no upside here; the point of the exercise is the mapping, not
the grammar. .NET has no TOML, HOCON or YAML reader in the box, so those take a package dependency
(TOML, YAML) or a hand-written lexer (HOCON) - in the reference language's own package, never in the
toolkit (see open question 98).

**What a reference language owns.** Every spelling its format has: how a string is quoted and escaped, how a key is
written, how a comment is written, how a date and a number are spelled. That is not a property of a *template* and not
a property of the framework's template vocabulary - it is the business of the language that mirrors the format, in its
own generator, exactly as the notation of a concept belongs to the language that declares it. A second language that
targets the same file format then reuses that language instead of re-deriving its rules.

### `Projectional.Json`

The smallest complete format: an object, an array and four value kinds, holding the spelling the file
wrote rather than the parsed value.

[import/json.md](import/json.md)

### `Projectional.Xml`

Attributes, mixed content, whitespace as data, prefixes kept as text, and CDATA and processing
instructions kept as raw text.

[import/xml.md](import/xml.md)

### `Projectional.Toml`

Tables and arrays of tables, four kinds of string, four kinds of date and time, and a document
whose order *is* its structure.

[import/toml.md](import/toml.md)

### `Projectional.Hocon`

A JSON superset whose braces and commas are optional, with unquoted keys, value concatenation and
constructs that are evaluated rather than read.

[import/hocon.md](import/hocon.md)

### `Projectional.Yaml`

The one format whose layout *is* its syntax: block and flow collections as separate concepts, a
scalar that keeps the whole spelling, and raw text for what the language does not model.

[import/yaml.md](import/yaml.md)

All five packages reference `Projectional.Language` only, exactly like `Expressions`. JSON and XML use the platform's
reader; **TOML takes `Tomlyn` and YAML takes `YamlDotNet`**, each as a package dependency of its own language and never
of the toolkit (open question 98). **HOCON's lexer is hand-written**, and that is the same rule rather than an exception:
the package may be taken only if it answers with a source position, and no HOCON reader for .NET does - a reader that
answers a dictionary cannot make a tier-1 diagnostic point at the character that broke the file. YAML is the case that
shows why: its scanner is worth having because it resolves indentation, which no hand-written reader in this repository
would get right first time.

## Converting a model into one of these languages

The reason the five formats are **languages** rather than a printing mode is this: converting a
model into JSON, XML, TOML, HOCON or YAML produces a **model of that language**, and that language's own text
generator prints it. Nothing else knows how to spell the format, so the spelling exists once.

That answers the question a conversion always raises - who escapes the value? - with "the language
that owns the format, in the same place it always does". A user's language maps its concepts into
`Json.Object`, `Toml.Table`, `Hocon.Object` or `Yaml.Mapping` nodes, exactly as
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
- **The five formats stay symmetric.** Import and generation are two directions of *one* language,
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
`Projectional.Json/Checks/`, `Projectional.Xml/Intentions/`, `Projectional.Xml/Checks/`, `Projectional.Toml/Checks/`, `Projectional.Hocon/Checks/` and
`Projectional.Yaml/Checks/` - folders in the
language's own project ([ADR 0021](../adr/0021-one-project-per-language.md)), with the namespaces they had when
they were separate. The framework's own catalogue (`Projectional.Checks`) stays a package of its own, because it is
shared by every language and belongs to none. All five reference languages have those two folders, and there is never a
shared "reference languages" package - that would have to ask which language it is looking at.

The split against the standard intentions is the usual one: the framework's catalogue derives what the *place* implies
(rename, delete, move, wrap, replace, fill from the declarations), and a language adds what a declaration cannot say.
For a notation of a real file format that means the operations of that format:

| Language | Intentions | Checks, each with its repair |
| --- | --- | --- |
| JSON | add a member to an object, add an item to an array | a string or name no longer spelled as JSON requires (escape it), a number slot that no longer holds a number (restore the declared default), one member name used twice (rename the later one) |
| XML | add an attribute, add a child element - the two slots the notation cannot draw empty | text or an attribute value no longer well-formed (escape it), a comment containing `--` (separate the dashes) |
| TOML | add a pair, a table or an array of tables to the list the caret is in | a duplicate key (rename to the first free suffix), a number cell that no longer holds a number (restore the declared default), a date or time that no longer parses (restore the default and its kind), a string no longer spelled as its kind allows (escape it as a basic string) |
| HOCON | add a property, add an item to an array - both into the nearest list the caret is inside | an unquoted value that no longer holds a legal run (quote it), a number cell that no longer holds a number (restore the declared default) |
| YAML | add a member to a block mapping, add an item to a block sequence - both into the nearest *block* list, never a flow one | a block collection on its key's line (write the value on the next line), a scalar whose spelling stopped being one (quote it) |

All five are implemented, so every row is a report rather than a requirement. Each of
the five is a language, so each ships the packages a language has. A format whose rules were only enforced by a shared
catalogue would be a second opinion about what that format allows - and the rule itself would have to ask which
language it is looking at, which is exactly what this design refuses to build.

What a language keeps is what a *declaration* cannot express. `Projectional.Expressions` has no intention package entry
for wrapping an expression in parentheses, because `Projectional.Intentions` derives that row from the declarations; what
it keeps is the `Unary` that is born with `!`. The syntax languages above follow the same line, at the scale of their
grammar.

## Import as a declaration (planned)

A language written as a model should read its own files and not only print them. The plan is
`Projectional.ImportRules`: an `import` folder beside `generator` and `constraints`, two layers - tokens
and rules - and a closed vocabulary that mirrors the template's verbs.

**I9-a, the rule layer, is delivered**: `Projectional.ImportRules` declares an importer as a document in an `import` folder - `Import.Literal`, `Import.Capture`, `Import.Nest` and `Import.Repeat` - and `LanguageProject` offers it beside the importers its generators derive. The token layer is delivered too: `Import.Word`, `Import.Number`, `Import.Quoted` and `Import.Skipped`, so a capture ends where its class says and a language can step over whitespace and comments. `Import.Fixed`, `Import.Raw`, `Import.Bind`/`Matches` and `Import.Fail` complete the vocabulary, each with the case this document named for it: a section that resolves no escapes, a run kept verbatim, a closing tag that has to equal the opening one, and a rule's own message instead of `expected`. `Import.Line` reads the rest of the line into a property and steps over the line break, which is the only way to state "this value ends here" without spelling one of the two spellings a file may use - a newline literal leaves the carriage return of a CRLF file in the value. **I9-b, the inverse of the template, is delivered** and is the base the rest builds on:
`TemplateImporter` (`Projectional.Language/Import/`) reads back what a generator's own templates write, so
a declared language is no longer a one-way street for the case its generator can already print.
`LanguageProject.Importers` offers one importer per generator that inverts, and `TryCreateImporter` says why
one does not. **There is no backtracking**: every choice is decided by the literal at the cursor, so an
ambiguity - two branches or two slot concepts whose text begins alike, a template that ends in a property -
is refused when the importer is built rather than discovered in a file. `ParentPart` and `ReferencePart` are
refused because they write what the node does not own.

[import/declared-importer.md](import/declared-importer.md)

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
- **I3 - TOML.** *Delivered.* `Projectional.Toml`: seventeen concepts, a document whose order is its
  structure (`[a]` then its pairs, then `[[b]]`), the four string kinds and the four date/time kinds as
  slots beside the text they spell, a dotted key that stays text, a comment as an entry, a duplicate key
  that imports nothing, and the format's own intentions and four checks. The lexer is `Tomlyn`, taken as a
  package dependency of the language and never of the toolkit. Tests: `TomlImporterTests` (the order, the
  four kinds of each leaf, dotted and quoted keys, a comment kept and one moved out of an array with its
  warning, a duplicate key refused with nothing imported, a legal `[[a]]` pair, the place of a syntax
  error, one undo step), `TomlTextGeneratorsTests`, `TomlNotationTests`, `TomlChecksTests`,
  `TomlIntentionsTests` and `TomlCorpusTests` (the byte-exact round trip).
- **I4 - HOCON.** *Delivered.* `Projectional.Hocon`: fourteen concepts, a document that is the brace-less
  object every file is, the four separators as one property with four conditional cells, unquoted,
  quoted and triple-quoted strings, a `Concat` with a substitution inside it (and unwrapped again when
  only one part arrived), two repeated keys that stay two, both comment markers, and an `include` that
  is kept and **never followed**. The lexer is hand-written, because no HOCON reader for .NET answers
  with a source position (open question 98). Tests: `HoconImporterTests` (the repeated key, the four
  separators, the three string kinds, the number literals, the substitution's two forms, the
  concatenation and the unwrapped single value, an include that is never followed, both markers, a stray
  brace's place, one undo step), `HoconTextGeneratorsTests`, `HoconNotationTests`, `HoconChecksTests`,
  `HoconIntentionsTests` and `HoconCorpusTests` (the byte-exact round trip).
- **I4b - YAML.** *Delivered.* `Projectional.Yaml`: sixteen concepts, one document root holding the file's notes, markers
  and node in order, block *and* flow collections as separate concepts with the flow slots typed over `Yaml.FlowValue`
  (so a block collection inside a flow collection cannot be built), a scalar whose slot is the file's whole spelling, a
  pair that records whether its value stands on its key's line, an anchored or tagged collection kept as `Yaml.Raw`, a
  complex key kept as one raw line, and a note that is held until the list it belongs to starts. The reader is
  `YamlDotNet`'s scanner with `skipComments: false`, taken as a package dependency of the language. Tests:
  `YamlImporterTests` (the one root, both collection kinds, the scalar spellings including block scalars, anchors and tags
  inside a spelling, an alias, `inline` versus `nested`, a held note, a hoisted note and a note beside a value with their
  warnings, a marker and a directive, an anchored collection and a complex key as raw text with line and column of a
  refusal, CRLF normalization, one undo step), `YamlTextGeneratorsTests`, `YamlNotationTests`, `YamlChecksTests`,
  `YamlIntentionsTests` and `YamlCorpusTests` (the byte-exact round trip).- **I5 - appending to a non-empty model.** *Delivered.* `JsonImporterTests.AnImportAppendsToANonEmptyModel`:
  the existing root is untouched and stays first, the imported one is last, and one `Ctrl+Z` removes only
  the import.
- **I6 - the raw-text tier.** A construct the language does not map survives as `Xml.Raw` /
  a JSON-preserving node and regenerates to the same text.
- **I7 - the shell.** *Delivered.* **File > Import...** in the workbench: the picker offers every extension of the five formats (`.yaml` and `.yml`
  alike), the
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
  until the test went green. Covered: JSON (nested objects, an array of values, a number in four spellings),
  XML (attributes in order, mixed content, CDATA, a processing instruction, a namespace prefix), TOML (root pairs,
  tables, arrays of tables, dotted keys, the value kinds), HOCON (properties, objects, notes) and YAML (a document with a
  marker and a note, nested collections and flow collections, the scalar spellings, notes at two levels).
- **I9 - the declared importer, as a language.** `Projectional.ImportRules`: the two-layer vocabulary of
  [The shape of the declaration](import/declared-importer.md#the-shape-of-the-declaration) - six token concepts (`Literal`, `Word`, `Number`,
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
