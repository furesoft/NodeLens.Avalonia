# Implemented

What is finished, where it lives, and what proves it. This file is the record of delivery;
[`roadmap.md`](roadmap.md) keeps only what is left.

**The rule: a feature moves here in the same commit that finishes it.** A row is added when the
acceptance gate it belongs to passes, not when the code compiles. If a row is here, the feature is
done and a later change must not quietly undo it; if a feature is not here, it is not done, however
much of it exists. The other half of the rule: **a milestone whose gate has passed is removed from the
roadmap**, so that file never lists work that is done.

A row is written as **feature | where it lives | proof**. The proof is the test file that would fail
if the feature were removed, or - where a test cannot reach it - the verification that was actually
performed. "Proof" is deliberately not "it builds": a milestone is done when its gate passes.

Verified against the tree that added this file: `main` at `610d813`, **1606 tests green**, build
clean on Windows and Linux.

## M0 - Foundation (complete)

| Feature | Where it lives | Proof |
| --- | --- | --- |
| Solution skeleton, one project per layer, two test projects | `src/`, `tests/`, `Projectional.slnx` | `LayerDependencyTests` (arrows point downward only) |
| Node, model and project containers; the six model invariants enforced on every mutation | `Projectional.Core/Model/` | `ModelTests`, `NodeTests`, `ProjectTests` |
| Stable GUID identity per node, never renumbered | `Projectional.Core/Identity/` | `NodeIdTests`, `ModelIdTests` |
| References store target id plus a `resolveInfo` fallback, so an unresolved reference degrades | `Projectional.Core/Model/` | `NodeReferenceTests` |
| Change journal, transactions, rollback, coalescing | `Projectional.Core/Changes/` | `ChangeEventTests`, `ModelTransactionTests` |
| Undo/redo driven by the journal, with coalescing keys | `Projectional.Core/Undo/` | `UndoRedoTests` |
| Byte-stable XML persistence, atomic save, never drop data on load | `Projectional.Persistence/` | `ModelXmlTests`, `ModelFileTests`, `CorpusTests` over four corpus documents |
| Language registries (concepts, notation, typing rules) that validate at registration | `Projectional.Language/` | `ConceptRegistryTests`, `NotationRegistryTests`, `TypingRuleRegistryTests` |
| Property types, including enumerations that name their own values | `Projectional.Language/` | `PropertyTypeTests` |
| CI on Windows and Linux, plus a Linux-only corpus job | `.github/workflows/ci.yml` | the workflow itself |

