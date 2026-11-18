# Tests of a language, as data

Status: **planned** (M6). Nothing of this exists: every test in this repository is C# xunit over
`Projectional.Core.Tests`, and a language author has no way to test a language at all.

## Why

Three facts point at the same gap.

1. **A language author cannot write the test we write.** The interesting assemblies are internal by design
   ([ADR 0010](../adr/0010-public-api-surface.md)): a language package references the toolkit, and the
   toolkit deliberately carries neither `Editing` nor `Semantics`
   ([ADR 0019](../adr/0019-toolkit-is-the-language-surface.md)). So the checks, the snapshot, the generator
   and the interpreter are exactly the things a language author can *use* and cannot *assert*.
2. **What we test ourselves is one language.** The parity rule of
   [ADR 0014](../adr/0014-both-authoring-paths-are-permanent.md) is executed by three tests over a single
   fixture (`TestLanguage` and its declared twin). A DSL in a folder - `samples/declared-pipeline` - has a
   corpus that round-trips and nothing else.
3. **Question 17 already states the rule** ([open-questions.md](../open-questions.md)): a language package
   without a golden corpus is untestable and will regress. A corpus without assertions is a snapshot nobody
   compares.

MPS answers this with `jetbrains.mps.lang.test` - tests are models, assertions are annotations on the sample
code, and the same vocabulary covers the aspects:

| Aspect under test | MPS test kind | How it asserts |
| --- | --- | --- |
| constraints, scopes, type system, data flow | Node test (`NodesTestCase`) | the annotated node has an error/warning, has a type, is in a scope, or reports no error at all |
| editor, intentions, actions, key maps | Editor test (`EditorTestCase`) | initial code + a list of editing actions + expected code, plus editor-state introspection |
| generator, TextGen | Generator test | transform input models, compare the output models |
| migrations | Migration test | run the migration on input nodes, compare the output |
| BaseLanguage code | `unitTest` (`BTestCase`) | ordinary JUnit-style code |

Some parts of that map onto this framework almost one to one; the parts that do not are named under "What does
not carry over".

## What

A **`tests/` folder** in a language project, beside `structure`, `editor`, `types`, `generator` and
`constraints`, holding documents of a **test language** - one test per document, because one definition per
document is already this project's rule ([language-projects.md](language-projects.md)). File-name order is the
run order, which makes a diff of one test a diff of one file.

A test declares three things and nothing else:

- **what** it is about - the document under test, by its path inside the language project;
- **which aspect** - one of a closed set of assertions (below);
- the **expectation** - text, a list of findings, a value, or nothing at all.

The assertion vocabulary is closed on purpose, for the same reason the constraint language has four bodies and
a template nine parts: a test framework that can compute would be a programming language, and the point is
that a language author writes an assertion, not a program.

| Assertion | What it runs | What it pins |
| --- | --- | --- |
| `Testing.Draws` | `LayoutSnapshot.Render(document, notations)` (optionally through a variant) | the notation: a projection change that moves a cell fails a named test |
| `Testing.Findings` | `ConstraintEngine` + the language's `CheckSet` | constraints and checks: which findings, where, and their severities |
| `Testing.Generates` | the language's `TextGenerator`, by name | generated text and the traceability map's first node per line |
| `Testing.Evaluates` | the interpreter, with the variables a test names | the answer or the failure, and the trace |
| `Testing.RoundTrips` | `ModelXml.Serialize` twice | invariant 7 for that document |
| `Testing.Script` | a list of edit steps (type text, press a key, invoke an intention, invoke a fix) and then a `Draws` comparison | the editing behaviour MPS tests with an `EditorTestCase` |

The first five are document-level assertions: they need nothing from the framework that does not exist. The
sixth is the editor test, and it is the one that needs the most care, because it must name a caret place and
an intention without holding live objects.

## The runner is a behaviour package, not a language package

This is the one packaging decision the plan makes rather than leaves open, and it follows from ADR 0019: a
runner reads findings, types, generators and the interpreter, so it must reference `Semantics` and `Editing`,
and a package that references them can never be what a language package depends on. `Projectional.Testing`
therefore sits in the behaviour band next to `Projectional.Checks` and `…Intentions` - a package a *host*
lists, not one a language does.

