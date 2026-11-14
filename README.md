# Projectional Editor for .NET

A projectional editor framework for .NET, modelled after JetBrains MPS: models are
trees of nodes, not text buffers. Notation is *projected* from the model, and every
keystroke is interpreted as a structural edit. This makes it possible to define
domain-specific languages without writing a parser, and to give each language a
notation that fits the domain — text-like, tabular, form-based or diagrammatic.

The editor is designed to be embedded as a component in other .NET applications:
either as a design-time language workbench, or as an end-user model editor inside a
shipped product.

> **Status: planning.** No implementation yet. The documents under [`docs/`](docs/)
> are the design baseline for milestone M0.

## Documentation

| Document | Content |
| --- | --- |
| [docs/README.md](docs/README.md) | Documentation map and reading order |
| [docs/architecture/overview.md](docs/architecture/overview.md) | Layers, projects, data flow |
| [docs/architecture/model.md](docs/architecture/model.md) | Node model, identity, change tracking |
| [docs/architecture/language.md](docs/architecture/language.md) | Concepts, aspects, bootstrap stages |
| [docs/architecture/projection.md](docs/architecture/projection.md) | Editor tree, layout, caret, virtualization |
| [docs/architecture/persistence.md](docs/architecture/persistence.md) | XML format, project layout, migrations |
| [docs/architecture/semantics.md](docs/architecture/semantics.md) | Constraints, types, scopes, generators, interpreter |
| [docs/architecture/integration.md](docs/architecture/integration.md) | Embedding API, plugin model, packaging |
| [docs/roadmap.md](docs/roadmap.md) | Milestones M0–M7 with acceptance gates |
| [docs/open-questions.md](docs/open-questions.md) | Decision log for unresolved aspects |
| [docs/adr/](docs/adr/) | Architecture decision records |

## Non-goals

- No parser, no text buffer as the source of truth, no text-mode fallback.
- No hand-built UI per language; everything language-specific is declarative.
- No multi-user collaboration, no graphical diagram editing, and no
  model-to-model generators before 1.0.

## License

Apache-2.0 (see [ADR 0003](docs/adr/0003-runtime-and-license.md)).
