# Tests of a language, as data

Status: **delivered** (M6). All five slices are complete: the `tests/` folder, the test language, `TestRunner`,
the xunit bridge and the sample's test documents, with all eight assertions a document can make -
`Testing.RoundTrips`, `Testing.Draws`, `Testing.Findings`, `Testing.Generates`, `Testing.Evaluates`,
`Testing.Script` (the editor test) and the two node-addressed ones, `Testing.TypeOf` and `Testing.ScopeOf` -
plus the workbench command that runs a language's folder and lists one row per test.

Before this existed, every test in this repository was C# xunit over `Projectional.Core.Tests`, and a
language author had no way to test a language at all.

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
a template twelve parts: a test framework that can compute would be a programming language, and the point is
that a language author writes an assertion, not a program.

| Assertion | What it runs | What it pins |
| --- | --- | --- |
| `Testing.Draws` | `LayoutSnapshot.Render(document, notations, width)` | the notation: a projection change that moves a cell fails a named test |
| `Testing.Findings` | `ConstraintEngine` over `LanguageProject.Constraints` | the language's declared constraints: which findings, where, and their severities |
| `Testing.Generates` | the language's `TextGenerator`, by name | the generated text; the traceability map is not pinned yet |
| `Testing.Evaluates` | `Interpreter` over the rules the caller supplies, with the variables a test names | the answer, or the message of the failure that stopped the run; the trace is not pinned yet |
| `Testing.RoundTrips` | `ModelXml.Serialize` twice | invariant 7 for that document |
| `Testing.Script` | an `EditorSession` over the document, replaying steps through `KeyRouter` and `Finder` | what a user's keystrokes do: the model after the steps, drawn |
| `Testing.TypeOf` | `TypeRegistry.TypeOf(node)` over the language's declared type rules | the type a node has, as the framework writes it |
| `Testing.ScopeOf` | `IScopeProvider.ScopeOf(node, slot)` - the language's own providers over the default chain | which names a reference slot sees, in the order it ranks them |

All eight are document-level assertions: they need nothing from `Editing` but the two that are about input or
about naming - `Testing.Script`, which replays keystrokes, and the two node-addressed ones, which use `Finder` to
turn a text into a node. They reach that layer only through what it publishes, so the runner carries no second
idea of input or of search.

**How a node is named.** By the text the document shows at it, found through `Finder` - the same mechanism the
editor test's caret step uses, and the same question a user's find asks. Ordinal matching, first occurrence, and
`TestNodes` is the one implementation, so the caret step and the two assertions cannot drift apart. A test that
had to spell a node id would be unreadable, could not be reviewed in a diff, and would break the moment a fixture
was regenerated.

## The runner is a behaviour package, not a language package

This is the one packaging decision the plan makes rather than leaves open, and it follows from ADR 0019: a
runner reads findings, types, generators and the interpreter, so it must reference `Semantics` and `Editing`,
and a package that references them can never be what a language package depends on. `Projectional.Testing`
therefore sits in the behaviour band next to `Projectional.Checks` and `…Intentions` - a package a *host*
lists, not one a language does.

The test *language* travels in the same package, which is a deliberate compromise: splitting a language from
the only thing that can run it would buy a dependency rule and cost every consumer a second reference. If a
second runner ever appears (an app, a build task), that is the moment to split.

**Running.** `TestRunner.Run(LanguageProject, EvaluationRegistry?)` answers a `TestReport` - one row per test
with the assertion, the outcome and the actual-versus-expected text, so a failure reads as a diff. The second
argument exists because a language project carries no evaluation aspect: which rules belong to which language
is something the host knows and a `tests/` folder cannot state. An `evaluates` assertion run without them is a
failed row naming what it needed, so a host that forgets sees every such test fail rather than pass quietly,
and the other assertions still report.

Three doors onto it:

