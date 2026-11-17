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
| Text clipboard: select all, copy, cut, paste through `IEditorClipboard` | `Projectional.Editing/Clipboard/` | `SelectAllActionTests`, `CopyActionTests`, `CutActionTests`, `PasteActionTests`, `InMemoryClipboardTests` |
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

## M3 - References, triggers and intentions (complete)

| Feature | Where it lives | Proof |
| --- | --- | --- |
| Reference cells with three states (unset, unresolved, resolved) | `ReferenceCell`, `ReferenceState` | `NodeReferenceTests`, the `references.mps` corpus document |
| Scope model: a chain of levels, first match wins, ambiguity as a value | `Projectional.Semantics/Scopes/` | `ScopeTests`, `ScopeChainTests` |
| The default chain: local, ancestors, model, project; only named nodes, never the node itself | `DefaultScopeProvider` | `DefaultScopeProviderTests` |
| Scope-driven completion: a reference offers the scope's entries and accepting re-resolves the name | `CompletionSource.References`, `CompletionKind.Reference` | `ScopeCompletionTests` |
| Typing a name into a reference cell resolves it through the same scope; ambiguous is refused with a reason | `TypeTextAction.ResolveReference` | `ScopeTypingTests` |
| Per-language scope overrides: a concept declares a provider, inherited by the concepts below it | `ScopeProviderRegistry` | `ScopeProviderRegistryTests` |
| Ambiguity diagnostics: a reference whose name no longer identifies its target is reported on the reference slot | `AmbiguousReferenceConstraint`, `ScopeConstraints.All` | `AmbiguousReferenceConstraintTests` |
| Completing a filled slot: the list offers what could replace the child, accepting replaces it in one undo step | `CompletionSlot.Replace`, `CompletionSource.Replacements`, `ReplacementConcepts` | `CompletionActionTests` |
| Wrapping a selection: the range entry, its search, and the model's run form of the wrap | `StandardIntentions.WrapSelectionPrefix`, `WrapperConcepts.Of(nodes, ...)`, `Model.Wrap(IReadOnlyList<Node>, ...)`, `IntentionContext.SelectedNodes` | `StandardIntentionsTests`, `WrapInActionTests` |
| Structural clipboard: copy, cut and paste of a subtree with new identities, rebound references and one undo step; one adapter shared by every tab | `ModelFragment`, `IEditorClipboard`, `CopySelectionAction`, `CutSelectionAction`, `PasteFragmentAction`, `EditContext.InsertFragment`, `MainWindow._clipboard` | `ModelFragmentTests`, `CopySelectionActionTests`, `CutSelectionActionTests`, `PasteFragmentActionTests`, `KeyRouterTests` |
| Concept triggers: a typed character at the edge of a node creates and wraps | `ConceptTrigger`, `TriggerAnchors` | `ConceptTriggerTests`, `ConceptRegistryTriggerTests`, `TypeTextTriggerTests` |
| Intention model, per-caret list (MPS' transformation menu), apply path, popup, menu split | `Projectional.Editing/Intentions/` | `IntentionTests`, `IntentionListTests`, `IntentionAvailabilityTests`, `EditorSessionIntentionTests`, `OfferKeysTests` |
| Intention preview: sandbox copy, diff, marks, hover ladder, ghost frame | `Projectional.Editing/Preview/`, ADR 0017 | `PreviewSandboxTests`, `ModelCopyTests`, `ModelDiffTests`, `PreviewMarksTests`, `PreviewDelayTests`, `PreviewStateTests`, `GhostFrameTests`, `PreviewBadgeTests`, `OfferPreviewTests`, `SamplePreviewTests` |
| Standard intention catalogue (replace, sort, select node, select slot, wrap, negate, accept rule, make abstract/concrete) | `Projectional.Intentions/` | `StandardIntentionsTests`, `ExpressionsIntentionsTests`, `RulesIntentionsTests`, `StructureIntentionsTests` |
| The app registers the standard catalogue once, at open | `MainWindow.RegisterIntentions` | `StandardIntentionsTests` (one row per operation) |

The gate's three clauses hold: a reference into another model resolves, a rename leaves every model intact
because the reference stores `resolveInfo`, and wrapping a selection is one undo step. The last one is proven
on a wrapper that holds a list (`GroupLanguage` in the tests) rather than on an `if`, because no shipped
language has a statement body yet - the operation is generic, so the `if` arrives with the code DSL in M6.

## M4 - Checks and quick fixes (partly, landed early)

| Feature | Where it lives | Proof |
| --- | --- | --- |
| Diagnostics and the fix contract: a fix produces an edit, never mutates | `Projectional.Semantics/`, `Projectional.Editing/Fixes/` | `DiagnosticFixTests`, `FixSetTests`, `FixAvailabilityTests`, `EditProgramTests` |
| Constraint engine: a language's rules as an aspect, run over a document or one subtree | `Projectional.Semantics/Constraints/` | `ConstraintEngineTests` |
| The sample language's own constraint (a rule needs a condition), merged into the app's findings | `SampleConstraints`, `MainWindow.PublishFindings` | `SampleConstraintsTests` |
| Type model: `IType`, `PrimitiveType`, `UnknownType`, the `TypeFor` aspect and a registry inherited by sub-concepts | `Projectional.Language/Types/` | `TypeRegistryTests` |
| Structural types: record (compared by name), list, optional (accepts its inner type, nesting not collapsed) | `RecordType`, `ListType`, `OptionalType` | `StructuralTypeTests` |
| The expression language's own types, shipped with it, and a constraint that a rule's condition must be a truth value | `ExpressionsLanguage.Types`, `SampleConstraints.ConditionMustBeBoolean` | `ExpressionsLanguageTests`, `SampleConstraintsTests` |
| The declared type-rule language: a rule as a model, lowered into the same `ITypeRule` | `Projectional.TypeRules/` | `TypeRuleLoweringTests` (incl. parity with `ExpressionsLanguage.Types`), `TypeRuleNotationTests` |
| Type expressions as data: primitive, record, list and optional, nested; the drawing of a type is exactly its `IType.Name` | `TypeRuleConceptKeys.TypeExpr` + four kinds, `TypeRuleLowering.LowerType` | `TypeRuleLoweringTests`, `TypeRuleNotationTests` |
| Named types: a type declared under a name and referenced from rules and other types, transparent and cycle-checked at load | `NamedType`, `TypeRule.Named`, `TypeRule.Ref` | `NamedTypeTests`, `TypeRuleLoweringTests`, `TypeRuleNotationTests` |
| Inline collection notation: a separated list that stays inside its line, so a record's fields can be written on one | `InlineCollectionNotationCell`, `NotationEditor.InlineCollection` | `InlineCollectionNotationTests` |
| A language project's `types.mps` loads into the registry a constraint asks for types, and the app checks each document with its own language's rules | `LanguageProject.Types`, `DocumentTab.Types` | `LanguageProjectTypesTests` |
| Quick fixes at the caret, the bulb, the offer list | `QuickFixes`, `OfferList` | `QuickFixesTests`, `QuickFixBulbTests`, `EditorSessionFixTests` |
| Standard check catalogue as a package (`STD001`, `STD003`, `STD010`, `STD040`) | `Projectional.Checks/` | `StandardChecksTests`, `CheckSetTests`, `PlaceholderNameCheckTests`, `SurroundingWhitespaceCheckTests`, `EmptyChildSlotCheckTests`, `BinaryGroupingCheckTests` |
| The app's own checks (default names, duplicate rule names) | `Projectional.Workbench/Checks/` | `DefaultNamesTests`, `DuplicateRuleNamesTests` |
| Interpretation: the `Evaluate` aspect per concept, inherited like the type rules | `IEvaluationRule`, `EvaluationRegistry`, `EvaluationContext` | `EvaluationRegistryTests` |
| The interpreter: explicit stack, post-order trace, step budget, cancellation, and a failure that names its node | `Interpreter`, `InterpretationResult`, `TraceEntry`, `InterpretationFailure` | `InterpreterTests` |
| The value model: int, bool, string and date as content-compared records, read through a property's declared type | `Value`, `IntValue`, `BoolValue`, `StringValue`, `DateValue`, `EvaluationContext.PropertyValue` | `ExpressionsEvaluationTests` |
| The shipped meanings: the expression language, and the pilot's decision list (first matching rule wins; no match is a failure) | `ExpressionsEvaluation`, `RulesEvaluation` | `ExpressionsEvaluationTests`, `RulesEvaluationTests` |
| The variables a run is given, read from one line of `name=value` pairs (number, flag, ISO date, else text) | `Projectional.Workbench/Run/RunInputs` | `RunInputsTests` |
| Running a document: its language's interpreter, the answer or the failure as a headline, and the trace as rows that jump to their node | `DocumentRun`, `RunReport`, `RunRow`, `MainWindow.RunActiveDocument` | `DocumentRunTests` |
| A document runs only when its language defines an interpreter, derived from its root concept rather than from the command that opened it | `WorkbenchLanguage.InterpreterFor`, `DocumentTab.Evaluation` | `WorkbenchLanguageTests`, `DocumentTabTests` |
| The output panel: F5 or Run, the headline, the inputs line, and a trace row that moves the caret | `MainWindow.axaml` (`OutputPanel`), `MainWindow.ShowOutput` | verified in a real window (probe: 5 steps for `amount=1500`, a failure for `amount=500`) |

Not done in M4: text generation and the cross-check tests that make generated code and the interpreter agree
on the corpus.

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
| Two test projects, xunit.v3 on Microsoft.Testing.Platform | `tests/` | 1860 tests |
| CI: Release build and tests on Windows and Linux, corpus job on Linux | `.github/workflows/ci.yml` | the workflow |
| Byte-identical round trip over the whole corpus | `tests/Projectional.Core.Tests/Corpus/` | `CorpusTests` |

## What is not here

Everything else. `roadmap.md` is the list of what is left, milestone by milestone, with the
acceptance gate each one has to pass.