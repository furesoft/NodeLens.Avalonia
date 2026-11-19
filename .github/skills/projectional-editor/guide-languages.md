# Language packages as models, and text import

> Part of the `projectional-editor` skill. `SKILL.md` is the index and holds the invariants, the
> conventions and the build commands; this file is one layer of the rest, read on demand.

## How a language is declared as a model

`Projectional.Structures` is slice P1 of M2: the structure language, nine concepts whose nodes *are* a language
definition, plus `StructureLowering` into the `ConceptDeclaration`s every other layer understands.

- **A definition is a model like any other** - same editor, same writer, same undo - because that is what makes
  the two authoring paths of ADR 0014 equal. `StructureLanguage.ConceptDeclarations` is the C# twin of the
  definition language itself.
- **The language says what a concept *is*, never how it looks.** Presentation is the editor language (P2), in a
  separate model, for the same reason MPS splits structure documents from editor documents.
- **The cardinality is data, spelled as a range**: a `cardinality` names one of the four MPS offers in one cell -
  `0..1`, `0..n`, `1`, `1..n` - and `typeName` is a name an author types, so the *lowering* validates them. A model
  that refused them would have to know the concept registry. The range is stored as `ChildSlot.Cardinality` (the
  storage shape) plus `ChildSlot.Minimum` (the lower end), and the two checks read it: `STD010` for a single child
  whose minimum is one, `STD011` for a list whose minimum is at least one. A minimum above one is representable in
  the model but has no spelling yet - do not invent one until a language asks.
- **`StructureLowering` checks what a compiler would have checked** (every property names a known type, an
  enumeration has symbols and a unique key, a slot is one of three kinds) and deliberately **not the graph**:
  unknown super concepts, child slots naming nothing and duplicate keys are `ConceptRegistry.Create`'s answers.
  **Do not add a second graph validator here** - the two would drift, and `CreateRegistry` is the single answer to
  "is this a language".
- **The namespace is `Projectional.Structures`, not `Projectional.Structure`**: CA1716 is an error here and
  `Structure` is a VB keyword. The class names keep `Structure`.
- **`Projectional.Editors` is the other half (slice P2)**: the editor language, nineteen concepts whose nodes are a
  notation, and `EditorLowering` into real `Notation`s. It is a *separate* language from the structure one, because
  a definition that mixes the domain with the presentation cannot be reviewed. The **inline/block split is carried
  by slot declarations** here - `Editor.Line.cells` holds `Editor.InlineCell` - so `ed.Line(ed.Indent(...))`'s
  compile error becomes `ModelInvariantException` at the edit, and the lowering has no such check of its own.
  **The lowering goes through `NotationEditor`, not around it**, so a declared notation *is* a builder call; it
  checks only that a notation names a declared concept, and leaves undeclared slots and duplicate variants to
  `NotationRegistry`. The namespace is `Projectional.Editors` (plural) because `Projectional.Editor` would shadow
  every `Editor*` type of `Projectional.Editing`, and the notation of the notation lives in `…Editors.Rendering`
  because `…Notation` would shadow the `Notation` type.
- **Both definition languages are drawn in MPS' shape** (`StructureNotations`, `Editors.Rendering.EditorNotations`):
  a header, a block of options, then one section per kind of slot, with `[/ ... /]` / `[> ... <]` / `[ ... ]` /
  `[\| ... \|]` for the editor language's cell model and a glyph per cell - `-word-` (keyword), `%slot%` (property),
  `( slot )` (single child), `{ slot }` (list), `< slot >` (reference), `;` (gap). The editor language shows an
  empty list as `<< ... >>`; the structure language names the kind instead (see below). Neither replaces its
  placeholder with prose such as `<no notations>`. A line is `[` beside its cells beside `]`, which is what
  `HorizontalNotationCell` exists for.
- **A structure concept is a header and a brace block** (`StructureNotations`). The header says what the concept
  *is* and what it inherits - `abstract concept Rules.Rule extends Rules.Element, implements Framework.Named {` -
  and a list that is empty is **not drawn at all** (`concept Rules.Rule {`). The body is one block of options plus
  one section per kind of slot, closed by `}`. The comma between the two lists sits inside the `implements` run
  and is conditioned on `extends`, so it never dangles.