1. **xunit**, for CI today: one test method per shipped language project that runs its `tests/` folder and
   fails with the report. The suite therefore covers the samples and the declared pipeline with no new runner
   infrastructure, and every failure names the test document.
2. **The workbench**: **Run ▸ Run tests** runs the folder of the language the document in front belongs to -
   the project it was opened from - and answers one row per test in the output panel, the headline being the
   run's summary. Choosing a row opens the test document it names. A command rather than a panel of its own,
   because the panel already shows one line per row and resolves a click, and a test run is a list of things
   that happened.
3. **A host**, whenever it wants: the report is a value, not console output.

## What does not carry over, and why

| MPS | Ours |
| --- | --- |
| assertions are annotated onto the sample code (`@check`) | not possible without a common base concept: a slot must be declared over a concept key, and languages here share no root (the interface plan adds `Framework.Named`, not `BaseConcept`). A test names a document instead |
| a test model holds many tests | one test per document, like every other definition |
| caret markup inside the sample code | not possible for the same reason: a document under test stays a plain document, so a `Testing.Caret` step names the text to stand in front of instead |
| `TestInfo` with a project path, in-process versus out-of-process | not applicable: the runner is handed the project, and there is no second process to start |
| BaseLanguage code in a test, labels, utility methods | not applicable: the vocabulary is closed, so there is no code in a test |
| migration tests | no migration tooling exists yet (M6) - the assertion arrives with it |
| two-phase deletion, editor-state introspection | MPS-specific: they test the IDE's own furniture, and this editor owns its session |

## What is delivered

The folder, the language, the runner and the bridge, with five of the six assertions:

- **`Projectional.Testing`** is the behaviour package: `TestingLanguage` declares the concepts
  (`Testing.Test`, `Testing.Assertion`, `Testing.RoundTrips`, `Testing.Draws`, `Testing.Line`,
  `Testing.Findings`, `Testing.ExpectedFinding`, `Testing.Generates`, `Testing.Evaluates`),
  `Notation/TestingNotations` draws one sentence per assertion, and `TestingLowering` turns a written test
  into a `LanguageTest`.
- **`TestRunner.Run(language, rules)`** reads the language's `tests/` folder in ordinal file-name order and
  answers a `TestReport` - one `TestResult` per document, carrying the assertion and, on a failure,
  the actual-versus-expected text so the report reads as a diff. A language with no `tests/` folder
  answers an empty report. A document *under* test that is gone is a failed row naming it; a test
  document that does not lower is a broken suite and throws.
- **`Testing.RoundTrips`** asserts invariant 7 for the document it names: serialise, read back,
  serialise again, compare the text. A projection or persistence change that moves a byte fails the
  test that names the document.
- **`Testing.Draws`** asserts the document's notation at a stated width: the expected lines are child
  nodes of the assertion, so a projection change that moves a cell fails a named test with the two
  texts side by side.
- **`Testing.Findings`** asserts which findings the document's own constraints report, each row naming
  a code, a severity and optionally the slot it is about. A check catalogue is a C# package and not
  part of a language project, so the assertion covers the language's declared constraints; when checks
  become declarable it widens with them.
- **`Testing.Generates`** runs the generator the test names - `LanguageProject` may declare several, so
  "the" generator is a choice a test makes - and compares its text against the expected lines, one child
  node per line. An empty last line is how a test says the text ends with a line break. A name the
  language does not declare is a failed row listing the names it could have meant; a run that stopped
  mid-document reports the failure's message instead of half a file.
- **`Testing.Evaluates`** runs the interpreter over the rules the caller supplies and compares one text:
  the value's display when the run succeeded, the failure's message when it did not. One expected text
  rather than an answer-with-a-failure-flag, because a run produces exactly one of the two and the row
  shows which. The variables are read by `VariableInputs` - the same parser the editor's run box uses,
  moved down from `Projectional.Avalonia` into `Projectional.Language` so a run in a test and a run in the
  shell cannot disagree about what `1500` means - and a variables line that is not a list of
  `name=value` pairs is refused at lowering, like any other malformed slot.
