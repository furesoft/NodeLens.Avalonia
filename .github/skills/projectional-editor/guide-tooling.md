# Tests, C# language level, Windows pitfalls and verification habits

> Part of the `projectional-editor` skill. `SKILL.md` is the index and holds the invariants, the
> conventions and the build commands; this file is one layer of the rest, read on demand.

## How the tests are organised

`tests/Projectional.Core.Tests` is the main test project. It references `Core`,
`Language`, `Persistence`, `Projection`, `Editing` and - for the one pure type in it -
`Avalonia`. `TestLanguage.cs` (five concepts: `Test.Tag`,
`Test.Rule`, `Test.Body`, `Test.Step`, `Test.Note`) and `TestNotations.cs` (their notation, in
the same style a language author would write it) exist once. If a second test project
ever needs that language, promote it to a `Projectional.TestLanguage` library - do not copy
it. **Baseline is 2947 tests**; anything below that means tests were deleted.

`tests/Projectional.Toolkit.Tests` is the second test project and is deliberately unlike the first: it
references **only** `Projectional.Toolkit`, and declares its own two-concept language (`BoardLanguage`,
`BoardNotations`) instead of borrowing the fixture. Its four tests are the compile-time proof of
[ADR 0019](../../../docs/adr/0019-toolkit-is-the-language-surface.md) - a language that needed anything the
toolkit does not bring would not build. Do not add a reference to it to "make an assertion possible"; that
would delete the only test of the surface itself.

**A language's own tests are documents, not C#.** `Projectional.Testing` is the behaviour package
that declares the test language (`Testing.Test`, `Testing.Assertion`, `Testing.RoundTrips`,
`Testing.Draws`, `Testing.Findings`, `Testing.Generates`, `Testing.Evaluates`, `Testing.Script`,
`Testing.TypeOf`, `Testing.ScopeOf`) and its lowering, and `TestRunner.Run(language, rules)` runs a
language project's `tests/` folder - one document per assertion, answering a `TestReport` whose rows
carry the actual-versus-expected text so a failure reads as a diff. A language without the folder answers
an empty report; a document under test that is gone is a failed row; a test document that does not lower
is a broken suite and throws. The second argument is the language's `EvaluationRegistry`: a language
project carries no evaluation aspect, so an `evaluates` assertion is run with the rules the host supplies
and fails naming them when it is given none. `Testing.Script` is the editor test: it holds an
`EditorSession` and replays four steps - `Testing.Caret` (the caret goes in front of the first text the
document shows), `Testing.Type`, `Testing.Press` (both through `KeyRouter`, never a second idea of input)
and `Testing.Invoke` (an offer by its `Intention.Id`) - then compares the drawing; a place showing no text
is reached with a key, not by name, because `Finder` only finds text a user could change. **A node is
named the same way** - by the text the document shows at it, through `TestNodes`, shared by the caret step
and the two node-addressed assertions `Testing.TypeOf` and `Testing.ScopeOf`. `LanguageTestsAsDataTests` is
the bridge that runs the shipped samples' folders in CI. `LanguageProject.Open` does **not** read `tests/`,
because a language does not depend on its own tests. **Run ▸ Run tests** in the shell runs the open
document's language folder and lists one row per test in the output panel, a row opening the test document
it names. The plan is delivered in full; [language-tests.md](../../../docs/architecture/language-tests.md)
records the decisions it settled.

Two *real* languages sit next to the fixture and are the executable proof that the language
layer composes:

- `Projectional.Expressions` - an abstract `Expression` plus seven concrete concepts
  (`IntLiteral`, `BoolLiteral`, `StringLiteral`, `Name`, `Binary`, `Unary`, `Parenthesized`),
  the three literals declared `isOfferedInCompletion: false`.
  Four of the seven are what completion offers for an empty `condition`, and four is what a
  test asserting the list length should expect.
  A host borrows its declarations with `[.. ExpressionsLanguage.ConceptDeclarations, .. own]`;
  it never inherits from them. Operators are `PropertyType.Enum` symbols so the typed value
  *is* the notation.
- `Projectional.Samples` - the toy rules language (`Ruleset`, `Rule`, condition over
  `Expressions.Expression`) and `PayrollSample`, the shared document used by notation tests
  and (later) the standalone app.