The test *language* travels in the same package, which is a deliberate compromise: splitting a language from
the only thing that can run it would buy a dependency rule and cost every consumer a second reference. If a
second runner ever appears (an app, a build task), that is the moment to split.

**Running.** `TestRunner.Run(LanguageProject)` answers a `TestReport` - one row per test with the assertion,
the outcome and the actual-versus-expected text, so a failure reads as a diff. Three doors onto it:

1. **xunit**, for CI today: one test method per shipped language project that runs its `tests/` folder and
   fails with the report. The suite therefore covers the samples and the declared pipeline with no new runner
   infrastructure, and every failure names the test document.
2. **The workbench**, later: a Run tests command whose rows go to the output panel, which already knows how to
   show a row per line and jump to the node that produced it.
3. **A host**, whenever it wants: the report is a value, not console output.

## What does not carry over, and why

| MPS | Ours |
| --- | --- |
| assertions are annotated onto the sample code (`@check`) | not possible without a common base concept: a slot must be declared over a concept key, and languages here share no root (the interface plan adds `Framework.Named`, not `BaseConcept`). A test names a document instead |
| a test model holds many tests | one test per document, like every other definition |
| caret markup inside the sample code | the action script places the caret, because a document under test stays a plain document |
| `TestInfo` with a project path, in-process versus out-of-process | not applicable: the runner is handed the project, and there is no second process to start |
| BaseLanguage code in a test, labels, utility methods | not applicable: the vocabulary is closed, so there is no code in a test |
| migration tests | no migration tooling exists yet (M6) - the assertion arrives with it |
| two-phase deletion, editor-state introspection | MPS-specific: they test the IDE's own furniture, and this editor owns its session |

## Slices

| Slice | Content | Effort |
| --- | --- | --- |
| T1 | the `tests/` folder and the language (`Testing.Draws`, `Testing.Findings`, `Testing.RoundTrips`), `TestRunner`, the xunit bridge, and the sample language's first tests | 1 week |
| T2 | `Testing.Generates` and `Testing.Evaluates` (generators by name, variables from one line of `name=value` pairs, the way `RunInputs` already reads them) | 3-4 days |
| T3 | `Testing.Script`: the action vocabulary, caret placement, and the comparison after the run - the editor test | 1-2 weeks |
| T4 | node-addressed assertions (`Testing.TypeOf`, `Testing.ScopeOf`), which need a way for a test to name a node: a path syntax, or a reference once a common base concept exists | open |
| T5 | the workbench command, the report rows, and "run on save" if it is wanted | 3-4 days |

## Non-goals

- **No code in a test.** A declarative assertion cannot branch, loop or compute; a test that needs that is a
  test of the framework, and the framework's own tests stay in xunit.
- **No performance testing.** Timing belongs to the profiling pass (M7) and its own harness.
- **No test discovery outside a language project.** The `tests/` folder belongs to a language; a project-wide
  test runner is a shell feature, not this one.
- **No assertion is silently relaxed.** A snapshot compares text; an assertion that tolerates a difference
  says so in its declaration, as MPS' match options do.

## What still needs a decision

- **How a test names its document.** A path relative to the language project is explicit and reviewable, and
  it is what MPS' `TestInfo` does for the project root. A reference to the model root would be stronger, but a
  reference slot needs a concept key and a root concept is the language's own - the common base concept this
  framework deliberately does not have. Take the path now; revisit in slice T4, or once a language can declare
  a concept that *every* root conforms to (neither the interface plan nor anything else adds one today). A third
  answer arrives with [smodel.md](smodel.md): a pointer or a query is what MPS uses to name a node in a test,
  and it would let an assertion name *any* node instead of a whole document.
- **What a snapshot compares.** Exact text is honest and brittle in one way: a change of measure or width
  rewrites every expectation. Renderer output is the thing being pinned, so exact text is right - but the
  report must show the diff rather than "not equal", or a width change reads as fifty failures.
- **Whether a language may ship tests without shipping a corpus.** A test that names a document needs the
  document; a document in `Corpus/` is a fixture. They can be the same files, and keeping one file for both
  purposes is the smaller answer.
- **Whether the first consumer is a sample or the framework's own language.** The declared pipeline is the
  natural first customer - it has a corpus and no assertions - and it is also the proof that a *user's* folder
  can be tested without C#.
