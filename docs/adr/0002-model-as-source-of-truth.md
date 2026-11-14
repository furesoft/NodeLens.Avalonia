# ADR 0002 — The model is the only source of truth

- Status: Accepted
- Date: 2026-11-14
- Related: [model.md](../architecture/model.md), [projection.md](../architecture/projection.md)

## Context

The editor could be built in three ways:

1. **Text-first**: keep a text buffer, parse it into a tree, edit the text.
2. **Dual**: keep text and tree in sync, letting the user edit either.
3. **Model-first (projectional)**: the tree is the document; text is a rendering.

The goal is to build DSLs without writing a parser, and to allow notations that a
text buffer cannot represent (tables, forms, mixed notations). Options 1 and 2 make the
text grammar the authority, which reintroduces exactly the work this project is meant
to eliminate.

## Decision

The model — a tree of typed nodes — is the only document. There is no text buffer,
no grammar, and no text-mode fallback. Every visible character is derived on render;
every keystroke is a structural edit.

Consequences encoded in the architecture:

- No `Document.Text` anywhere in the codebase.
- Whitespace has no meaning; indentation is computed by layout.
- A model is always structurally valid; only semantic problems exist as diagnostics.
- Text export is a *feature of the language* (text generation), not the model.

## Consequences

### Positive

- No parser, no grammar, no syntax-error recovery — a whole category of complexity
  disappears.
- Notation is free per language and per concept: the same model can be shown as a
  table, a form, or a code block.
- Half-finished models are legal, which matches how humans actually work.
- Refactorings and validation are tree operations, not text surgery.

### Negative / costs

- Every editing operation must be re-implemented structurally; the platform's text
  editing infrastructure cannot be reused. This is the single largest cost of the
  project.
- Users cannot use their text-editing muscle memory; cursor movement, selection and
  copy/paste all need deliberate design.
- Diff/merge and grep are no longer free; they must be provided by the framework.
- Importing existing text files requires a real parser (out of scope before 1.0).

### Non-negotiable rules derived from this decision

1. Never introduce a shadow text buffer "temporarily".
2. Never let a language author hand-place characters; notation is declarative.
3. Never make saving depend on a valid model.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Text-first with an embedded parser** | Requires writing a parser per DSL, which is the problem statement. Also caps notation freedom. |
| **Dual text/tree with synchronisation** | The synchronisation is the hardest part of both worlds; every ambiguity becomes a bug class. |
| **Text editor + error-tolerant parser (tree-sitter style)** | Best in class for *existing* text languages, but notations like aligned tables cannot be expressed, and DSL authors still write grammars. |