Every expression notation must stay a **single `Line`**, because `ChildInline(slot)` refuses
anything else - a two-line expression concept silently breaks inline children.

- **The corpus is the file format.** `tests/Projectional.Core.Tests/Corpus/*.mps` are
  documents the writer produced once and that now have to load and save back to the same
  bytes. They are copied to the output directory by a `<None Update="Corpus\**\*.mps"
  CopyToOutputDirectory="PreserveNewest" />` item. **Never regenerate a corpus file to make
  a test pass** - a byte that moves is a wire-format change and needs an ADR. The files must
  stay LF, BOM-free and without a trailing newline.
- **Notation is tested as text, not pixels.** `Projectional.Projection/Snapshot/` holds
  `SnapshotGrid` (a character grid that refuses to draw past its edge rather than clipping)
  and `LayoutSnapshot.Render(model|node, notations, width)`, which lays the cells out and
  draws every text-bearing one where layout put it. So a notation test asserts a whole
  drawing: layout bugs show up as text. Both types are `internal` per ADR 0010, reached from
  tests through `InternalsVisibleTo`.
- **CI runs both platforms.** `.github/workflows/ci.yml` builds Release and tests with
  `--no-build` on `windows-latest` and `ubuntu-latest` (`fail-fast: false`), plus a
  Linux-only job that runs just the corpus so a format regression names the contract that
  broke. The SDK comes from `global.json`, so CI and this machine compile identically.
- **xunit.v3 on Microsoft.Testing.Platform**, so a test project is an executable.
  `--filter-class "*Name"` works; a comma-separated list does **not** ("wildcards may only
  be at the beginning and/or end"). Exit code 8 means no tests ran, 2 means failures - and
  on this console the run summary is printed in German.
- One type per file applies to tests too: `TemporaryDirectory.cs` is its own file, not a
  nested class.
- See `docs/architecture/testing.md` for what each layer is expected to prove.

## Language level (explicit user requirement)

**Always use the newest C# that the pinned SDK offers; never settle for the syntax the
project already happens to use.** `Directory.Build.props` sets `<LangVersion>latest</LangVersion>`,
which on SDK 10.0.403 resolves to **C# 14** (confirm with `dotnet build -v:diag` and look for
`_MaxAvailableLangVersion`). Do not pin a numbered `LangVersion` and do not enable `preview`
features - `latest` already means "the newest shipped version", and `preview` would make the
build depend on unreleased behaviour.

Features that are in active use and should be the default choice:

- **Collection expressions** `[]`, `[a, b]` for every list, array, span and dictionary creation.
- **`params ReadOnlySpan<T>`** instead of `params T[]` for variadic APIs that only read their
  arguments. See `PropertyType.Enum` and `EnumPropertyType` - the compiler passes string
  literals without allocating an array.
- **The `field` keyword** to remove a hand-written backing field, most often for a lazily built
  read-only view: `public IReadOnlyList<Node> Roots => field ??= _roots.AsReadOnly();`
  (`Model`, `Project`). Do not reach for `field` when the property is set from a constructor
  and the compiler would then report CS8618 - keep an explicit field there.
- **Primary constructors** on small types that only capture their arguments.
- **Pattern matching** (`is { } x`, property patterns, list patterns) instead of chains of
  null checks and casts.
- **`ArgumentNullException.ThrowIfNull`** and the other `ArgumentException` throw helpers.
- **`readonly record struct`** for small value types; `sealed class` otherwise.

If a newer compiler ships a feature that replaces something here, add it to this list in the
same commit that starts using it.

## Windows and PowerShell pitfalls in this environment

- **No heredocs.** `git commit -F -` with `<<'EOF'` produces a parser error. Use one `-m` per
  paragraph instead, or a single-quoted here-string:
  `@'` newline, content, newline, `'@ | ...`.
- **PowerShell 5.1 semantics.** No `&&`, `||`, `??`, `?.`. Chain with `;` and gate with
  `if ($?) { ... }`.
- **The repo path contains spaces.** Quote every path.
- **Line endings drift.** `core.autocrlf=true` is set globally, and the file-writing tools plus
  `dotnet new` emit CRLF. `.gitattributes` forces `eol=lf` in the index, so commits are
  correct even when the working tree is not. To renormalize a dirty working tree, **delete the
  tracked files and run `git checkout -- .`** - `git reset --hard` and `git checkout-index -a -f`
  both skip files whose stat looks clean. Diagnose with `git ls-files --eol`.