- **`WhenFilledNotationCell` (`ed.WhenFilled`) is the cell that hides an empty slot**, and hiding one is a pair:
  the empty slot has no caret stop, so the language owes an intention that creates the first child.
  `Projectional.Structures.Intentions` pays with `Inherit from…`/`Implement interface…`: `InsertChildAction` for
  an empty list, `InsertSiblingAction` for one that already has entries - Enter does not extend an inline list -
  and both land the caret on the new key. Do not add the cell without the entry: an unreachable slot is worse
  than a placeholder. Its declared twin is `Editor.WhileFilled` (ADR 0014: anything C# can say, a model must be
  able to say).
- **A declaration says its own kind and closes where it ends** - `<<property name : string ... >>`,
  `<<child steps : Rules.Step[1..n]>>`, `<<reference condition : Rules.Condition ... >>` - and every collection in
  that notation **stacks**, because a definition is read one declaration per line. Layout draws a collection inline
  unless the notation asks otherwise, so this is `stacked: true` on every collection here, not a renderer default; a
  section with four slots used to be drawn side by side. The marker is what makes a property's three option lines
  readable: without a visible end nobody can tell where one declaration stops.
- **An empty list names its kind in the structure language**: `<<property>>`, `<<child>>`, `<<reference>>`,
  `<<symbol>>`. Three empty sections that shared one glyph could not tell an author which slot Enter would fill -
  do not collapse them back into a single `<< ... >>`. The two header lists are the exception: `extends` and
  `implements` already say which list they are, so an empty one is hidden rather than given a glyph. The editor
  language keeps `<< ... >>` for its empty cell lists.
- **`Structure.Concept` holds `properties`, `children` and `references` as three `Many` slots, not one `slots`
  list.** The kind of a slot is a fact of the model, not something the renderer infers from the child's concept -
  which is why MPS splits the sections too. The consequence is a **fixed rank**: lowering concatenates properties,
  then children, then references, and that rank *is* the concept's declaration order, which decides the default
  fill and the completion ranking. A definition that creates a reference before a property still declares the
  property first; do not sort by creation order or by name.
- **`abstract concept Rule`, not `is abstract: true`** - MPS' spelling, and a `ConditionalNotationCell` is what
  makes it work: the word exists in the notation always and in the drawing only while the flag is true. A concrete
  concept draws *nothing* there, not an empty cell, so the flag has no cell to type into - which is why toggling it
  is an intention (`Projectional.Structures.Intentions`, `Make abstract`/`Make concrete`) and why the action behind
  it is `SetNodePropertyAction` (node + slot) rather than the cell-addressed `SetPropertyAction`.
- **One part of MPS' look is still out of reach and must not be faked**: `instance can be root` and `alias` exist
  in MPS but lower nowhere here, so ADR 0014's "nothing declared-only" keeps them out.

### The parity rule is executed by three tests

`LanguageParityTests` + `ParityHarness` compare `TestLanguage` (C#) with `DeclaredTestLanguage` (the same language
as a model, lowered by the shipping code): **registry dump** (ordered text - order decides default fill and
completion ranking, so never compare sets), **snapshot** (one document drawn through both notation registries) and
**findings** (a `CheckSet` over two documents built the same way). Findings are compared **by place in document
order, never by node id** - two documents built the same way have different identities, and a finding promises
*where*. The harness returns text rather than booleans so a failure reads as a diff.

**A parity gap is a defect, not a test problem.** Writing the twin immediately found two: `collapseSlot` and a
collection's separator were expressible in C# and not as data. Both are data now. When a declared language cannot
express something C# can, fix the language - ADR 0014 rule 2.

### The workbench loads the definition languages

`WorkbenchLanguage` (`Projectional.Workbench/Documents/`) is the app's **one** registry and **one** notation
registry: it composes the sample rules language with `Projectional.Structures` and `Projectional.Editors`, and
`DocumentFile.TryLoad` loads against it. A concept document therefore opens in the app like any other document - a
language definition is a model.

- **Include the transitive closure.** `RulesLanguage.ConceptDeclarations` is that language's *own* concepts; it
  borrows `Expressions.Expression`, so a host composing it must bring `ExpressionsLanguage.ConceptDeclarations`
  too. Missing it is a `ConceptDefinitionException` at startup, naming the concept.
- **`EveryConceptOfAllThreeHasANotation`** walks the composed registry and demands a notation per concrete concept,
  because a registered concept without one renders as `MissingNotationCell` - the one state a user cannot work
  around. It already caught `Editor.Collection` being added without its notation: **add the notation in the same
  commit**.
