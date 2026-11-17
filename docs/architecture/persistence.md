# Persistence

Models are trees, so the stored form is a tree too — but the file format is
constrained by three things that are not obvious: **git diffs should be readable**,
**IDs must be stable**, and **old files must stay loadable**.

## What M0 implements

`Projectional.Persistence` stores and reads the core model tree. The shape below is
final for that scope; everything this document describes beyond it (imports, project
layout, migrations, diagnostics on load) is still design and marked as such.

```xml
<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="11111111-..." name="Payroll">
  <nodes>
    <node id="22222222-..." concept="Test.Rule" name="Payroll">
      <properties>
        <name>Payroll</name>
        <enabled>true</enabled>
      </properties>
      <references role="tag" target="33333333-..." resolveInfo="Salary" />
      <children role="body">
        <node id="44444444-..." concept="Test.Body">
          <children role="steps">
            <node id="55555555-..." concept="Test.Step">
              <properties>
                <text>Pay</text>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>
```

As built, and worth knowing:

- **The public surface is two static classes.** [`ModelXml`](../../src/Projectional.Persistence/ModelXml.cs)
  converts between a model and a string or stream; [`ModelFile`](../../src/Projectional.Persistence/ModelFile.cs)
  adds the file and the atomic replace. There is no separate serializer object to keep
  in sync.
- **`formatVersion` is written as `1` and validated on read.** A newer version fails
  with a message naming the version; a *missing* attribute is read as the oldest
  format, so a hand-written file without it still opens.
- **The `name` attribute on `node` duplicates the name property.** It is written for
  greppability (`grep 'concept="Test.Rule"'` shows the name) but never read back: the
  property is authoritative, so the two can never disagree silently — a hand-edited
  `name` is simply ignored.
- **`version` and `imports` are not written yet.** `Model.Version` is a mutation
  counter, not a schema version, and import bookkeeping only becomes meaningful with
  the language module (M2). The reader tolerates their absence.
- **A failed load leaves nothing behind.** The reader builds into the project and, on
  any error, deletes what it built (recording nothing) and removes the model again, so
  a broken file cannot leave a half-model in the project index. Every failure is a
  [`ModelFormatException`](../../src/Projectional.Persistence/ModelFormatException.cs).
- **Values are validated while reading.** Property values go through `Node.Set`, so a
  value the language refuses fails the load instead of being kept as a string for later
  diagnostics. This is the one place where M0 deliberately departs from the
  "never drop data on load" rule below: the tolerant path arrives with the diagnostics
  milestone, and until then a bad value is a loud failure rather than silent loss.

## File format

XML, MPS-like. Human-readable, diffable, streamable.

The sample below is the target shape once languages and imports exist; the part that
M0 writes is shown in the previous section.

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
        structure/            # concept declarations (declared languages), one definition per file
        editor/               # notation declarations, one notation per file
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
