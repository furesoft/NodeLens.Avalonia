# Documentation

This directory holds the design baseline for the .NET projectional editor. Most documents
are written *before* the implementation: each states what will be built, which constraints it
must satisfy, and which alternatives were rejected. A document whose feature has shipped says
so — a `Status: **delivered**` line under the title, or "(delivered)" in the list below — and
records what was actually verified.

## Reading order

1. [Architecture overview](architecture/overview.md) — the five layers and why they
   are separated.
2. [Model](architecture/model.md) — nodes, identity, and the change-event backbone.
3. [Language definition](architecture/language.md) — concepts, aspects, and the
   three bootstrap stages.
4. [Language authoring](architecture/language-authoring.md) — how a language is written down,
   in C# declarations or as structure and editor models in the project.
5. [Projection](architecture/projection.md) — from model to pixels.
6. [Editing](architecture/editing.md) — caret identity, edit actions, and the session.
7. [Node moves](architecture/node-moves.md) — reordering a node without losing its identity;
   the first slice is implemented.
8. [Completion](architecture/completion.md) — offering the concepts an empty slot accepts;
   the first slice is implemented.
9. [Context actions](architecture/context-actions.md) — the light bulb: intentions offered
   at the caret (planned; after quick fixes).
10. [Markers](architecture/markers.md) — wavy severity underlines under what validation
   reports (delivered).
11. [Quick fixes](architecture/quick-fixes.md) — the repairs a finding carries, and how they
    reach the user (in progress; the fix model is delivered).
12. [Line numbers](architecture/line-numbers.md) — numbering the rows a
    projection produced, and painting them in a gutter (delivered).
13. [Font size](architecture/font-size.md) — zooming a document by replacing its measure
    (delivered).
14. [Clipboard](architecture/clipboard.md) — copy, cut and paste through a host-supplied
    text clipboard (delivered).
15. [Persistence](architecture/persistence.md) — XML, project layout, migrations.
16. [Semantics](architecture/semantics.md) — constraints, types, scopes, generation.
17. [Integration](architecture/integration.md) — how host applications embed this.
18. [Standalone editor](architecture/standalone.md) — the app that ships alongside,
    and why it has no privileged access.
19. [Embedded shell](architecture/embedded-shell.md) — the shell as a component a host
    composes, locked to one language.
20. [Testing](architecture/testing.md) — the corpus, the snapshot harness, and CI.
21. [Roadmap](roadmap.md) — milestones and acceptance gates.
22. [Open questions](open-questions.md) — what is deliberately still undecided.

Decision records live in [adr/](adr/) and are frozen once accepted; revised
decisions get a new number and supersede the old one.

## Working with an AI agent

The repository ships a project skill at
[.github/skills/projectional-editor/SKILL.md](../.github/skills/projectional-editor/SKILL.md)
that condenses the invariants, the branch and commit workflow, the build commands and the
Windows tooling pitfalls. It is the entry point for an agent picking up work here. This
directory stays authoritative whenever the two disagree.

## Conventions

- Documents describe the intended system, not the current state. Where the
  implementation will lag behind, the roadmap marks the milestone.
- Terms are used with MPS meaning: *concept*, *property*, *child*, *reference*,
  *aspect*, *scope*, *substitution action*, *projection*.
- Code snippets are illustrative shape, not final API. The public API is frozen by
  [ADR 0010](adr/0010-public-api-surface.md).

## Decision overview

| ADR | Decision |
| --- | --- |
| [0001](adr/0001-ui-stack-avalonia.md) | Avalonia as the UI stack |
| [0002](adr/0002-model-as-source-of-truth.md) | The model is the only source of truth |
| [0003](adr/0003-runtime-and-license.md) | .NET 10 LTS, Apache-2.0 |
| [0004](adr/0004-stable-node-ids.md) | Stable node IDs with `resolveInfo` fallback |
| [0005](adr/0005-xml-persistence.md) | XML persistence, MPS-like |
| [0006](adr/0006-change-event-backbone.md) | Change events are the backbone |
| [0007](adr/0007-reflection-free-language-definition.md) | Reflection-free language definition |
| [0008](adr/0008-bootstrap-order.md) | Built-in languages before self-hosting |
| [0009](adr/0009-custom-drawn-editor-control.md) | Custom-drawn control with structural caret |
| [0010](adr/0010-public-api-surface.md) | Public API limited to `Core` and `Avalonia` |
| [0011](adr/0011-standalone-editor.md) | A standalone editor ships, as a plain consumer of the public API |
| [0012](adr/0012-concept-rendered-more-than-one-way.md) | A concept may be rendered more than one way |
| [0013](adr/0013-markers-are-view-state.md) | Markers are view state: the frame never carries them |
| [0014](adr/0014-both-authoring-paths-are-permanent.md) | Languages are defined in code and in the project, both permanently |
| [0015](adr/0015-shell-is-a-library.md) | The shell is a library a host composes, and it can be locked to one language |
