# Testing

A projectional editor has two properties that are easy to break without noticing: the
stored form of a model is a contract with every file a user already saved, and the layout
of a projection is a contract with every test written against it. Both are text, so both
are asserted as text. That is what the harness in this repository is for.

## What each layer has to prove

| Layer | The test shows | Why it cannot be checked any other way |
| --- | --- | --- |
| Model mutations | every invariant in [model.md](model.md) holds after each edit, and a refused edit changes nothing | a partially applied edit leaves a tree that is still readable but no longer valid |
| Undo and redo | the sequence of states is exactly reversible and the journal coalesces as documented | "looks right after undo" hides lost redo stacks |
| Persistence | a stored document reads and writes back to the same bytes | "it deserialized" accepts a file that changes on every save |
| Projection | a model renders to a fixed text grid, and cell layout snaps to the grid | pixels need a window, a font and a screenshot to compare |
| Semantics | constraints report their severity and scopes resolve to the nodes they name | a checker that silently finds nothing always passes |

Assertions on model state go through the public model API. Assertions on the stored form
and on layout are byte or text equality against a constant in the test, so a change in
behaviour shows up as a diff in review rather than as a weaker assertion.

## The round-trip corpus

`tests/Projectional.Core.Tests/Corpus/` holds `.mps` documents that are the file format
written down: an empty model, a rule tree with properties, children and a cross-root
reference, a rule extending another rule with a self reference, and a model whose names
and values need escaping. The files are copied to the test output directory and every one
of them must load and save back to the identical bytes.

Two rules keep the corpus honest:

- **The files were produced by the writer, not typed by hand.** They record what the code
  does, not what someone believed it does. `ModelXmlTests` covers the other direction with
  a document written by hand, so a writer that drifts is caught even if the corpus is
  regenerated.
- **A file is never regenerated to make a test pass.** A byte that moves is a change to
  the wire format; that needs a new ADR, a migration story for existing files, and the
  corpus edited in the same commit that justifies it.

The corpus also pins two properties that a single-document test cannot: the files must be
plain LF text without a byte order mark and without a trailing newline, because a byte
that survives a checkout on one platform and not the other turns a stable format into one
that is stable per machine.

## The snapshot harness

Notation is tested by rendering the model to text and comparing the text. The harness for
that is `Projectional.Projection/Snapshot/`:

- `SnapshotGrid` is a fixed grid of characters. It refuses to draw past its right edge
  instead of clipping, because a layout test that silently loses the end of a line passes
  for the wrong reason. `ToString` trims trailing blanks and drops trailing blank lines, so
  a snapshot shows what was drawn rather than how much room was reserved.
- `LayoutSnapshot.Render(model, notations, width)` renders every root of a model, one below
  the other, and `Render(node, notations, width)` renders a single node. Both build the
  editor tree, lay it out with `MonospaceTextMeasure`, and draw each cell that has text at
  the place layout put it - which means a snapshot failure is a layout failure, not an
  artefact of the harness.
- Text wider than `width` is cut rather than wrapped, because the width was asked for.
  Roots are stacked with no gap between them: vertical spacing is something a notation
  decides, so the renderer does not invent any.
- An empty slot draws the `EmptyText` of its `ChildCell` or `CollectionCell`. Layout has
  already reserved room for that text, and a projection that shows nothing at all would
  hide the fact that the slot is there to be edited into.
- One layout unit is one character and one row, which is exactly what
  `MonospaceTextMeasure` promises. The renderer knows nothing else about units, so it stays
  a consumer of layout rather than a second layout implementation.

Both types are `internal` with `InternalsVisibleTo` to the test projects, per
[ADR 0010](../adr/0010-public-api-surface.md): only `Core` and `Avalonia` are public, and a
test hook does not belong in an embedding API.

## Continuous integration

`.github/workflows/ci.yml` has two jobs:

- **build and test** on `windows-latest` and `ubuntu-latest`, with `fail-fast: false` so a
  failure on one platform does not hide the state of the other. It restores, builds in
  Release, and tests with `--no-build`. Running both platforms is deliberate: this project
  handles paths, line endings and file replacement, and those are exactly the places where
  a change works on the machine it was written on.
- **byte-stability** runs only the corpus tests, on Linux. It exists so that a format
  regression names the contract that broke instead of hiding inside a full suite.

The SDK comes from `global.json`, so CI and a developer machine build with the same
compiler.

## Test code conventions

- **One type per file holds here too.** A helper such as `TemporaryDirectory` gets its own
  file rather than being nested in the test class that uses it first.
- Tests live in `tests/Projectional.Core.Tests`, which references `Core`, `Persistence` and
  `Projection` so that `TestLanguage` - the five-concept language every model test is
  written in - exists once. If a second test project ever needs it, promote it to a
  `Projectional.TestLanguage` library instead of copying it.
- Test names state the rule, not the method: `AFailedLoadLeavesNothingBehind`, not
  `LoadThrows`.
- A test that needs a file system uses `TemporaryDirectory`, which deletes itself.

## Not covered yet

- **UI tests.** The Avalonia control has no rendering tests; the text grid is the seam that
  makes them unnecessary until real layout exists.
- **Performance.** No allocation or latency budget is asserted anywhere, and ADR 0009 makes
  virtualization a design constraint rather than a measured one.
- **The public API baseline** required by ADR 0010 is not built yet.
- **A language testing itself.** Every test here is C# over one fixture, and the aspects a
  language author can use - checks, snapshot, generator, interpreter - are internal by design
  ([ADR 0019](../adr/0019-toolkit-is-the-language-surface.md)), so a DSL in a folder has a
  round-trip corpus and no assertions. The plan is
  [language-tests.md](language-tests.md): a `tests/` folder of declarative assertions and a
  runner in the behaviour band.