- The extra `ProjectReference`s are **language packages** (`Structures`, `Editors`), the same category as
  `Samples`, not Core-adjacent internals.

### A language project is a folder

`Projectional.Languages` holds `LanguageProject`: a directory with a `structure` folder (required) and optional
`editor`, `types`, `generator` and `constraints` folders becomes a `ConceptRegistry` + `NotationRegistry` +
`TypeRegistry` + generators + `ConstraintSet`.
**`Open(directory, out error)` answers a usable
language or a message, never an exception** - opening is a user action, and a user action reports. The notation is
lowered *against the concepts the structure documents just declared*, so a notation for an undeclared concept is
caught at the earliest moment both halves exist. **A language without an `editor` folder loads on purpose**: every
node draws as a missing-notation cell, which is the state a language being written is in.

**One document holds one definition**, and the folder is read in ordinal file-name order - that order is the
declaration order, which decides default fill and completion ranking. A structure document's root *is* a
`Structure.Concept` or a `Structure.Enumeration`; an editor document's root is one `Editor.Notation`; a types
document's root is one `TypeRule.Rule` or one `TypeRule.Named`; a generator document's root is one
`TextGen.Generator`; a constraints document's root is one `Constraint.Rule`. **There is no container concept**
(`Structure.Language`, `Editor.Notations`, `TypeRule.Rules`,
`TextGen.Generators` were deleted): a container with one child would be a second place to say which definitions
exist. Do not reintroduce one to "simplify" a fixture - a document with two roots is refused by `LanguageProject.Read`
and by the rules of MPS' file layout.

**`samples/declared-pipeline` is the M2 gate made concrete**: a DSL that exists only as data - a `structure` folder,
an `editor` folder, a `constraints` folder and a document - with no C# anywhere. `DeclaredSampleLanguageTests` copies the folder next to the test
assembly (a `<None Include="..\..\samples\...">` item) and loads exactly the files a user opens, so the sample cannot
drift from what the code accepts without a test failing. It is the place to look when asking "can a language really
be written without C#", and the place to extend when the declared path grows. The definition files there are the
*writer's* output - `EveryDefinitionDocumentSavesBackByteIdentically` re-saves each one and demands the same bytes -
so generate them with `ModelFile.Save` rather than by hand.

**`WorkbenchLanguage` is per shell, not static** - it grows with **File ▸ Open language…**, and `TryAdd` builds both
candidate registries first so a clash changes nothing (a half-added language gives documents a concept registry that
disagrees with the notation registry in force). Growing the set re-declares every concept already in it
(`Redeclare`), because `ConceptRegistry` is immutable; the parity harness's dump comparison covers that path. A
document of an opened language starts at the first **rootable** concept: concrete and `Applicability.IsAnywhere` - a
concept that names places can never be a root.

## How text import works

Import is the only place a parser exists, and it runs once: the host passes a string, the importer fills a model the
host created, and the text is thrown away - so ADR 0002 is intact. `docs/architecture/import.md` is the design of
record, and `Projectional.Json` is the first reference language behind the contract.

- **The contract is in `Projectional.Language/Import/`** (`ITextImporter`, `ImportResult`, `ImportDiagnostic`,
  `ImportSeverity`), beside `ICellEmphasis` and for the same reason: a language package must be able to declare an
  importer without referencing `Editing`. An `ImportResult` is either refused (no roots, one error) or imported (roots
  plus warnings), so a host writes one branch.
- **One transaction is the whole failure path.** `using var transaction = model.BeginTransaction();` ... `Complete()`
  and nothing else: a refusal or a throw disposes it, the model is byte-identical again, and a success is one undo step
  and one change event. Import needs no new Core seam - `NewNode`/`NewChild`/`AddRoot` are public and journal
  themselves, which is exactly what makes that true.
- **An import diagnostic is not a `Diagnostic`.** That one names a `NodeId`, and a syntax error has no node - that is
  the whole problem. The place is a 1-based line and column counted in *characters* of the text the host passed, plus a
  code that is never localized (`json.unexpected-token`, `json.duplicate-name`).
- **A reference language holds the file's spelling, not the decoded value.** `Json.String.text` is what sat between the
  quotes, escapes included; a number keeps its literal (`1.0`, `1e3` and `1` are three files); a duplicate member is
  kept and reported. That is what makes `Import` -> `Generate` byte-exact **without** an escaping cell in a notation or
  an escaping part in generation.
