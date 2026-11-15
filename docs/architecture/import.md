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

Two languages ship as packages, for the same reason `Projectional.Expressions` exists: they
are the executable proof that the layer composes, and they give the editor something to open
that was not authored in it.

Both parse with the platform library, not with a hand-written parser. A hand-written JSON or
XML parser is a liability with no upside here; the point of the exercise is the mapping, not
the grammar.

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

Both packages reference `Projectional.Language` only, exactly like `Expressions`.

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
- **I3 - appending to a non-empty model.** Import into a model that already has a root; assert
  the existing root is untouched, the new one is last, and one `Ctrl+Z` removes only the
  import.
- **I4 - the raw-text tier.** A construct the language does not map survives as `Xml.Raw` /
  a JSON-preserving node and regenerates to the same text.
- **I5 - the shell.** File > Import in the workbench: pick a file, create a document from it,
  and land on a clean (not dirty) document, because nothing has been edited yet.
- **I6 - docs and samples.** A sample corpus of `.json` and `.xml` files under
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
