# ADR 0005 — XML persistence, MPS-like

- Status: Accepted
- Date: 2026-11-14
- Related: [persistence.md](../architecture/persistence.md)

## Context

The stored form of a tree document has to satisfy requirements that text files get for
free and tree formats usually lose:

| Requirement | Why it matters here |
| --- | --- |
| Human-readable | a DSL user with a broken model must be able to inspect and repair it |
| Diffable | projects live in git; opaque blobs make review impossible |
| Stable across saves | untouched nodes must not churn; otherwise diffs are noise |
| Forward/backward compatible | models outlive the framework version that wrote them |
| Streamable | a million-node model must load without a gigabyte of RAM spikes |
| Self-describing | a file must reveal which language and version produced it |

Candidates: JSON, XML, a custom line-oriented notation, binary, SQLite.

## Decision

**XML, structured like MPS model files**, with strict determinism rules:

- one file per model, extension `.mps`
- attributes for identity (`id`, `concept`, `name`), nested elements for slots
- fixed serialization order; UTF-8 without BOM; `\n` line endings; two-space indent
- no timestamps, no hashes, no machine data in the output
- `version` on the model and on every import

## Consequences

### Positive

- Attributes plus nesting map naturally onto a node tree; no custom escaping rules.
- XML tooling (pretty-printing, schema validation, XPath for debugging) exists and needs
  no work.
- MPS compatibility of *shape* means the design can borrow proven solutions instead of
  inventing a format language.
- A corrupted file is repairable by hand in any editor — an important property for user
  data.

### Negative / costs

- XML is verbose; files are roughly 2–3× a compact JSON.
- Deterministic output is not free: the writer must never rely on dictionary order and
  must enforce declaration order for slots. Enforced by a byte-identical round-trip test.
- Merge conflicts in XML are textual; a proper model-aware merge is deferred to M6.

### Rules

1. The writer is the only component that decides ordering — never the data structures.
2. A reader must accept missing optional attributes but reject unknown
   `formatVersion` values loudly.
3. Loading never drops data it does not understand; unknown nodes are preserved
   verbatim as opaque nodes.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **JSON** | No attribute/element distinction, so either IDs explode into extra nesting or the format becomes irregular; also no natural way to distinguish a slot list from metadata. Compactness does not compensate. |
| **Custom line-oriented notation** | Best possible diffs, but inventing a format language (escaping, nesting, references, versioning) is a project in itself, with no tooling and no ecosystem. |
| **Binary / MessagePack** | Fast and small, but invisible in git and unrepairable by the user. Unacceptable for user-owned models. |
| **SQLite** | Great performance, but a database file is not a document: no diffs, no review, no hand repair. Also complicates "one model = one file". |
