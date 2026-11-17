# ADR 0019 — The toolkit is the surface a language is written against

- Status: Accepted
- Date: 2026-11-17
- Related: [ADR 0007](0007-reflection-free-language-definition.md),
  [ADR 0010](0010-public-api-surface.md), [ADR 0014](0014-both-authoring-paths-are-permanent.md),
  [language-authoring.md](../architecture/language-authoring.md), [overview.md](../architecture/overview.md#projects)

## Context

A language in this framework is a package that declares concepts and notation, and every shipped
language is written that way. Until now that package referenced `Core`, `Language` and - when it wanted
to test its notation - `Projection` and `Persistence`. Four references to reach one idea, and the count
is not the problem: **what a language author may see was decided by whatever happened to be `public`
in four assemblies, none of which was designed as a language surface.**

That was already visible in [ADR 0010](0010-public-api-surface.md), whose table says `Language`,
`Projection` and `Persistence` are internal. They are not, and cannot be: a language is a *third-party*
assembly. `InternalsVisibleTo` names friends in advance, so it can cover the tests and the persistence
package (which Core names), but it can never name the language somebody else writes next year.

So the real choice was never "public or internal". It is:

1. leave the question to whichever member happened to be public, and let the effective surface drift; or
2. **name one assembly as the language surface**, keep everything behind it as it is, and give that one
   assembly the few entry points a language genuinely needs.

## Decision

`Projectional.Toolkit` is the third public surface, beside `Projectional.Core` and
`Projectional.Avalonia`. A language package references it and nothing else.

| Assembly | Public surface |
| --- | --- |
| `Projectional.Core` | as [ADR 0010](0010-public-api-surface.md) lists it - the model, changes, diagnostics |
| `Projectional.Avalonia` | as [ADR 0010](0010-public-api-surface.md) lists it - the control, the session, theming |
| `Projectional.Toolkit` | **new**: the language-authoring surface, `LanguageSnapshot` today |
| `Projectional.Language`, `Projectional.Projection`, `Projectional.Persistence` | public as a *consequence*: the toolkit aggregates them, and a language reaches their types through it |
| `Projectional.Editing`, `Projectional.Semantics` | internal, unchanged |

Four rules follow, and each is a decision rather than a detail:

1. **The toolkit aggregates `Core`, `Language`, `Projection` and `Persistence`, and deliberately not
   `Editing` or `Semantics`.** Those two are the editor. Keeping them out is the same argument that put
   `ITypingRules` and `ICellEmphasis` in `Language`: a language package must not be able to reference the
   editing layer, or "headless language" stops being a property of the build and becomes a rule somebody
   has to remember. A language that needs a check or an intention ships a *second* package beside it,
   which is what `Projectional.Samples.Checks` and the intention packages already are.
2. **`Persistence` is in, although it is a host concern**, because the promise the toolkit makes is
   "a language starts testable": the golden corpus is a byte-stability test, and it needs the writer.
3. **The one entry point the toolkit adds is `LanguageSnapshot`.** The renderer is internal on purpose -
   [ADR 0010](0010-public-api-surface.md) keeps projection closed so a language cannot build its own -
   but a language must be able to *look* at the projection it declared, or a notation cannot be tested
   at all. The projection layer therefore names the toolkit the way Core names `Persistence`: a friend
   with one narrow, documented reason. The toolkit adds no behaviour; each method is the renderer's own
   call.
4. **Nothing else is re-exported.** The toolkit is a reference list plus one type, not a façade over the
   framework. A type that wants to be part of the language surface is moved there deliberately, one at a
   time, with this rule in mind.

This amends the table in [ADR 0010](0010-public-api-surface.md): it said `Language`, `Projection` and
`Persistence` are internal, which the code had already stopped being. Naming the toolkit does not make
them *more* public - it makes the reason explicit and gives a future decision one place to happen.

## Consequences

- A language package has **one** `ProjectReference`. The template (`dotnet new projectional-language`)
  can therefore produce a project that compiles against the toolkit alone, and its test project does too.
- The test that keeps this honest is a test *project*: `tests/Projectional.Toolkit.Tests` references
  only `Projectional.Toolkit`, declares a language, draws it and round-trips it. If the surface shrank,
  that project stops compiling - a stronger statement than a rule in a document.
- `LanguageSnapshot` is a compatibility promise from now on. Widening it (a diff, marks, a custom
  measure) is a decision, not a convenience.
- The toolkit is not a versioning boundary in the same sense as `Core`: it exists for authors, and its
  one type may grow as the authoring story does. What must not happen is a host reaching for it as a
  shortcut around the control.

## Alternatives considered

- **Make the renderer public in `Projection`.** Fewer moving parts, and it makes ADR 0010's "internal"
  claim for that assembly false in the one place it matters most: the snapshot is a test tool, and
  `Projection` is where projection lives. A friend grant on one type keeps the assembly's promise.
- **Grant `InternalsVisibleTo` to each language by name.** Works for the languages in this repository
  and never for the one a user writes, which is the case the framework exists for.
- **Let languages reference the four assemblies directly** (today's state). It works, and it is exactly
  the drift this ADR is about: no assembly owns the answer to "may a language use this?".