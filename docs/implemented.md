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
| Child slots declare a range: the storage shape plus the minimum the slot needs (`0..1`, `0..n`, `1`, `1..n`) | `ChildSlot.Minimum`, `ChildSlot.Maximum` | `ChildSlotTests` |
| CI on Windows and Linux, plus a Linux-only corpus job | `.github/workflows/ci.yml` | the workflow itself |

The stored form and the test harness are described in [persistence.md](architecture/persistence.md#what-m0-implements)
and [testing.md](architecture/testing.md).

## M1 - Projection and editing (complete)

| Feature | Where it lives | Proof |
| --- | --- | --- |
| Runtime editor tree: cells, addresses, one tree per node | `Projectional.Projection/Tree/` | `EditorTreeTests`, `CellTextTests` |
| Property placeholders: an empty property cell shows the notation's stand-in (`<<operator>>`), display only | `PropertyNotationCell.EmptyText`, `PropertyCell.EmptyText`, `CellText` | `CellTextTests`, `CellStylerTests`, `ExpressionsLanguageTests`, `EditorLoweringTests` |
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
| Concept and property descriptions on hover, and a slot's sentence over its value when the slot opts in | `ConceptTip`, `PropertyTip`, `PropertySlot.ShowDescriptionWhenFilled` | `ConceptTipTests`, `PropertyTipTests` |
| Notation variants, chosen per concept as view state | `NotationChoice`, ADR 0012 | `NotationChoiceTests`, `NotationVariantsTests`, `NotationAlternativesTests`, `NotationVariantProjectionTests` |
| Tables as a variant | `TableNotationCell`, `TableCell` | `TableNotationTests`, `TableProjectionTests`, `SampleTableVariantTests` |
| A table column that names a reference, so a foreign key is a column rather than a separate line: projected by the same `ReferenceCell` builder as a notation's reference cell, with the column's own empty text | `NotationRegistry.ValidateTable`, `EditorTreeBuilder.BuildColumnCell`/`BuildReferenceCell` | `TableReferenceColumnTests` |
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
| A language project is a folder of aspect folders, one definition per document (`structure/*.mps`, optional `editor`, `types`, `generator`), read in file-name order | `Projectional.Languages/`, `LanguageProject.Read` | `LanguageProjectTests` (incl. a document with two roots being refused) |
| The app composes the sample language with both definition languages and opens a language folder as a document | `WorkbenchLanguage`, File > Open language | `WorkbenchLanguageTests` |
| Model browser (walks the model, not the projection) | `Projectional.Workbench/Outline/` | `OutlineTests` |
| Problems panel (document order, jump target, stale findings last) | `Projectional.Workbench/Problems/` | `ProblemListTests` |
| Find: walks caret places in property cells, ordinal, skips the current match | `Projectional.Editing/Find/` | `FinderTests`, `FindNextActionTests` |
| Replace one and replace all (one transaction, right to left, refused values skipped) | `Projectional.Editing/Find/` | `ReplaceMatchActionTests`, `ReplaceAllActionTests` |
| Recent files (dedupe, order, cap, silent store failures) | `Projectional.Workbench/Documents/` | `RecentFilesTests`, `TextRecentFilesStoreTests` |
| Table add-row affordance: an empty table draws its header and one fillable `AddRowCell` row | `Projection/Tree/AddRowCell.cs`, `TableCell` | `TableProjectionTests`, `TableNotationTests`, `SampleTableVariantTests` |
| Table column widths, dragged and reset, as view state | `Editing/Tables/ColumnWidths.cs`, `ColumnResize`, `IColumnWidths` | `ColumnWidthsTests`, `ColumnResizeTests` |
| Self-contained single file, with a `--self-check` the bundle runs on itself | `Projectional.Workbench.csproj`, `SelfCheck.cs`, `ci.yml` | the `single-file` CI job: publish, assert one file, run the check |
| **The M2 gate**: a DSL that exists only as data - `structure/` and `editor/` folders of one-definition documents, plus a document | `samples/declared-pipeline/` | `DeclaredSampleLanguageTests` (loads, draws as a table, edits, saves, reloads byte-identically, hands the folder to the shell) |

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
| A language project's `types` folder loads into the registry a constraint asks for types, one rule or named type per document, and the app checks each document with its own language's rules | `LanguageProject.Types`, `DocumentTab.Types` | `LanguageProjectTypesTests` |
| Quick fixes at the caret, the bulb, the offer list | `QuickFixes`, `OfferList` | `QuickFixesTests`, `QuickFixBulbTests`, `EditorSessionFixTests` |
| Standard check catalogue as a package (`STD001`, `STD003`, `STD010`, `STD011`, `STD040`) | `Projectional.Checks/` | `StandardChecksTests`, `CheckSetTests`, `PlaceholderNameCheckTests`, `SurroundingWhitespaceCheckTests`, `EmptyChildSlotCheckTests`, `MinimumChildCountCheckTests`, `BinaryGroupingCheckTests` |
| The app's own checks (default names, duplicate rule names) | `Projectional.Workbench/Checks/` | `DefaultNamesTests`, `DuplicateRuleNamesTests` |
| Interpretation: the `Evaluate` aspect per concept, inherited like the type rules | `IEvaluationRule`, `EvaluationRegistry`, `EvaluationContext` | `EvaluationRegistryTests` |
| The interpreter: explicit stack, post-order trace, step budget, cancellation, and a failure that names its node | `Interpreter`, `InterpretationResult`, `TraceEntry`, `InterpretationFailure` | `InterpreterTests` |
| The value model: int, bool, string and date as content-compared records, read through a property's declared type | `Value`, `IntValue`, `BoolValue`, `StringValue`, `DateValue`, `EvaluationContext.PropertyValue` | `ExpressionsEvaluationTests` |
| The shipped meanings: the expression language, and the pilot's decision list (first matching rule wins; no match is a failure) | `ExpressionsEvaluation`, `RulesEvaluation` | `ExpressionsEvaluationTests`, `RulesEvaluationTests` |
| A template part that emits a reference's target name, with the language's own words for an unset and a dangling target - so a generated file can name what a key points at | `ReferencePart`, `TextGen.Reference`, `Generator.ReferenceOf`, `TextGenLowering.ReferenceOf` | `GeneratorTests` (resolved, unset, dangling after a load, and the traceability region), `TextGenLoweringTests` (lowers end to end, refuses a property slot) |
| Text generation from a template tree per concept: literal, property, reference, child, list, loop, indent, line break, conditional, self | `TextTemplate` and ten `TemplatePart` kinds in `Projectional.Language/Generation/` | `GeneratorTests` |
| The generator engine: indentation from the template, a depth guard, and a traceability map that answers what wrote a character | `Generator`, `GeneratedText`, `GeneratedRegion` | `GeneratorTests` |
| Two generators for one language, proving a language has as many as it declares | `RulesTextGenerators` (JSON and a C# fragment) | `RulesTextGeneratorsTests` |
| The declared generator language: one generator per document, a template per concept, lowered into the same runtime generator, loaded from the `generator` folder | `Projectional.TextGen/`, `LanguageProject.Generators` | `TextGenLoweringTests` (incl. parity with the C# twin) |
| The shell offers a document's generators by name, shows one row per generated line, and saves the text to a file the user picks | `MainWindow.UpdateGenerators`, `DocumentGenerate`, `MainWindow.OnSaveGenerated` | shown in a real window by `tools/Projectional.Screenshots` (`feature-generate-output.png`: C# 52 character(s), 13 region(s)) |
| The variables a run is given, read from one line of `name=value` pairs (number, flag, ISO date, else text) | `Projectional.Workbench/Run/RunInputs` | `RunInputsTests` |
| Running a document: its language's interpreter, the answer or the failure as a headline, and the trace as rows that jump to their node | `DocumentRun`, `RunReport`, `RunRow`, `MainWindow.RunActiveDocument` | `DocumentRunTests` |
| A document runs only when its language defines an interpreter, derived from its root concept rather than from the command that opened it | `WorkbenchLanguage.InterpreterFor`, `DocumentTab.Evaluation` | `WorkbenchLanguageTests`, `DocumentTabTests` |
| The output panel: F5 or Run, the headline, the inputs line, and a trace row that moves the caret | `MainWindow.axaml` (`OutputPanel`), `MainWindow.ShowOutput` | shown in a real window by `tools/Projectional.Screenshots` (`feature-run-output.png`: 5 steps for `amount=1500`) |
| The cross-check: for one input, the decision the interpreter reaches, the reason the generated JSON records, and the rule a separately assembled expression evaluator finds are the same | `Projectional.Samples` (`RulesEvaluation`, `RulesTextGenerators.Json`) | `SampleCrossCheckTests` (three rules, four inputs, so each input stops at a different rule) |
| `INT010`/`INT011` as menu entries: the row that does what Enter does names the concept it will create and is disabled with a reason where Enter would do nothing, and `Insert...` opens the completion list exactly where `CompletionSlot.At` says a list can be asked for | `EditorMenu.InsertEntry`, `EditorMenu.CanComplete`, `EditorSession.ContextFor` (internal, so the menu asks the same question an action does) | `EditorMenuTests` (`AnEmptySlotNamesTheConceptEnterWouldCreate`, `InsertIsDisabledWhereEnterWouldDoNothing`, `InsertEllipsisIsOfferedOnlyWhereCompletionIsPossible`) |
| Interfaces, slice I4: the records - ADR 0020 settles that the framework's vocabulary is injected into every registry rather than composed by hosts, and open questions 92-96 are decided by the code that shipped | [ADR 0020](adr/0020-framework-vocabulary-is-injected.md), [open-questions.md](open-questions.md) | `ConceptRegistryTests.AnInterfaceWithNoMembersIsAType` (question 94), `AnOverrideThatChangesTheShapeIsRefused` and `AnInterfaceMayContributeNoChildSlot` (93, 96) |
| Interfaces, slice I3: the shipped languages adopt `Framework.Named` - `Expressions.Name`, `Rules.RuleSet`/`Rules.Rule`, the `Test.Rule` fixture on both authoring paths, and the two concepts of `samples/declared-pipeline` - each dropping its own name-slot designation and keeping only the metadata it overrides | `ExpressionsLanguage`, `RulesLanguage`, `TestLanguage`/`DeclaredTestLanguage`, `samples/declared-pipeline/structure/` | `RulesLanguageTests.ARuleAndARulesetAreNamedThroughTheFrameworkInterface`, `ExpressionsLanguageTests.ANameImplementsTheFrameworkInterfaceAndKeepsItsOwnSentence`, `ConceptRegistryTests.TheFixtureProvesThatAnImplementersMetadataBeatsTheInterface`, `DeclaredSampleLanguageTests.BothConceptsAreNamedThroughTheFrameworkInterface`; `LanguageParityTests` and `EveryDefinitionDocumentSavesBackByteIdentically` prove adoption moves no saved byte |
| Interfaces, slice I2: the declared path - `implements` and the `interface` header word in the structure language, the lowering and its refusal of a concept that is an interface without being abstract, `implements:` in the notation, `interfaces=` in the parity dump, and the two intentions | `StructureLanguage.ImplementsSlot`/`IsInterfaceProperty`, `StructureLowering`, `StructureNotations`, `StructureIntentions.MakeInterfaceId`/`MakeConceptId`, `ParityHarness.Dump` | `StructureLoweringTests` (`AConceptThatImplementsAnInterfaceLowersToIt`, `AnInterfaceThatIsNotAbstractIsRefusedWhereItWasWritten`, `AnInterfaceSaysInterfaceInsteadOfAbstract`), `StructureIntentionsTests` (five rules), `LanguageParityTests.TheDumpReportsWhatAConceptImplements` |
| Interfaces, slice I1: a concept kind whose properties and references its implementers inherit, conformance still `IsKindOf`, one flat slot map, metadata overridable by the implementer without moving the slot, and `Framework.Named` injected into every registry | `ConceptDeclaration(..., isInterface, interfaces)`, `Concept.IsInterface`/`.Extensions`/`.Interfaces`, `ConceptRegistry.ValidateInterface`/`HasSameShape`, `FrameworkInterfaces`, `Node.IsNamed` | `ConceptRegistryTests` (nine rules), `NodeTests.IsNamedIsAFactOfTheConcept`; `CorpusTests` and `EveryDefinitionDocumentSavesBackByteIdentically` prove the injection moves no saved byte |
| The three things a shell does with a document - run it, generate its text, derive its findings - as public entry points beside the control, answering a value instead of filling a panel | `Projectional.Avalonia/Run/` (`DocumentRun`, `DocumentGenerate`, `RunReport`, `RunRow`, `RunInputs`), `Findings/` (`DocumentFindings`, `FindingSet`); the app uses the same three | `DocumentRunTests`, `RunInputsTests`, `DocumentFindingsTests` |
| A host application that embeds the control: one document, the four gestures (check, run, generate, break a rule), and no reference to the standalone app - so the M4 gate's "inside the embedded control in a small host application" is a fact, and a build that needed an internal package would break | `samples/host-app/` | compiled by `dotnet build` through the solution; started for twelve seconds in a real window with no output on stderr |
| The third worked example, `Projectional.Samples.SqlSchema`: two documents of one schema, tables drawn as a grid with their keys under them, a key that names a column of the *other* document, a DDL generator that names the column a key points at, and a `…Checks` package whose rule (a column name repeated in one table) carries a rename repair | `Projectional.Samples.SqlSchema/` (`SqlSchemaLanguage`, `Notation/SqlSchemaNotations`, `SqlSchemaTextGenerators`), `Projectional.Samples.SqlSchema.Checks/` (`ColumnNamesCheck`, `SqlSchemaChecks`), `Corpus/shop.mps` and `Corpus/orders.mps` | `SqlSchemaTests` (both files byte-identical, clean, the grid, the generated DDL, cross-document resolution, degradation when one file loads alone), `SqlSchemaChecksTests` (incl. the repair applied through a session) |
| A reference may cross a model boundary: `Node.SetReference` compares the project instead of the model, matching what the scope provider offers, what invariant 5 already cleared and what `model.md` says `resolveInfo` absorbs | `Node.SetReference` | `NodeTests.AReferenceMayPointAtAnotherModelOfTheSameProject`, `AReferenceRejectsATargetOfAnotherProject`, `DeletingATargetClearsAReferenceFromAnotherModel` |
| The second worked example, `Projectional.Samples.FeatureModel`: features with a selection, `requires`/`excludes` constraints as nodes of their own over an abstract base, an interpreter that runs every constraint and answers the configuration, a generator that writes `name=value` lines, and a `…Checks` package whose one rule (two features under one name) carries a rename repair | `Projectional.Samples.FeatureModel/` (`FeatureModelLanguage`, `Notation/FeatureModelNotations`, `FeatureModelEvaluation`, `FeatureModelTextGenerators`), `Projectional.Samples.FeatureModel.Checks/` (`FeatureNamesCheck`, `FeatureModelChecks`), `Corpus/release.mps` | `FeatureModelNotationTests`, `FeatureModelEvaluationTests`, `FeatureModelChecksTests` (incl. the repair applied through a session), `FeatureModelCorpusTests` (byte-identical re-save, clean under its own checks, the notation, the generated configuration and the run's answer) |
| The first worked example, `Projectional.Samples.StateMachine`: three concepts whose transitions *reference* their states, one concept drawn two ways (a braced block and a table over `transitions`), `final` from a `Conditional` cell, an interpreter that walks the machine and fails where it stops, and the unreachable-state constraint beside it | `Projectional.Samples.StateMachine/` (`StateMachineLanguage`, `Notation/StateMachineNotations`, `StateMachineEvaluation`), `Projectional.Samples.StateMachine.Checks/` (`StateMachineConstraints`), `Corpus/release.mps` | `StateMachineNotationsTests` (block, table, and a non-final state drawing no word), `StateMachineConstraintsTests`, `StateMachineEvaluationTests`, `StateMachineCorpusTests` (byte-identical re-save, clean under its own rule, both notations pinned) |

Not done in M4: stepping (open question 11), and the dependency-set incrementality that lets the constraint
engine produce diagnostics in volume.

## M5 - Self-hosting (started)

| Feature | Where it lives | Proof |
| --- | --- | --- |
| A language definition is a model like any other: it opens, edits, saves and undoes in the same editor | `Projectional.Structures`, `Projectional.Editors`, `WorkbenchLanguage` | `StructureLoweringTests`, `EditorLoweringTests`, `WorkbenchLanguageTests` |
| The structure language's own notation, complete: a header that carries the inheritance and draws a list only while it is filled (`concept X extends A, implements B {`, `concept X {` when it has neither), a body in braces with one section per kind of slot, every declaration naming its kind and closing where it ends (`<<property … >>`, `<<child … [range]>>`, `<<reference … >>`), every collection stacked so a definition is one declaration per line, and an empty section naming the kind it will create (`<<property>>`, `<<child>>`, `<<reference>>`) | `Projectional.Structures/Notation/StructureNotations.cs` | `StructureLoweringTests.ADefinitionIsDrawnLikeAnyOtherModel`, `TheHeaderCarriesTheInheritanceAndTheBodyIsABlock`, `AChildSlotIsDrawnWithItsRangeInBrackets`, `EveryDeclarationOfASectionStartsANewLine`, `AnEnumerationsSymbolsStandOnePerLine`, `AnEmptyInheritanceIsNotDrawnAndLeavesNoCell`, `TheIntentionIsTheWayInToTheFirstSuperConcept` |
| A notation cell that draws its cells only while a child slot holds a child — the sibling of the property conditional, and the reason a header can say `concept X extends A {` and nothing at all for a concept without inheritance | `Projectional.Language/Notation/WhenFilledNotationCell.cs`, `NotationEditor.WhenFilled`, `NotationWalker`, `NotationRegistry`, `EditorTreeBuilder.BuildWhenFilled` | `WhenFilledNotationTests` (drawn while filled, nothing while empty, no caret stop left behind, refused over a property or an unknown slot, refused as a separator that draws nothing) |
| The declared twin of that cell, so a notation can be written as a model exactly as in C# | `Editor.WhileFilled` (`Projectional.Editors`), `EditorLowering`, `Rendering/EditorNotations` | `EditorLoweringTests.ADeclaredWhileFilledDrawsItsCellsOnlyWhileTheSlotHoldsAChild`, `ADeclaredWhileFilledIsValidatedByTheRegistry` |
| A notation cell that puts blocks in one row: the same children a sequence stacks, arranged horizontally, so a notation whose cells are each a small diagram can keep them on one line without weakening the inline/block split | `Projectional.Language/Notation/HorizontalNotationCell.cs`, `NotationEditor.Horizontal`, `NotationWalker`, `EditorTreeBuilder` | `HorizontalNotationTests` (two blocks on one row, a tall block keeping the row, the row staying inside its sequence, an empty one drawing nothing, and no implied gap) |
| The declared twin of that cell, so the editor language can say `beside each other` as a model | `Editor.Horizontal`, `EditorLowering`, `Rendering/EditorNotations` | `EditorLoweringTests.ADeclaredHorizontalPutsItsBlocksInOneRow` |
| The editor language drawn in MPS' glyphs: `-word-` for a keyword, `%slot%` for a property, `( slot )` for a single child, `{ slot }` for a list, `< slot >` for a reference, `;` for a gap, and `[/ ... /]` / `[> ... <]` / `[ ... ]` / `[| ... |]` for the four block containers - a line keeps its brackets beside its cells, and every string a cell carries stays editable | `Projectional.Editors/Rendering/EditorNotations.cs` | `EditorLoweringTests.ADefinitionOfANotationIsDrawnLikeAnyOtherModel` (the sample's header line in one row) |
| The two intentions that make a hidden list writable: `Inherit from…` and `Implement interface…` create the first entry, or append beside the last one when the list already has entries (Enter does not extend an inline list), and land the caret on the new key | `StructureIntentions` (`Projectional.Structures.Intentions`) | `StructureLoweringTests.TheIntentionIsTheWayInToTheFirstSuperConcept`, `TheInheritanceEntryAddsASecondEntryToAFilledList`, `StructureIntentionsTests.TheTwoInheritanceEntriesFillTheirOwnSlotAndLeaveTheOtherAlone` |
| Declared to C# export: a language folder becomes the C# a package is made of - its concepts as `new ConceptDeclaration(...)` with their slots, inheritance, interfaces and designated slots, its notation as the same `ed.*` calls it lowers through, and a README naming every aspect the export cannot write (`types` and `generator` as a member that throws, `constraints` as a note, because a `ConstraintSet` cannot be named through the toolkit). On request the source is compiled in-process (Roslyn, deterministic) into the assembly a package ships, and **File ▸ Export language to C#…** writes all of it into the folder the user picks | `Projectional.LanguageExport/Export/` (`LanguageExporter`, `DeclarationWriter`, `NotationWriter`, `SourceWriter`, `AssemblyCompiler`, `ExportOutput`/`ExportResult`/`GeneratedFile`/`GeneratedAssembly`), `Projectional.Workbench/Export/LanguageExportWriter.cs` | `LanguageExporterTests` (the sample's file pinned, the README's stub policy, the emitted assembly loaded back with its registry dump and drawing compared against the declared folder's, the emitted references limited to `Core` and `Language`, the same bytes twice), `DeclarationWriterTests`, `NotationWriterTests`, `LanguageExportWriterTests` (the files land where asked, UTF-8 without a BOM, a second export overwrites rather than appends) |

Not done in M5: live preview, bootstrap and the language browser.

## M6 - Declarative aspects (started)

| Feature | Where it lives | Proof |
| --- | --- | --- |
| Constraints as a definition language: a language's own rules are models, lowered into the same `IConstraint` a language written in C# declares, loaded from a `constraints` folder | `Projectional.ConstraintRules/`, `LanguageProject.Constraints`, `ConstraintNotations` | `ConstraintLoweringTests` (incl. parity with `Projectional.Checks`), `ConstraintLanguageTests`, `LanguageProjectConstraintsTests`, `DeclaredSampleLanguageTests` |
| Scopes as a definition language: a `scopes` folder names, per concept, which reference slot sees which place (`local`, `ancestors`, `model`, `project`, `everywhere`) and optionally which concept, lowered into the same `NamedScopeProvider` a C# language installs. Unnamed slots keep the default chain, two rules for one slot are refused rather than merged, and the shell hands an opened language's providers to its documents | `Projectional.ScopeRules/` (`ScopeLanguage`, `ScopeLowering`, `ScopePlaces`, `Notation/ScopeNotations`), `Projectional.Semantics/Scopes/` (`ScopePlace`, `ScopeBinding`, `NamedScopeProvider`, `ScopeWalk`), `LanguageProject.Scopes`, `DocumentTab.Scopes` | `ScopeLoweringTests` (a rule becomes a provider, two references of one concept share one, and the refusals for an unknown concept, a non-reference slot, an unknown target and a duplicate rule), `NamedScopeProviderTests` ("everywhere, no target" equals the default chain entry for entry, a target narrows to one concept, an unnamed slot falls back), `DefaultScopeProviderTests` (unchanged, on the shared walk), `LanguageProjectScopesTests` (a `scopes` folder loads into the language's providers, resolves from a real model, and refuses a rule naming a property at load) |

Not done in M6: the remaining aspect as a definition language (intentions), refactorings,
migrations tooling, model-aware diff/merge, model-to-model generation, text import, the projected C# base language,
the embedded shell, packaging.

## M7

Nothing is delivered. M7 (installers, file association) is untouched; the app is still `MainWindow`
alone, and `docs/architecture/standalone.md` describes the extraction M6 owns.

## Cross-cutting

| Feature | Where it lives | Proof |
| --- | --- | --- |
| 19 accepted architecture decisions, frozen | `docs/adr/` | the files; a change needs a new ADR that supersedes |
| Architecture documents per layer and per planned feature | `docs/architecture/` | the files |
| Two test projects, xunit.v3 on Microsoft.Testing.Platform | `tests/` | 2054 tests |
| CI: Release build and tests on Windows and Linux, corpus job on Linux | `.github/workflows/ci.yml` | the workflow |
| Byte-identical round trip over the whole corpus | `tests/Projectional.Core.Tests/Corpus/` | `CorpusTests` |

## What is not here

Everything else. `roadmap.md` is the list of what is left, milestone by milestone, with the
acceptance gate each one has to pass.