- **Write LF text explicitly** when authoring files:
  ```powershell
  [IO.File]::WriteAllText($path, ($text -replace "`r`n", "`n"), (New-Object System.Text.UTF8Encoding($false)))
  ```
- **A member named like an inherited Avalonia member is a compile error, not a shadow.**
  `Theme` on a `Control` collides with `StyledElement.Theme` (CS0108), so the palette property
  had to be called `Palette` and the type `SyntaxPalette` rather than `SyntaxTheme`. Before
  naming a new public property, check the base class first - renaming later touches the tests
  and the prose.
- **Non-ASCII in tool output is unreliable.** The console mangles UTF-8, so an em dash typed
  into a file can be silently stored as three characters (U+00E2, U+20AC, U+201D) instead of
  the single U+2014 that was meant, and nothing shows the difference. Never diagnose encoding
  by eye: read the character codes
  (`$t.Substring($i, $n).ToCharArray() | ForEach-Object { '{0:X4}' -f [int]$_ }`) and expect
  `2014` for an em dash. After authoring prose, scan the tree for that triple, building the
  pattern from code points so the scan never matches itself:
  ```powershell
  $bad = [string][char]0x00E2 + [char]0x20AC
  Get-ChildItem -Recurse -File -Include *.cs,*.md,*.csproj,*.axaml,*.yml src,tests,docs,.github |
    Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } |
    Where-Object { [IO.File]::ReadAllText($_.FullName).Contains($bad) }
  ```
- **`git show file > out` writes UTF-16 in PS 5.1**, so byte-level checks on committed files
  are useless through it. Use `runInTerminal` for a clean `git diff`, or check the working
  tree instead.
- **A local function named after a type shadows it.** A probe helper called `Key(...)` broke
  every later `Key.Right` with CS0119. Name helpers `Combo`, `Press`, `Chord`.
- **`Stop-Process -Id` needs a literal PID** in this environment; a `$pid` variable makes the
  whole command be refused. Design one-shot probes so they close their own window and exit.


## Verification habits

- Markdown links **and anchors** must both resolve. Anchors break silently when a heading is
  renamed, so validate `#fragment` targets against generated heading slugs, not just file paths.
- The persistence test is byte equality after a full round trip, not "it deserialized".
- Whenever a change touches the model, check the seven invariants in
  `docs/architecture/model.md` still hold: unique ID per project, exactly one parent or the
  root, child slot conforms to the concept, no reference into the referencing node's own
  subtree unless allowed, removing a node clears incoming references, every property value
  parses, and a concept only stands where its `Applicability` allows. They are enforced on every
  mutation (`ModelInvariantException`), not merely asserted in debug builds, so a green test run
  means the checked ones hold - the structural ones (invariants 1 and 2) still need a thought.
- **A reference may cross a model boundary but not a project one.** `Node.SetReference` compares
  `Project`, not `Model`: two documents of one project share the node index, and the scope provider
  already offers their nodes by name. Do not "restore" a model-level check - it is what made a
  cross-document foreign key impossible until the SQL schema example found it.

### Runtime probe: how UI code is verified

There is no `Avalonia.Headless` package in the cache and no test harness for the control, but
**a real Avalonia window runs on this machine**. So the verification technique is a throwaway
project *outside* the repository:

1. Create `%TEMP%\probe` with a `WinExe` `net10.0` project referencing `Projectional.Avalonia`
   and `Projectional.Samples` by absolute path (never add the probe to the solution).
2. Host the control in a `Window`, and in `Opened` run an `async` sequence: raise inputs, then
   `await Task.Delay(40)` a few times to let layout and render settle between steps.
3. Read state back through the public surface (`EditorSession.Selection`, `.Layout.Size`,
   `EditorFrame.Build(...)`, `OffsetX/Y`) and rasterise proof with
   `new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96))` + `bitmap.Render(control)`
   + `bitmap.Save(stream)` (the `Save(string)` overload is obsolete: CS0618).
4. Append findings to a `List<string>`, write it to a log file, then `Close()` the window so the
   process exits on its own - a probe that stays open blocks the shell call.
5. Look at the PNGs. Geometry lines can look right while the picture is wrong.

