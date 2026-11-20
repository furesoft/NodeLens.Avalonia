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
5. [Interfaces](architecture/interfaces.md) — the framework's structural vocabulary: a
   concept kind whose members an implementing concept inherits rather than retypes (planned; M5).
6. [Concept behaviours](architecture/behaviours.md) — what a node is called and how it describes
   itself, as pure functions a concept declares instead of a name slot it retypes (planned; M6).
7. [The smodel language](architecture/smodel.md) — queries over the tree in MPS' own operation
   words, as a pure-read language whose changes stay with the edit actions (planned; M6).
8. [Projection](architecture/projection.md) — from model to pixels.
9. [Editing](architecture/editing.md) — caret identity, edit actions, and the session.
10. [Node moves](architecture/node-moves.md) — reordering a node without losing its identity;
    the first slice is implemented.
11. [Completion](architecture/completion.md) — offering the concepts an empty slot accepts;
    the first slice is implemented.
12. [Context actions](architecture/context-actions.md) — the light bulb: intentions offered
    at the caret (planned; after quick fixes).
13. [Intention preview](architecture/intention-preview.md) — what an entry would do, shown on
    a longer hover by projecting a discarded copy.
14. [Markers](architecture/markers.md) — wavy severity underlines under what validation
    reports (delivered).
15. [Quick fixes](architecture/quick-fixes.md) — the repairs a finding carries, and how they
    reach the user (in progress; the model, the apply step and two real checks are delivered).
16. [Concept descriptions](architecture/concept-descriptions.md) — the sentence a language
    writes about a concept, shown on hover and in the completion list (planned).
17. [Line numbers](architecture/line-numbers.md) — numbering the rows a
    projection produced, and painting them in a gutter (delivered).
18. [Font size](architecture/font-size.md) — zooming a document by replacing its measure
    (delivered).
19. [Clipboard](architecture/clipboard.md) — copy, cut and paste through a host-supplied
    text clipboard (delivered).
20. [Persistence](architecture/persistence.md) — XML, project layout, migrations.
21. [Semantics](architecture/semantics.md) — the pass order and the diagnostics, then one file per
    aspect under [architecture/semantics/](architecture/semantics/): constraints, types, generation
    and interpretation.
22. [Model generation](architecture/model-generation.md) — reducing a document into a model of
    a target language, which that language's generator then prints (planned; M6).
23. [Worked examples](architecture/worked-examples.md) — three sample languages, one per axis
    the framework bets on, each with its checks, its corpus and its snapshots (planned; M4).
24. [Integration](architecture/integration.md) — how host applications embed this.
25. [Standalone editor](architecture/standalone.md) — the app that ships alongside,
    and why it has no privileged access.
26. [Embedded shell](architecture/embedded-shell.md) — the shell as a component a host
    composes, locked to one language.
27. [Testing](architecture/testing.md) — the corpus, the snapshot harness, and CI.
28. [Tests of a language](architecture/language-tests.md) — a language's own assertions as data:
    the drawing, the findings, the generated text, a run's answer (planned; M6).
29. [Gaps against MPS](architecture/mps-gap-analysis.md) — what MPS has that this framework does
    not, and which of it is deliberate.
30. [Packaging](architecture/packaging.md) — every project is a NuGet package, versioned by
    GitVersion and published from a tag.
31. [Roadmap](roadmap.md) — milestones and acceptance gates.
31. [Open questions](open-questions.md) — what is deliberately still undecided.
32. [Text import](architecture/import.md) — the intake contract and, under
    [architecture/import/](architecture/import/), one file per reference language (JSON, XML, TOML,
    HOCON, YAML) plus the declared importer (delivered; the parity rule still has one subject).
33. [Interactive language documentation](architecture/language-docs.md) — a language folder turned
    into static, searchable HTML that can be read without the editor (planned; the four decisions
    the implementation has to take are listed there).

Decision records live in [adr/](adr/) and are frozen once accepted; revised
decisions get a new number and supersede the old one.

## Working with an AI agent

The repository ships a project skill at
[.github/skills/projectional-editor/SKILL.md](../.github/skills/projectional-editor/SKILL.md)
that condenses the invariants, the branch and commit workflow, the build commands and the
Windows tooling pitfalls. It is the entry point for an agent picking up work here, and it is
deliberately an index: the knowledge of one layer sits in its own file beside it
(guide-core-model.md, guide-language.md, …) so a session reads only the layer it touches.
This directory stays authoritative whenever the two disagree.

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
| [0016](adr/0016-quick-fixes-are-edits-a-host-supplies.md) | Quick fixes are edits a host supplies, applied by the session |
| [0017](adr/0017-preview-is-a-discarded-edit.md) | A preview is a projection of a discarded edit |
| [0018](adr/0018-concept-description-is-language-data.md) | A concept description is language data, not model data |
| [0019](adr/0019-toolkit-is-the-language-surface.md) | The toolkit is the surface a language is written against |
| [0020](adr/0020-framework-vocabulary-is-injected.md) | The framework's vocabulary is injected into every registry |
| [0021](adr/0021-one-project-per-language.md) | One project per language: checks and intentions are folders in the language |
| [0022](adr/0022-every-project-is-a-package.md) | Every project is a package, versioned by GitVersion and published from a tag |