- **Whitespace is not a value**, so the round-trip corpus is the generator's own output. Never rewrite a corpus file to
  make a test pass; a changed byte is a behaviour change.
- **The tokenizer is the platform's, the mapping is ours.** `Utf8JsonReader` does the lexing, so escapes, number
  grammar and UTF-8 are not our problem. The tokenizer reports byte offsets (`TokenStartIndex`, a `JsonException`'s
  `LineNumber`/`BytePositionInLine`); `SourcePositions` translates them into the line and character column an editor
  shows, and the exception's message has its appended position cut off because the diagnostic carries the place as a
  field.
- **A bracket's inner space is a `WhenFilled` cell in the notation and a `WhenFilledPart` in the generator; the brackets
  themselves are unconditional.** So an empty object prints `{}` while it draws `{ }` - an empty slot must stay visible
  to the caret, and a drawing is not the file.
- **Each reference language ships its own intentions and checks** (explicit user requirement, not a nicety), and since
  [ADR 0021](../../../docs/adr/0021-one-project-per-language.md) they are **folders inside the language project**:
  `Projectional.Json/Checks/` (namespace `Projectional.Json.Checks`), `Projectional.Json/Intentions/`,
  `Projectional.Xml/Checks/`, `Projectional.Xml/Intentions/`. TOML, HOCON and YAML have the same two folders -
  **never one shared "syntax languages" package**, which would have to ask which language it is looking at.
- **XML's two intentions are mandatory, not a convenience.** Its notation hides both collections while they are empty -
  an empty XML slot cannot be drawn as a blank, because whitespace inside an element *is* content and a placeholder would
  make `<a></a>` and `<a> </a>` indistinguishable - so "add attribute" and "add child element" are the only way into
  them. Hiding a slot is a pair; this is the other half, and a reference language would otherwise be uneditable.
- **The checks are the format's own rules, each with its repair**: `JSON001` a name or string that is no longer legal
  JSON (escape it), `JSON002` a number cell whose text stopped being a number (restore the declared default), `JSON003`
  one member name used twice (rename to the first free suffix), `XML001` text or an attribute value that is not the
  canonical spelling (escape it), `XML002` a comment containing `--` (separate the dashes). Both answer a `CheckSet`, so
  a host composes them with `StandardChecks.All`.
- **`XmlEscaping` is public on purpose**: the checks package must spell a repaired value exactly as the importer did, and
  a second escaper in a behaviour package would be a second answer to "how is this written".
- **Planned, not built: a declared importer.** Today `ITextImporter` is C#-only, so a language written as a model can
  print its files and not read them. The plan (`docs/architecture/import.md` section *Import as a declaration*) is
  `Projectional.ImportRules` - an `import` folder beside `generator`/`constraints`/..., two layers (tokens, then rules),
  a closed vocabulary that mirrors the template's verbs, `Import.Raw` for tier 2, and a load-time `ImportLowering` into
  the same `ITextImporter`. The recommended default is the *inverse of the generator's templates* (literal -> anchor,
  property -> capture, child -> nest, children -> repeat, when-filled -> optional, conditional -> alternative), with a
  declared rule only where the inverse does not exist - resolved **per slot** from each candidate's first anchor, so the
  parser has no state but the model. Six token concepts (`Literal`, `Word`, `Number`, `Quoted`, `Fixed`, `Skipped`),
  ten rule words including `Bind`/`Matches` (XML's closing tag) and `Fail` (a rule's own message), and
  `Import.Importer.target`: **an importer inverts one named generator**, because a language may print two formats and
  their inverses are different readings - so a faithful language's importer is `default inverse` plus its exceptions.
  Open questions 100-108 hold the decisions.
- **A reference language may hold a note the format cannot express.** `Json.Comment` is a `Json.Member` beside
  `Json.Property`, drawn as `// why` and mapped to `TextTemplate.Empty`, so generation omits it - and the "an entry that
  emits nothing takes no separator" rule is what keeps a note between two members from producing a stray comma. It is
  never imported (a JSONC file stays refused, because the round trip would drop the notes), `JsonDuplicateMemberCheck`
  skips it (a note has no name to collide with), and `GenerationCoverage` counts it as covered - a decided silence is
  not a missing template. A note-only object prints `{  }`: the inner spaces are chosen by whether the slot holds a
  child, which is the price of not having a second answer to "is this object empty".

