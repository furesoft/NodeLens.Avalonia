# Documentation

This directory holds the design baseline for the .NET projectional editor. It is
written *before* the implementation: each document states what will be built, which
constraints it must satisfy, and which alternatives were rejected.

## Reading order

1. [Architecture overview](architecture/overview.md) — the five layers and why they
   are separated.
2. [Model](architecture/model.md) — nodes, identity, and the change-event backbone.
3. [Language definition](architecture/language.md) — concepts, aspects, and the
   three bootstrap stages.
4. [Projection](architecture/projection.md) — from model to pixels.
5. [Editing](architecture/editing.md) — caret identity, edit actions, and the session.
6. [Persistence](architecture/persistence.md) — XML, project layout, migrations.
7. [Semantics](architecture/semantics.md) — constraints, types, scopes, generation.
8. [Integration](architecture/integration.md) — how host applications embed this.
9. [Testing](architecture/testing.md) — the corpus, the snapshot harness, and CI.
10. [Roadmap](roadmap.md) — milestones and acceptance gates.
11. [Open questions](open-questions.md) — what is deliberately still undecided.

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
