# Persistence

Models are trees, so the stored form is a tree too — but the file format is
constrained by three things that are not obvious: **git diffs should be readable**,
**IDs must be stable**, and **old files must stay loadable**.

## File format

XML, MPS-like. Human-readable, diffable, streamable.

```xml
<model id="a1f3c0de-..." name="Payroll" language="Rules" version="3">
  <imports>
    <import id="b2c4d1ef-..." name="Projectional.Expressions" version="1"/>
  </imports>
  <nodes>
    <node id="c9d1e2f3-..." concept="Rules.RuleSet" name="Payroll">
      <properties>
        <maxAmount>1000</maxAmount>
      </properties>
      <children role="rules">
        <node id="d4e7f8a9-..." concept="Rules.Rule" name="Approved">
          <references role="condition" target="e8f2a3b4-..." resolveInfo="Amount"/>
          <children role="outcome">
            <node id="f1a2b3c4-..." concept="Rules.Action" name="Accept"/>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>
```

Structural choices, and why:

| Choice | Reason |
| --- | --- |
| `concept` and slot names as attributes/values (not indices) | readable diffs; a renamed concept is visible in review |
| Children grouped by role, then ordered | stable diffs; ordering only inside a role |
| `resolveInfo` next to `target` | self-healing references after moves ([ADR 0004](../adr/0004-stable-node-ids.md)) |
| `version` on model *and* imports | migrations need to know the language version per import |
| No whitespace significance, no text escapes beyond XML | property values are XML-escaped only; no custom escaping rules |

Determinism rules — these are enforced by tests, because retrofitting them breaks
every existing file:

1. Slot output order is the declaration order of the concept, not insertion order.
2. Attributes are written in fixed order (`id`, `concept`, then name).
3. Line endings are `\n`, encoding is UTF-8 without BOM.
4. Indentation is two spaces, no tabs.
5. Nothing time-dependent, machine-dependent, or hash-based is written.

The test is blunt: serialize → deserialize → serialize must be byte-identical, for
every model in the test corpus.

## Project layout

A project is a directory. This mirrors MPS and keeps generators, languages and models
separately greppable.

```text
MyDsl/
  project.xml                 # project name, referenced packages, settings
  languages/
    Rules/
      language.xml            # name, version, dependencies
      models/
        structure.mps         # concept declarations (declared languages)
        editor.mps            # notation declarations
  models/
    payroll.mps               # user models
  generators/
    RulesToCSharp/
      generator.xml
      models/
        rules.mps
```

- One file per model; the file name is the model name.
- IDs are GUIDs, so models can be moved between directories without breaking
  references.
- `project.xml` records language *versions*, so a project knows what it was built with.

## Load and save

**Load**

1. Parse XML into node data (no concept validation yet).
2. Run migrations per model/import, in version order.
3. Resolve references: by ID, then `resolveInfo` fallback, then leave unresolved.
4. Validate against concepts; violations produce diagnostics instead of a failed load
   (a model that lost a concept must still open, otherwise the user cannot fix it).
5. Build the index and mark the model clean.

**Save**

- Write to a temp file in the same directory, flush, then atomic replace. A crash must
  never leave a half-written model.
- Only dirty models are written.
- Saves never reorder children or renumber IDs; saving an unmodified model must
  produce an identical file.

**Never** write on a background thread while a transaction is open. The save path
takes a snapshot and writes from it.

## Partial and broken models

Explicitly supported, because projectional editing produces them constantly:

| Situation | Handling |
| --- | --- |
| Unresolved reference | kept as `target` + `resolveInfo`, marked with an error; save preserves it verbatim |
| Node whose concept was removed | kept as an opaque node with its raw data, marked as error; editable only as text |
| Property that no longer parses | kept as string, marked with an error |
| Cyclic imports | reported as a project error, loaded deterministically (sorted by ID) |

Rule of thumb: **never drop data on load.** Losing user work to a version bump is the
one unrecoverable failure mode.

## Migrations

```csharp
Migration.ForLanguage("Rules")
         .From(2).To(3)
         .Transform((node, ctx) => { /* node-level rewrite inside one transaction */ });
```

- Chained: `2 → 3 → 4` runs automatically.
- Applied to the in-memory model after parse, before validation.
- One transaction per model, so a migration is a single undo step if the user opens it
  and immediately undoes.
- Migrations never run on save; they only run on load. Old files stay on disk until
  saved.
- Every migration gets a golden-file test: old XML in, new XML out.

## Git workflow

- Deterministic serialization makes line diffs meaningful.
- Node IDs are the merge key; a textual 3-way merge is acceptable for non-overlapping
  edits and will be replaced by a model-aware merge at M6.
- Because saves preserve untouched nodes byte-for-byte, moving a model between
  directories produces a rename, not a rewrite.

## Versioning

| Artefact | Versioning |
| --- | --- |
| XML format | `formatVersion` attribute; bumped only for incompatible reader changes |
| Language | semantic version per language, migrations for breaking changes |
| Bootstrap models | versioned with the code, migrated like user data |

Reading a newer `formatVersion` fails loudly with an actionable message instead of
partially loading.