- **The xunit bridge** is `LanguageTestsAsDataTests`: it runs the shipped sample's folder, so CI
  covers a language's own tests without new runner infrastructure. The sample's test documents are
  `samples/declared-pipeline/tests/round-trips.mps`, `draws.mps`, `reports.mps`, `generates.mps` and
  `script.mps`.
- **`Testing.Script`** is the editor test: an `EditorSession` over the document, with the language's own
  intentions registered, replaying a closed vocabulary of four steps - `Testing.Caret` (the caret goes in front of
  the first occurrence of a text), `Testing.Type` (the characters are typed at the caret), `Testing.Press` (a key
  is routed the way the control routes it) and `Testing.Invoke` (the offer whose id the step names is applied) -
  and then comparing what the document draws, exactly as `Testing.Draws` does. A step the editor refuses is a
  failed row naming the step and the reason, because a script that could not be replayed has compared nothing.

- **The workbench command** is **Run ▸ Run tests**: it resolves the project the document in front was opened
  from (its root's concept decides, exactly as the interpreter and the generators are decided) and runs that
  folder through the same `TestRunner` CI uses, so a row in the panel and a row in the build are one fact. The
  rows are the panel's own `RunRow`s - one per test, in the runner's order - and the summary is the headline
  rather than a row, because a row is what a click resolves. Choosing a row opens the test document: it is
  written in the test language, so it is opened with that language's project and notations, the way a language's
  own documents are opened with theirs. A document of one of the shell's compiled-in languages has no project and
  therefore no tests, which the entry says by being disabled with the reason on its tooltip. The rules the shell
  hands the runner are the tab's own - the registry it already resolved for Run - so a folder that ships an
  `evaluates` test reports it as failed while evaluation is not a declarable aspect, with the runner's own sentence
  saying that no rules were supplied.
- **`Testing.TypeOf`** pins the type the language's rules answer for the node the test names, written the way the
  framework writes it (which is also how a diagnostic quotes it). A language that declares no type rules answers
  `unknown` for everything, and a test that says so holds: it is asserting the framework's fallback, which is
  behaviour, not a rule the language could have got wrong.
- **`Testing.ScopeOf`** pins what a reference slot sees, one name per line, in the order the scope ranks them -
  nearest first, the same order a completion list offers. Pinning the order as well as the contents is deliberate:
  a language that starts ranking one name first has changed what its users can reference. The scope asked is the
  language's own registry over the default chain, which is what a session installs, so a language that declares no
  scopes is still testable.

**How a script names a place and an offer.** Neither is named by identity, because a test is written as data and
a node id is a GUID nobody can read:- a **place** is named by the text the document shows there, found through `Finder` - the same question a user's
  find asks. Matching is ordinal, so a test that says `release` cannot pass by landing on `Release`, and the
  caret goes in front of the occurrence so a step that types prefixes a value.
- an **offer** is named by its `Intention.Id`, which `Intention` documents as the stable, untranslated name a test
  can use without matching a label that may be localised or computed per caret.

Two consequences are worth stating because they are properties of the editor rather than of this framework's
tests. A place that shows **no text** - an empty slot, whose `<empty>` is notation rather than document - cannot be
named, exactly as a user cannot find it; a script reaches it with a key (`ControlEnd`, `Tab`), which is what a user
does too. And a language project carries **no typing rules aspect**, so a script cannot type into an empty slot to
create a concept there; filling a slot is `press Enter` or `invoke`, which is the path a user takes when the
language declares no rules either.

**No shipped language declares evaluation rules**, because evaluation is not a declarable aspect: rules are
written in C# against an `EvaluationRegistry`, and nothing in a language project states which registry belongs
to it. `Testing.Evaluates` is therefore proven where a host supplies one, in the suite's own temp-copied
`declared-workflow` folder, rather than by a document in a sample's `tests/` folder. When evaluation becomes
declarable, the assertion needs no change - only a sample that ships rules.

## Slices

| Slice | Content | Effort |
| --- | --- | --- |
| T1 | the `tests/` folder and the language (`Testing.Draws`, `Testing.Findings`, `Testing.RoundTrips`), `TestRunner`, the xunit bridge, and the sample language's first tests - **delivered** | 1 week |
| T2 | `Testing.Generates` and `Testing.Evaluates` (generators by name, variables from one line of `name=value` pairs, the way `RunInputs` already reads them) - **delivered** | 3-4 days |
| T3 | `Testing.Script`: the action vocabulary, caret placement, and the comparison after the run - the editor test - **delivered** | 1-2 weeks |
| T4 | node-addressed assertions (`Testing.TypeOf`, `Testing.ScopeOf`), which need a way for a test to name a node: the text the document shows at it, through the same `Finder` the editor test uses - **delivered** | open |
| T5 | the workbench command (**Run ▸ Run tests**), the report rows, and the click that opens the test - **delivered**. "Run on save" is not part of it: a language's tests are not cheap enough to run on every keystroke, and nothing has asked for it | 3-4 days |

## Non-goals

- **No code in a test.** A declarative assertion cannot branch, loop or compute; a test that needs that is a
  test of the framework, and the framework's own tests stay in xunit.
- **No performance testing.** Timing belongs to the profiling pass (M7) and its own harness.
- **No test discovery outside a language project.** The `tests/` folder belongs to a language; a project-wide
  test runner is a shell feature, not this one.
- **No assertion is silently relaxed.** A snapshot compares text; an assertion that tolerates a difference
  says so in its declaration, as MPS' match options do.

## What still needs a decision

- **Whether evaluation rules travel with a language project.** They do not, so `TestRunner.Run` takes them as
  an argument. That is the honest shape while rules are C# and a project cannot state which registry belongs
  to it; once evaluation joins `structure`, `editor` and the rest as a declarable aspect, the assertion reads
  them from the project and the parameter goes away. Until then a host that runs tests supplies the same
  registry it hands to its own Run command, which is also what keeps a test's run and a user's run identical.
- **Whether a script can assert that an edit was refused.** Today a refused step fails the row, because a script
  that could not be replayed has not compared anything - which means "this keystroke must be rejected, and the
  document must be unchanged" cannot be written. It is the editor test's version of a negative test, and the
  vocabulary has no word for it yet: an `Testing.Refused` step that names what must not happen would need the
  runner to keep both the refusal and its reason, so the choice is between a fifth step kind and a flag on each
  of the four.
- **How a test names its document.** A path relative to the language project is explicit and reviewable, and
  it is what MPS' `TestInfo` does for the project root - it stays. A reference to the model root would be
  stronger, but a reference slot needs a concept key and a root concept is the language's own - the common base
  concept this framework deliberately does not have, so it would wait on a language declaring a concept that
  *every* root conforms to (neither the interface plan nor anything else adds one today). The **node** half of
  this question is settled: a node is named by the text the document shows at it (see "How a node is named"), so
  no path syntax was needed after all. A richer answer still arrives with
  [smodel.md](smodel.md): a pointer or a query is what MPS uses to name a node in a test, and it would name a
  node the projection does not currently show - which a text-based name cannot.
- **What a snapshot compares.** Exact text is honest and brittle in one way: a change of measure or width
  rewrites every expectation. Renderer output is the thing being pinned, so exact text is right - but the
  report must show the diff rather than "not equal", or a width change reads as fifty failures.
- **Whether a language may ship tests without shipping a corpus.** A test that names a document needs the
  document; a document in `Corpus/` is a fixture. They can be the same files, and keeping one file for both
  purposes is the smaller answer.
- **Whether the first consumer is a sample or the framework's own language.** The declared pipeline is the
  natural first customer - it has a corpus and no assertions - and it is also the proof that a *user's* folder
  can be tested without C#.