The stored form and the test harness are described in [persistence.md](architecture/persistence.md#what-m0-implements)
and [testing.md](architecture/testing.md).

## M1 - Projection and editing (complete)

| Feature | Where it lives | Proof |
| --- | --- | --- |
| Runtime editor tree: cells, addresses, one tree per node | `Projectional.Projection/Tree/` | `EditorTreeTests`, `CellTextTests` |
| Measure and arrange layout, hanging continuation, tables | `Projectional.Projection/Layout/` | `CellLayoutTests`, `LayoutRectTests`, `LayoutViewportTests` |
| Headless snapshot renderer (notation tested as text, not pixels) | `Projectional.Projection/Snapshot/` | `LayoutSnapshotTests`, `SnapshotGridTests` |
| Edit-action layer: caret navigation, selection, key routing | `Projectional.Editing/` | `CaretNavigationTests`, `KeyRouterTests`, `EditorKeyMapTests`, `EditActionTests`, `HitTestTests` |
| Insert, delete, duplicate, wrap, set property, set node property | `Projectional.Editing/` | `InsertChildActionTests`, `InsertSiblingActionTests`, `DeleteNodeActionTests`, `DuplicateNodeActionTests`, `WrapInActionTests`, `SetPropertyActionTests`, `SetNodePropertyActionTests` |
| Node moves (Alt+Up/Down) | `NodeMove`, `MoveNodeAction` | `NodeMoveTests` |
| Text clipboard: select all, copy, cut, paste through `ITextClipboard` | `Projectional.Editing/Clipboard/` | `SelectAllActionTests`, `CopyActionTests`, `CutActionTests`, `PasteActionTests`, `InMemoryClipboardTests` |
| Selection units: word and value (double click) | `SelectWordAction`, `SelectValueAction` | `SelectWordActionTests`, `SelectValueActionTests`, `SelectionRectsTests` |
| Cross-cell selection delete | `DeleteSelectionAction` | `DeleteSelectionActionTests` |
| Completion: concepts for an empty slot, values for an enumerated property, filtering, icons | `Projectional.Editing/Completion/` | `CompletionListTests`, `CompletionItemTests`, `CompletionActionTests` |
| Syntax highlighting from meaning, host-swappable palette | `TextStyleKind`, `CellStyler`, `SyntaxPalette` | `CellStylerTests`, `SyntaxPaletteTests`, `SyntaxThemeTests` |
| Emphasis (bold, italic, underline, strikethrough) that reaches the measure | `TextEmphasis`, `ICellEmphasis` | `TextEmphasisLayoutTests`, `EditorTreeEmphasisTests`, `RejectedRuleEmphasisTests` |
| Context menu described by the editing layer | `EditorMenu`, `EditorMenuPresenter` | `EditorMenuTests` |
| Markers with hover tooltips | `Projectional.Editing/Markers/` | `MarkerSetTests`, `MarkerGeometryTests`, `MarkerRunsTests`, `MarkerCellsTests`, `MarkerHitTestTests`, `MarkerTipTests` |
| Concept and property descriptions on hover | `ConceptTip`, `PropertyTip` | `ConceptTipTests`, `PropertyTipTests` |
| Notation variants, chosen per concept as view state | `NotationChoice`, ADR 0012 | `NotationChoiceTests`, `NotationVariantsTests`, `NotationAlternativesTests`, `NotationVariantProjectionTests` |
| Tables as a variant | `TableNotationCell`, `TableCell` | `TableNotationTests`, `TableProjectionTests`, `SampleTableVariantTests` |
| Folding | `Foldable`, `FoldMarks`, `CollapsedCell` | `FoldableTests`, `FoldMarksTests`, `FoldedMarkerTests`, `CollapsedSessionTests`, `CollapsedNodeSetTests`, `CollapsedCellTests` |
| Multi-line cells: layout, caret, editing | `Projectional.Projection`, `Projectional.Editing` | `MultiLineTextTests`, `MultiLineCaretTests`, `MultiLineEditingTests` |
| Line numbers and font size | `ProjectionalEditorControl` | `LineNumbersTests` |
| Logical scrolling: bars, wheel, page keys | `ILogicalScrollable` | `ScrollRangeTests`, `CaretScrollTests` |
| Blinking caret | `CaretBlink` | `CaretBlinkTests`, `CaretBoundsTests` |
| The embeddable Avalonia control: paints glyphs, hit-tests, routes keys | `Projectional.Avalonia/` | `EditorFrameTests`, `EditorSessionMeasureTests` |
| `Expressions` language: abstract `Expression` plus seven concrete concepts | `Projectional.Expressions/` | `ExpressionsLanguageTests` |
| `Samples`: the toy rules language and the shared payroll document | `Projectional.Samples/` | `RulesLanguageTests`, `RulesLanguageEditingTests` |
| Standalone shell: tabs, menu bar, status bar, open/save, dirty prompt, theme | `Projectional.Workbench/` | `DocumentTabTests`, `DocumentSetTests`, `DocumentFileTests` |

## M2 - Languages and the standalone editor (complete)

| Feature | Where it lives | Proof |
| --- | --- | --- |
| Structure language: nine concepts whose nodes *are* a language definition | `Projectional.Structures/` | `StructureLoweringTests` |
| Editor language: eighteen concepts whose nodes *are* a notation, including a table and its columns | `Projectional.Editors/` | `EditorLoweringTests` |
| Both definition languages drawn in MPS' shape (header, options, one section per slot kind, `<< ... >>` for an empty section) | `Structures/Notation/StructureNotations.cs`, `Editors/Rendering/EditorNotations.cs` | the notations themselves; a definition document opens and draws in the app |
| Parity harness: the same language as C# and as a model must agree | `LanguageParityTests`, `ParityHarness`, `DeclaredTestLanguage` | `LanguageParityTests` (registry dump, snapshot, findings) |
| `Projectional.Toolkit`: the third public surface, so a language can be built without the editor | `Projectional.Toolkit/`, ADR 0019 | `tests/Projectional.Toolkit.Tests` (references the toolkit only) |
| `dotnet new projectional-language` template | `templates/projectional-language/` | verified end to end: install, generate, build, test |
| A language project is a folder (`structure.mps` + optional `editor.mps`) | `Projectional.Languages/` | `LanguageProjectTests` |
| The app composes the sample language with both definition languages and opens a language folder as a document | `WorkbenchLanguage`, File > Open language | `WorkbenchLanguageTests` |
| Model browser (walks the model, not the projection) | `Projectional.Workbench/Outline/` | `OutlineTests` |
| Problems panel (document order, jump target, stale findings last) | `Projectional.Workbench/Problems/` | `ProblemListTests` |
| Find: walks caret places in property cells, ordinal, skips the current match | `Projectional.Editing/Find/` | `FinderTests`, `FindNextActionTests` |
| Replace one and replace all (one transaction, right to left, refused values skipped) | `Projectional.Editing/Find/` | `ReplaceMatchActionTests`, `ReplaceAllActionTests` |
| Recent files (dedupe, order, cap, silent store failures) | `Projectional.Workbench/Documents/` | `RecentFilesTests`, `TextRecentFilesStoreTests` |
| Table add-row affordance: an empty table draws its header and one fillable `AddRowCell` row | `Projection/Tree/AddRowCell.cs`, `TableCell` | `TableProjectionTests`, `TableNotationTests`, `SampleTableVariantTests` |
| Table column widths, dragged and reset, as view state | `Editing/Tables/ColumnWidths.cs`, `ColumnResize`, `IColumnWidths` | `ColumnWidthsTests`, `ColumnResizeTests` |
| Self-contained single file, with a `--self-check` the bundle runs on itself | `Projectional.Workbench.csproj`, `SelfCheck.cs`, `ci.yml` | the `single-file` CI job: publish, assert one file, run the check |
| **The M2 gate**: a DSL that exists only as data - `structure.mps`, `editor.mps` and a document | `samples/declared-pipeline/` | `DeclaredSampleLanguageTests` (loads, draws as a table, edits, saves, reloads byte-identically, hands the folder to the shell) |

## M3 - References, triggers and intentions (partly, landed early)

| Feature | Where it lives | Proof |
| --- | --- | --- |
| Reference cells with three states (unset, unresolved, resolved) | `ReferenceCell`, `ReferenceState` | `NodeReferenceTests`, the `references.mps` corpus document |
| Scope model: a chain of levels, first match wins, ambiguity as a value | `Projectional.Semantics/Scopes/` | `ScopeTests`, `ScopeChainTests` |
| The default chain: local, ancestors, model, project; only named nodes, never the node itself | `DefaultScopeProvider` | `DefaultScopeProviderTests` |
| Scope-driven completion: a reference offers the scope's entries and accepting re-resolves the name | `CompletionSource.References`, `CompletionKind.Reference` | `ScopeCompletionTests` |
| Concept triggers: a typed character at the edge of a node creates and wraps | `ConceptTrigger`, `TriggerAnchors` | `ConceptTriggerTests`, `ConceptRegistryTriggerTests`, `TypeTextTriggerTests` |
| Intention model, per-caret list, apply path, popup, menu split | `Projectional.Editing/Intentions/` | `IntentionTests`, `IntentionListTests`, `IntentionAvailabilityTests`, `EditorSessionIntentionTests`, `OfferKeysTests` |
| Intention preview: sandbox copy, diff, marks, hover ladder, ghost frame | `Projectional.Editing/Preview/`, ADR 0017 | `PreviewSandboxTests`, `ModelCopyTests`, `ModelDiffTests`, `PreviewMarksTests`, `PreviewDelayTests`, `PreviewStateTests`, `GhostFrameTests`, `PreviewBadgeTests`, `OfferPreviewTests`, `SamplePreviewTests` |
| Standard intention catalogue (replace, sort, select node, select slot, wrap, negate, accept rule, make abstract/concrete) | `Projectional.Intentions/` | `StandardIntentionsTests`, `ExpressionsIntentionsTests`, `RulesIntentionsTests`, `StructureIntentionsTests` |
| The app registers the standard catalogue once, at open | `MainWindow.RegisterIntentions` | `StandardIntentionsTests` (one row per operation) |

Not done in M3: scopes and scope-driven completion.

## M4 - Checks and quick fixes (partly, landed early)

| Feature | Where it lives | Proof |
| --- | --- | --- |
| Diagnostics and the fix contract: a fix produces an edit, never mutates | `Projectional.Semantics/`, `Projectional.Editing/Fixes/` | `DiagnosticFixTests`, `FixSetTests`, `FixAvailabilityTests`, `EditProgramTests` |
| Quick fixes at the caret, the bulb, the offer list | `QuickFixes`, `OfferList` | `QuickFixesTests`, `QuickFixBulbTests`, `EditorSessionFixTests` |
| Standard check catalogue as a package (`STD001`, `STD003`, `STD010`, `STD040`) | `Projectional.Checks/` | `StandardChecksTests`, `CheckSetTests`, `PlaceholderNameCheckTests`, `SurroundingWhitespaceCheckTests`, `EmptyChildSlotCheckTests`, `BinaryGroupingCheckTests` |
| The app's own checks (default names, duplicate rule names) | `Projectional.Workbench/Checks/` | `DefaultNamesTests`, `DuplicateRuleNamesTests` |

Not done in M4: types, text generation, the interpreter.

## M5 - Self-hosting (started)

| Feature | Where it lives | Proof |
| --- | --- | --- |
| A language definition is a model like any other: it opens, edits, saves and undoes in the same editor | `Projectional.Structures`, `Projectional.Editors`, `WorkbenchLanguage` | `StructureLoweringTests`, `EditorLoweringTests`, `WorkbenchLanguageTests` |

Not done in M5: stage C (a DSL defined entirely in project files), language export, live preview,
bootstrap, the language browser.

## M6 and M7

Nothing is delivered. M6 (the shell as an embeddable, language-locked component) and M7
(installers, file association) are untouched; the app is still `MainWindow` alone, and
`docs/architecture/standalone.md` describes the extraction M6 owns.

## Cross-cutting

| Feature | Where it lives | Proof |
| --- | --- | --- |
| 19 accepted architecture decisions, frozen | `docs/adr/` | the files; a change needs a new ADR that supersedes |
| Architecture documents per layer and per planned feature | `docs/architecture/` | the files |
| Two test projects, xunit.v3 on Microsoft.Testing.Platform | `tests/` | 1606 tests |
| CI: Release build and tests on Windows and Linux, corpus job on Linux | `.github/workflows/ci.yml` | the workflow |
| Byte-identical round trip over the whole corpus | `tests/Projectional.Core.Tests/Corpus/` | `CorpusTests` |

## What is not here

Everything else. `roadmap.md` is the list of what is left, milestone by milestone, with the
acceptance gate each one has to pass.