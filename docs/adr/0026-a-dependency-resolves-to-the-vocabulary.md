# ADR 0026 — A dependency is resolved by the host and narrowed to the vocabulary

- Status: Accepted
- Date: 2026-11-21
- Related: [language-projects.md](../architecture/language-projects.md#resolving-a-dependency),
  [ADR 0014](0014-both-authoring-paths-are-permanent.md), [ADR 0021](0021-one-project-per-language.md)

## Context

`language.mps` declares the languages a language stands on (the language-root slice), but nothing acts on the
declaration. The gap is concrete rather than theoretical: a concept whose child slot is declared over another
language's concept cannot even load, because `ConceptRegistry.Create` validates the graph and refuses a slot naming a
concept nobody declared.

Two facts about the existing code decide the shape:

- **Composition is already the idiom.** The export's README tells a host to concatenate declarations
  (`[.. Own.ConceptDeclarations, .. Theirs.ConceptDeclarations]`), and `WorkbenchLanguage` does exactly that for
  every opened language: one registry built from the shell's declarations plus each language's *own*
  declarations, and one notation registry built the same way. Nothing absorbs anything; the host composes.
- **A project has one concept registry.** `Project` holds a single `ConceptRegistry` and every model of it is
  written in that vocabulary. So "this language also uses that language's concepts" can only mean "this language's
  registry is built over both declarations".

So the missing piece is not a merge mechanism between projects - the framework already has one - but the ability of
a *single* project to validate its own definitions against the bases it declared.

## Decision

**A language resolves its declared dependencies through a resolver the host supplies, and resolution narrows to the
vocabulary.** Concretely:

1. **The host resolves, the framework never searches.** `LanguageProject.Open` takes an optional `ILanguageResolver`
   whose only method is `Find(name) -> LanguageProject?`. There is no directory scan, no feed lookup, no convention
   about where a sibling language lives: what a dependency *is* is a deployment question, and this repository has no
   answer for it yet ([distribution.md](../architecture/distribution.md) is where that would land).

2. **Resolution is the project's own registry, not an absorption.** The project's `Concepts` is built from its
   bases' declarations (transitively, base before dependent) followed by its own; its accessory models are the
   union of the same chain, so a reference to a base's library node still resolves. `Declarations`, `Notations` and
   every behavioural aspect stay **the language's own**: they are what it says, not what it borrows. This is the
   composition the shell and the export already perform, moved to the one place that was missing it.

3. **Presentation and behaviour are composed by the host, not inherited.** The shell already unions the notations of
   every opened language, which is why a node of a base language draws in a dependent's document. Behaviour is not
   inherited by this decision: a base's constraints, scopes, types, typing rules and intentions do not run in a
   dependent's document. A language that wants those guarantees states them itself, in its own folders, or the host
   composes the sets. This is a deliberate limit with a named follow-up, not an oversight - inheriting behaviour
   changes what a dependency promises, and it needs the composite registries and the "who owns this concept's
   invariants" answer that this decision does not have.

4. **Generators and importers never cross the boundary.** They consume and produce documents rather than describe a
   node. Offering a base's generator over a dependent's document would be the framework inventing a mapping nobody
   wrote.

5. **A declaration is not a promise the framework checks.** The version in a dependency is a label: it is read,
   reported and shown, and it is not compared with the resolved language's version, because nothing here parses a
   version and refusing on free text would be a rule nobody can satisfy. Declaring `Core 2.0` beside a resolved
   `Core 1.0` opens; a host that cares reports it.

6. **A dependency that does not resolve refuses the open.** A message names the language and the folder that asked
   for it. The alternative - loading without the base - produces a language whose slots name concepts it lacks,
   which is precisely the state the registry already refuses, only later and with a worse message.

7. **Cycles are refused, with the chain.** `A` depending on `B` depending on `A` is not a language, and a resolver
   that answers both would otherwise recurse forever.

8. **Two languages declaring one concept key still collide.** Resolution does not introduce overrides: a language
   that wanted to re-declare a base's concept key would be a second definition of one key, which
   `ConceptRegistry.Create` already refuses and this decision keeps refusing. Overriding a base's *notation* for one
   of its concepts is a separate feature and is not part of this one.

## Consequences

- **A library host composes; it does not inherit.** Passing a resolver makes a project's *own* definitions valid.
  Drawing and checking a document that mixes languages remains what it always was: give the session the composed
  registries - which is what the shell does with the languages it has opened.
- **The workbench resolves against what is open.** `File ▸ Open language…` resolves a declared dependency against
  the languages the window already holds, and tells the user to open the base first when it cannot. That keeps the
  "one language, one folder, no hidden search path" rule the loader already follows.
- **Nothing is persisted differently.** Models still record no language version ([persistence.md](../architecture/persistence.md)
  describes that shape as planned), so this decision adds no migration and no new field.
- **The gap is visible rather than silent.** Module settings reports what resolved beside what was declared.

## Rejected alternatives

- **A resolver that walks sibling directories or a package feed.** Convenient, and it makes the load depend on the
  machine's layout; it also decides a deployment question in a library.
- **Merging every aspect into the dependent language.** It would give a base's checks in a dependent's document, at
  the cost of two languages owning one rule set and no way to say which one is in force - the composite semantics is
  the follow-up, not the first slice.
- **Loading without an unresolved base and reporting afterwards.** A half-loaded language is a folder whose
  documents cannot be opened; refusing at the open is the earlier, clearer failure.