Facts that came out of doing this, and cost time to rediscover:

- **Key and text events are public and raisable**: `new KeyEventArgs { RoutedEvent =
  InputElement.KeyDownEvent, Key = ..., KeyModifiers = ..., Source = control }` and
  `new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "..." }`, then
  `control.RaiseEvent(args)`, then read `args.Handled`.
- **The wheel is raisable too**, even though a press is not. `PointerPressedEventArgs` has no
  public constructor, but `PointerWheelEventArgs` does:
  `new PointerWheelEventArgs(control, new Pointer(1, PointerType.Mouse, true), control,
  new Point(20, 20), 0, new PointerPointProperties(), KeyModifiers.None,
  new Vector(0, -1))` - `-1` is one notch down, `Shift` makes it horizontal, and `Handled`
  afterwards is the assertion. That is how `ScrollTo`'s "did the offset move" answer is tested
  from the outside.
- **`RaiseEvent` bubbles to ancestors**, so `Handled` can be `true` because `TopLevel` or a
  parent handled it, not because the control did. To isolate the control, raise the same event
  at a control that is in **no** visual tree.
- **A pointer move is raisable as a plain `PointerEventArgs`** raised with
  `InputElement.PointerMovedEvent` - there is no `PointerMovedEventArgs` in 12.1.6 - passing the
  control as `rootVisual` so `GetPosition` answers the point you gave. Reading
  `control.Cursor.ToString()` afterwards (`"Hand"`, `"Arrow"`, `"Ibeam"`) is how the hover shapes
  are asserted.
- **A local method named after a type breaks the file**: `void Key(Key key, ...)` makes every
  later `Key.Left` a method group (CS0119). Name the helper `Combo` or `Press`.
- **Widgets are laid out lazily**, so a probe must `await Task.Delay` *after* the window opens
  before `Bounds` has a real size; `InvalidateVisual()` alone does not run a render pass in a
  probe - `RenderTargetBitmap.Render` forces one.
- **Attach the `Opened` handler before `base.OnFrameworkInitializationCompleted()`.**
  `Application.OnFrameworkInitializationCompleted` is what shows `MainWindow`, so a handler
  subscribed after the base call never fires and the probe exits having written nothing. The
  shape is `Application.Prepare(...)` before the base call, window content assigned there too,
  and the whole sequence wrapped in `try`/`catch` that logs the exception - a silent empty log
  means the handler did not run, not that the assertion failed.
- **Sampling a glyph colour needs the extreme pixel.** The centre of a glyph is
  antialiased against the background, so a mid-glyph sample returns a blend that matches
  neither the palette nor the background. Per run, take the pixel **farthest from the
  background colour** in that run's rect; that pixel is the brush. This is also how the
  "enumeration symbols are keywords" consequence was found and confirmed: `>` and `Accept`
  came back blue, not punctuation-grey and not identifier-black.


### Avalonia 12.1.6 facts worth having

- `DrawingContext.DrawText(FormattedText, Point)`, `FillRectangle(IBrush, Rect, float)`,
  `FormattedText(string, CultureInfo, FlowDirection, Typeface, double, IBrush)` with
  `WidthIncludingTrailingWhitespace`, `Height`, `MaxTextWidth`, `Trimming`.
- `Control` has **no** `Background`/`Foreground` (only `TemplatedControl`/`TextBlock` do), so
  declaring them on the editor control is legal and does not hide anything.
- **Namespace trap**: inside `namespace Projectional.Avalonia`, an unqualified
  `Avalonia.Media.X` resolves `Avalonia` to `Projectional.Avalonia` and fails to compile. Keep
  `using` directives at the top of the file and use unqualified names in the body.
- `FontFamily.Default`, `Typeface(FontFamily, FontStyle, FontWeight, FontStretch)`,
  `Brushes.Black/White`, `Color.FromArgb`.
- **`FormattedText` has no `textDecorations` constructor parameter** in 12.1.6. Underline and
  strikethrough come from `formattedText.SetTextDecorations(TextDecorationCollection)` after
  construction, which is what `AvaloniaTextMeasure.DecorationsOf` does. Weight and style, by
  contrast, *are* constructor state (`Typeface`) - which is exactly why emphasis has to reach
  the measure and cannot be a paint-time add-on.

