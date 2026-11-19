# Edit actions, intentions, checks, completion and the clipboard

> Part of the `projectional-editor` skill. `SKILL.md` is the index and holds the invariants, the
> conventions and the build commands; this file is one layer of the rest, read on demand.

## How the edit actions work

`Projectional.Editing` is the layer between a keystroke and the model. It has no UI
dependency, so every action is testable headlessly.

- **An address does not identify a cell.** Constants, punctuation and spaces all carry the
  address of the *node* they belong to, so the `{` at the end of a line and the `rule`
  keyword at its start share one address and one offset range. `CaretPosition` therefore
  also carries the **document-order index** of the cell (`Order`, `internal`): `>= 0` means
  "the closest caret place with this address", `< 0` means "the first place with it". Never
  resolve a caret by address alone - that is exactly the bug this hint exists for.
- **Corollary: `CaretPosition.ToString()` is `"{Address}:{Offset}"`, so it cannot be used to
  dedupe cells.** Several cells print the same string. When a test needs to know *which*
  same-address cell the caret is on, ask `CaretNavigation.PlaceAt(tree, position)` and compare
  `CaretPlace.Cell`.
- **`CaretPlace` is a `struct`.** `CaretNavigation.PlaceAt(...)` returns `CaretPlace?`, but
  `if (x is null) return;` does **not** narrow it - use `x.Value` afterwards.
- **A filled slot produces no caret place of its own** - only its leaves do. So Tab past a
  body that already has a step lands on the step's `index`, not on the collection.
- `EditorSession` owns the rebuild cycle: after every change it rebuilds tree *and* layout,
  then re-resolves the caret against the new generation, and hands the coalescing key back to
  the caller so a run of typed characters stays one undoable step. Rebuilding everything on
  every keystroke is deliberate M1 policy, not an oversight.
- **Enter inserts after the node the caret is in.** `InsertNodeAction.AddSibling` asks for
  `node.IndexInParent + 1` through `EditContext.AddChild(parent, slot, index, conceptKey)`, which is
  `Model.NewChildAt` - one `NodeAdded`, so undo takes the node out of the place it went into. Appending
  (`Model.NewChild`) is what the *slot-filling* half uses, where there is nothing to be after. It used to
  append everywhere, which made Enter in the middle of a body put the new step at the far end and drag the
  caret there; `EnterInTheMiddleOfAListDoesNotJumpToTheEnd` pins the fix.
- **Typing into an empty slot creates what you are describing.** `TypeTextAction`'s second case:
  an empty `ChildCell`/`CollectionCell` with a collapsed selection asks the language's
  `ITypingRules` which concept claims the text, creates it there and writes the text into the
  rule's property - one undo step, because `EditContext.AddChild` opens a nested
  `ModelTransaction`. It cannot be inferred from the concepts (a string property accepts `abc`
  as happily as `"abc"`), which is why the language declares it beside the notation. The caret
  afterwards is the *created node's* `CellAddress.OfElement`, so `Nearest` lands on the first
  typeable place inside it - `IntLiteral.value` (so `1` then `2` gives `12`), or
  `Parenthesized.inner` rather than the `(` the notation draws. A rule may name no property (a
  parenthesis is a gesture, not text); a rule naming a slot or a text the type refuses still
  creates the node and drops the text, because a keystroke must not be able to throw. A
  non-empty selection and a language with no rules both stay `Unchanged`.
- **The value a created node lands on arrives selected.** `EditAction.SelectsLandedValue` is the
  intent ("what I made is something to be named") and `EditorSession.Apply` carries it out against
  the tree the edit produced, widening the caret to the whole `PropertyCell` it landed in. It is a
  property of the *creating* edit only: if typing re-selected what it had just written, the next
  keystroke would replace it and no name longer than one character could be typed.
- **An empty single-child slot renders one cell** whose address is
  `CellAddress.OfSlot(ownerId, slot)` - that is the address Enter fills. **An empty collection
  slot renders one cell too**, showing its `EmptyText`; it has no `Content`, so it is a leaf
  and therefore a caret stop, which is why Enter and completion can fill it
  (`CaretPlace.IsEmptySlot` covers both `ChildCell { IsEmpty: true }` and
  `CollectionCell { IsEmpty: true }`). What produces *no* cell is a slot the notation does not
  draw - a concept with no notation, or a variant that hides the slot - and that is the only
  case where a keystroke has nothing to act on.
- **An `EditorSession` must be created after the nodes it should show exist.** It builds tree
  and layout in its constructor; nodes added to a live model afterwards need `Refresh()`. In
  tests this bites when a fixture builds the session before `model.AddRoot(...)`.
- **Typing into a defaulted property inserts, it does not replace.** Defaults are
  materialized values (`name` = `"unnamed"`, `enabled` = `"true"`), and the model cannot tell
  an unset property from a set one, so `"unnamed"` + `"P"` gives `"Punnamed"`. Accepted
  limitation until a `Node.Unset`/placeholder concept exists (future `feat/property-defaults`);
  the one path that escapes it is a node the user just created, whose value arrives selected.
- `Node.GetOrNull`/`Set` handle **properties only**. A reference slot needs `GetReference`,
  `GetReferenceTarget` or `SetReference`; asking `GetOrNull` for one throws
  `KeyNotFoundException` ("declares no property slot named ...").
- Text actions edit inside a single `PropertyCell`; a multi-cell selection degrades to caret
  behaviour. `Ctrl+Home`/`Ctrl+End` are routed as `CaretMove.DocumentStart`/`DocumentEnd`
  (`KeyRouter`), and `PageUp`/`PageDown` move the caret by `EditorSession.PageHeight`; `Ctrl+PageUp`/
  `Ctrl+PageDown` stay with the host.
- **`Ctrl+A` is a ladder, not one answer.** `SelectAllAction` widens one unit per press:
  the property at the caret, then the node under it (plus its descendants), then the
  document. `SelectionRanges` (`SelectionRanges.cs`) computes all three and returns `null`
  for a unit that does not exist, so a rung is *skipped* rather than blocking - a caret in a
  keyword has no value, so its first press selects the node. `SelectAllAction` compares
  candidate ranges with `Address`+`Offset` only, **ignoring `CaretPosition.Order`**: a
  position named by address alone has `Order == -1`, so record equality would call the same
  range different and the ladder would stall on one rung forever.
- **A double click asks for one rung of that ladder, narrower.** The control maps
  `e.ClickCount > 1` to `SelectWordAction` and `e.ClickCount > 2` to `SelectValueAction`, which
  are `SelectionRanges.OfWord` and `SelectionRanges.OfProperty` alone: the word under the
  pointer, then the whole text of the editable cell, or the selection exactly as the press left
  it when the cell is not a `PropertyCell` or is empty. Neither falls back to the node - a
  double click on a rule's `{` selecting the whole rule would answer a question nobody asked.
  A word is the maximal run of one character kind (letters/digits/underscore, whitespace, other)
  around the caret, taken from the character *after* the caret when it is a word character and
  otherwise from the one before, so clicking either half of a letter selects that letter's word
  and not the space beside it. Three things follow: the rules live in `Projectional.Editing`
  (the control owns no editing rule), the two units are two action types rather than one action
  with a width so a host can rebind them separately, and every press after the first
  deliberately does **not** set `_dragAnchor`/`_dragging` or capture the pointer, because the
  first pointer move would collapse the range onto the drag anchor and the selection the user is
  about to type over would vanish.
- **Node membership is `CellAddress.Owner`, never address equality.** A child's projection
  root carries the *parent's* element address, and a collection's gaps and separators carry
  the owning node's address, so "is this place inside that subtree" must be asked of the
  owner id or a selected list ends up with holes where its separators are.
- **`Selection.IsSingleCell` is address equality *plus* the order hint.** This is the trap the
  whole selection feature tripped over: `OfNode(rule)` for a `Rule` whose notation starts with
  the `rule` keyword and ends with `}` produces `Anchor = (rule.Id, 0)` and
  `Caret = (rule.Id, 1)` - equal addresses, different cells - so `IsSingleCell` was true,
  `HasText` was true, `SpansCells` was false, and `KeyRouter.Delete` sent the keystroke to
  `DeleteTextAction`, which refuses a `ConstantCell`. "Ctrl+A twice then Delete does nothing"
  was that. It now reads
  `Anchor.Address.Equals(Caret.Address) && (Anchor.Order < 0 || Caret.Order < 0 || Anchor.Order == Caret.Order)`,
  so a position named by address alone still compares by address. Do not "simplify" it back to
  an address comparison.
- **`KeyRouter.RemovesText` checks for a `PropertyCell` first, then `HasText`.** The order
  matters: a `HasText` selection on a non-editable cell must not reach a text action that is
  guaranteed to refuse it.
- **Delete acts on a cross-cell selection, through `DeleteSelectionAction`.** It asks
  `SelectionRanges.NodesIn` for the nodes the range *fully* covers (outermost only, never the
  document root) and removes them in one `Model.BeginTransaction()`, because `EditorSession.Apply`
  opens none and each `DeleteChild` journals its own step. A partly covered node removes
  nothing. `DeleteNodeAction.Landing` is `internal static` and shared, so both deletes land the
  caret in the same place. `DeleteNodeAction(NodeId)` is the same removal addressed by node id, which is
  what a repair needs - the caret may be anywhere when a fix runs - and it refuses the document root with
  a reason where the gesture is simply `Unchanged`. Cut and Copy do act on a cross-cell selection: they
  copy the covered nodes as a fragment (`CopySelectionAction`/`CutSelectionAction`) instead of text, which
  is what the structural clipboard is for.
- **`Model.DeleteSubtree` unlinks the top node only.** Descendants stay attached to each other
  and are merely unregistered from the project index, which is exactly what lets one
  `SubtreeSnapshot` undo restore the whole thing. So `body.GetChildren("steps")` still answers
  after `body` was deleted - a test asserting a deleted parent's children are gone is wrong.
- A cross-cell selection reports `IsSingleCell == false` and `HasText == false`, so no text
  action mistakes it for one. `SelectionRects.Of` already draws it correctly (both ends
  trimmed, intermediate cells whole, one rect per line) - the rendering needed no change.

## How checks and quick fixes work

A finding is a `Diagnostic` (in `Projectional.Semantics`); the repair is a `DiagnosticFix`
(`Projectional.Editing/Fixes/`). The contract is that a **fix produces an `EditAction`** and never mutates:
`EditorSession.Apply(DiagnosticFix)` joins the pick on `(Diagnostic, Id)` against the **current** `FixSet`,
runs the current object (never the caller's copy), and applies it with the coalescing key cleared so it is
one undo step. A pick that no longer resolves is `EditResult.Rejected` with a reason - the ordinary case,
not an error. A repair that takes several edits answers an `EditProgram`, which the session runs inside one
transaction and rolls back completely if a step is rejected.

- **`FixSet` is keyed by the `Diagnostic` record** and rejects two entries for one finding.
  `QuickFixes.At(session)` answers the caret's line (resolved geometrically, `MarkerGeometry` tops against
  `CaretBounds`), worst finding first; `QuickFixes.Of(session, diagnostic)` answers one finding. Both are
  derived per call and never cached.
- **`ApplyOffers()` never applies anything. It always opens the list** - one offer or ten. Do not reintroduce a
  "lone plain offer runs immediately" shortcut in any form, and do not gate it on `EditSafety` or on
  `Intention.Fix` either: one keystroke that sometimes acts and sometimes asks is one the user has to guess
  about, and its meaning would change the moment a language declared a second offer. `EditSafety` and
  `Intention.Fix` are still on the offer as *information* (a surface may mark a destructive row; `Fix` is what
  routes a repair through its own apply path) - they are not permissions. A one-entry `OfferList` is the
  ordinary case, not an exception.
- **A fix's label names the result** ("Rename to \"Check2\"") and `Description` is part of the contract; a
  menu falls back to the finding's message. The id is stable and never translated.
- **Safety is on the offer, not on the fix type**, because the intention catalogue carries the same enum.

`Projectional.Checks` is the standard catalogue a language includes by naming it: `ICheck.Find(Model)`
(the member is `Find` because CA1716 forbids `Of` on an interface member), `CheckSet` (ordered, immutable,
`With` to extend), and `StandardChecks` (`Names`, `Structure`, `References`, `Expressions`, `All`). A language publishes
one `CheckSet`; the host runs it and derives `MarkerSet`/`FixSet` from the fixes. A check produces
diagnostics *and* edits, so it names `Editing`, `Semantics` and (for the expression group) `Expressions` -
which is a reference a language package now makes too: since
[ADR 0021](../../../docs/adr/0021-one-project-per-language.md) a language's checks live in a `Checks/` folder
of the language project, and the project references the behaviour band. `Projectional.Checks` itself stays a
separate package because it is *the catalogue*, shared by every language.

Shipped checks and the rules they encode:

- `STD001` (`PlaceholderNameCheck`, `Hint`) - name still equals the concept's declared default; the repair
  names it `<Concept>1` with the first free suffix, computed as a group so two unnamed rules get `Rule1`
  and `Rule2`. A concept with no default is not reported.
- `STD002` (`DuplicateNameCheck`, `Warning`) - two children of one slot under one name. The group is one
  slot of one node (roots are not a group), the **later** child is the one reported and offered a rename
  from suffix 2, and a child whose name is still its concept's placeholder is skipped - that is `STD001`'s
  finding. **A node anything in the project references is not reported at all**: the rename would dangle a
  reference the repair never saw, so the ambiguity is left to the constraint that reports it at the
  reference. The referrer scan is over `Project.Nodes`, not the model, because a reference may cross a
  model boundary.
- `STD003` (`SurroundingWhitespaceCheck`, `Warning`) - surrounding whitespace; `Trim to "..."`. A name
  that is only whitespace is skipped, because trimming it would produce the empty name.
- `STD004` (`IdentifierNameCheck`, `Warning`) - the name breaks the spelling rule the concept declares;
  the repair is `Use "..."`, which is `NameRule.Propose`. **The rule is a declaration, not a setting**:
  `Concept.NameRule` is `NameRule.AnyText`, `NameRule.Identifier` or one of the case conventions
  (`Pascal`, `Camel`, `Snake`, `ScreamingSnake`, `Kebab`, `ScreamingKebab`, `Train`, `Dot`, `PascalDot`)
  and stays **nullable**, because "nobody declared a rule" and "somebody declared `any`" are different
  facts and only the second survives inheritance - a default would let `Framework.Named` (which declares
  none) shadow a language's own rule.
  The case rules are one algorithm (`NameCase`: cut into the words the name carries, join them the way
  the convention says), so `Propose` is both the repair and the test - a name the rule accepts is the
  name it proposes, and a proposal always passes `IsValid`. **One state, one finding**: an empty name and
  one still equal to the concept's placeholder are `STD001`'s, whitespace around a name is `STD003`'s, and
  `STD001`/`STD002` respell their proposals with the concept's own rule so a repair cannot leave `STD004`
  behind. The declared form is the structure language's `named by rule:` cell, which enumerates
  `NameRule.All`, where an empty cell means inherit - and `samples/declared-pipeline` declares `pascal` for a
  pipeline and `camel` for a stage, so the cell is proven from a folder and not only from a test. The shipped
  languages use it: rules/states/features are PascalCase, an expression name camelCase, schema/table/column
  names snake_case, a declared type PascalCase and a record field camelCase. The workbench's own `SHELL001`
  (`DefaultNames`) is the last hand-written copy of the placeholder rule and respells its proposal the same
  way, because it runs for documents whose language the shell does not know.
  **Do not put a rule on the definition languages themselves** (`Structure.Concept.key` as `pascal-dot`, a
  slot name as `camel`): that is a policy over every language a user writes in that folder, and open question
  109 keeps it out until a language-creation flow can offer it as a changeable default.
- `STD010` (`EmptyChildSlotCheck`, `Hint`) - a `Cardinality.One` slot with no child; the repair is
  `InsertChildAction`, which is the same creation path `InsertNodeAction` uses, so the fix and Enter cannot
  disagree. A slot whose whole set is abstract has no repair and therefore no finding.
- `STD011` (`MinimumChildCountCheck`, `Hint`) - the list half: a `Cardinality.Many` slot whose declared
  `Minimum` is at least one and that holds nothing. The same `InsertChildAction`, which is why the two checks
  cannot drift from Enter either.
- `STD012` (`EmptyNodeCheck`, `Hint`, destructive repair) - a list entry that holds nothing but its defaults;
  the repair is `DeleteNodeAction(NodeId)`, which exists because this check needed a delete that names its
  node. Three exclusions make it usable: **a list entry only** (emptying a single-child slot is `STD010`'s
  finding), **a concept that declares a slot to fill** (a leaf is its value, and a default cannot be told from
  a typed value), and **nothing set anywhere**.
- `STD020`/`STD021` (`UnresolvedReferenceCheck`, `Warning`) - a reference whose target is gone. **One check answers
  both ids**: a close candidate makes it `STD020` with one repair per candidate (`Point to "X"`), nothing close
  makes it `STD021` with the destructive `Clear the reference` - two checks would report one reference twice.
  Candidates are `IScopeProvider.ScopeOf(node, slot).Entries` filtered by the slot's concept and ranked by
  closeness to the kept `resolveInfo` (equal, equal ignoring case, one a prefix, at most two characters apart,
  scope order breaking ties); anything further is left to completion. Every "point" repair carries the target's
  node id in its fix id, because `FixSet` keys on the finding *and* the id - one finding with several repairs is
  the first real case of that shape. The provider is a **constructor argument** (default
  `DefaultScopeProvider.Instance`), because a language that replaces a scope level must hand its provider to the
  check as well as to a session. The repairs are `SetReferenceAction(owner, slot, target)` and
  `EditContext.ClearReference`; `EditContext.SetReference` compares the **project** now, not the model, or a
  candidate the scope offered from another document would be silently refused. **`SqlSchemaChecks` leaves the
  group out**: its keys cross documents on purpose, and a document opened alone has no targets.
- `STD041` (`DoubleNegationCheck`, `Warning`) - a negation whose operand is negated again by the **same** operator
  (`!!x`, `--x`); `!-x` is not one, so the check compares the two operator cells rather than keeping a table of
  involutions. The repair is one `EditProgram` of two `UnwrapAction`s (hoist the inner negation, then hoist its
  operand), so the operand itself ends up where the pair stood, in one undo step. **A pair at the top of a document
  is not reported**: unwrapping a model root would delete the node the session draws, so the finding is withheld
  rather than offered without a cure. Its primitive is `Model.Unwrap(node, slot)` - the only child of that slot
  takes the node's place and the node is deleted, all in one transaction - and it **refuses to drop structure**: a
  node holding another child list or a reference is refused, because an unwrap that deletes a subtree nobody asked
  about is a delete in disguise (properties are the exception, since they *are* the wrapper).
- `STD022`, `STD030`-`STD032`, `STD042`, `STD050`/`STD051` are **not shipped, and are not waiting to
  be written**. Do not add a check for a state the model cannot hold: the parser refuses a value its
  `PropertyType` does not accept on every write and the reader turns one into `ModelFormatException`, so
  `STD030`-`STD032` and `STD042` are unreachable; `STD050`/`STD051` need the opaque node `persistence.md`
  promises and the reader does not build; `STD022` is a policy a language writes itself, because a reference
  across models is a feature here. `docs/architecture/quick-fixes.md` records each.
- `STD040` (`BinaryGroupingCheck`, `Warning`) - an operand drawn without the parentheses its grouping
  needs. **The finding names the operand, not its parent**, and the repair wraps that operand: wrapping the
  parent leaves the same finding, and a repair that survives itself is broken. Precedence lives in the
  check, not in `ExpressionOperators`, which says analysis owns it.

Two structural primitives arrived with these: `Model.Wrap(node, wrapperConcept, wrapperSlot)` detaches,
creates and re-attaches inside one transaction (a single-valued slot cannot hold the node and its wrapper
at once), so identity and undo survive; `WrapInAction` is that as an edit and `InsertChildAction` is the
address-based half of Enter (`InsertNodeAction.FillSlot` delegates to it). Both decline with a reason
rather than throwing when the document moved on.

`Model.Unwrap(node, slot)` arrived with `STD041` as the inverse: the only child of that slot takes the node's
place, the node is deleted, and the three steps (child leaves, node leaves, child arrives) are one transaction -
undo replays them in reverse, which is why each has to be a placement the journal already knows how to take back.
It refuses a model root (the document a session draws *is* a root), a slot that does not hold exactly one child,
a child that may not stand where the node stood, and a node that still holds another child list or a reference -
an unwrap that deletes structure nobody asked about would be a delete in disguise. Properties are the exception,
because they are what made the node a wrapper. `UnwrapAction` addresses the node, not a cell, and turns the
model's refusal into a rejection with the model's own sentence: the model stays the one authority on a legal
placement rather than being restated in the edit layer.

## How intentions work

An intention is a *way to find* an `EditAction` at the caret, in `Projectional.Editing/Intentions/`, and it
is not a new kind of edit. The list is derived per call, never cached, and applies through the same session
path a keystroke takes.

- **`Intention`** carries `Id` (stable, never translated), `Label`, a **required** `Description` (a fix can
  fall back to the finding's message; an intention has nothing behind it), an optional `Gesture`, an
  `Availability`, an `EditSafety`, and `Create: Func<IntentionContext, EditAction?>`. That function is how
  deciding and applying stay separate: an intention never mutates, it produces an action.
- **`IntentionAvailability` has three states, unlike `FixAvailability`'s two**: `Applies`,
  `Disabled(reason)` and `Absent`. A fix is built per finding, so "does not apply" is simply "not built"; an
  intention is *declared once* and asked per caret, so it needs "not my caret" (`Absent`, dropped from the
  list) as distinct from "mine, but not now" (`Disabled`, listed with its reason).
- **A declaration is made per-caret with `With(availability)`**, which answers a copy. That is what lets a
  language hand back its whole catalogue and let the list filter it - do not mutate a declared intention or
  make availability a stored field of the aspect.
- **`IntentionContext` is read-only**: model, tree, layout, measure, selection, the resolved `Place` and the
  `Node` that owns the caret's cell. `Node` is the address *owner*, which is why an empty slot answers with
  the parent ("insert a rule here" is about the parent). It deliberately has none of `EditContext`'s
  mutation methods.
- **`IntentionList.For(session)` is the only answer to "what is here"**: host providers, then language
  providers, then `QuickFixes.At(session)`; each entry's `Id` de-duplicated first-wins, `Absent` dropped,
  `Disabled` kept. Order is fixed by `IntentionSource`, not by registration order; `AddIntentions` defaults
  the source to `Host`, so a language package says `Language` explicitly.
- **A fix enters the list as an intention** (`Intention.FromFix`) and keeps the fix on the entry
  (`Intention.Fix`). `Apply(Intention)` routes such an entry back through `Apply(DiagnosticFix)` so the
  repair is re-resolved against the *current* `FixSet`; everything else is inherited.
- **`Apply(Intention)`** refuses an absent or disabled offer with a reason, builds the action *when it runs*,
  clears the coalescing key (one undo step), runs an `EditProgram` in one transaction that rolls back whole,
  and closes any open quick-fix list. Do not move the "build the action at apply time" rule to list time.

Intentions are *behaviour*: they produce edits, so they name `Editing`. Since
[ADR 0021](../../../docs/adr/0021-one-project-per-language.md) they live in an `Intentions/` folder of the
language they belong to, with their namespace unchanged: `Projectional.Expressions/Intentions/`
(negate), `Projectional.Samples/Intentions/` (accept this rule) and
`Projectional.Structures/Intentions/` (make abstract / make concrete).
`docs/architecture/context-actions.md` carries the plan; the preview and slices A-G are delivered, the last
of them being the range form of `INT006` (`Wrap selection in "X"`).

- **`Projectional.Intentions` is the framework's own catalogue**, not a language's: `StandardIntentions`
  derives `INT001` (rename), `INT002` (duplicate), `INT003` (delete), `INT004` (move up/down), `INT005` (move to
  front/back), `INT030` (insert before/after), `INT031` (sort by name), `INT008` (select node/parent), `INT006`
  (wrap in X), `INT007`
  (replace with X) and `INT020`
  (reset to the default) from
  the *shape* of the caret's place, so any language gains them by registering the provider. Every entry is
  **absent where it would change nothing** (the first node cannot move up, a root has no neighbour, a property
  already holds its default, a list is already in name order) and `Delete`, `Replace` and `Sort` are the ones
  marked `EditSafety.Destructive`, carried as
  information for a
  surface rather than used to hide it. `WrapperConcepts.Of` is the whole of
  "wrap in X": a concrete concept whose single-child slot accepts this node's concept and whose own concept
  fits the parent's slot (and whose `Applicability` allows that place) is a wrapper - the first such slot in
  declaration order, and one id per concept (`standard.wrap.<conceptKey>`) so two wrappers cannot collide in
  the list. A root is never wrappable, because a wrapper has to take the node's *place*.
- **`ReplacementConcepts.Of` is the whole of "replace with X"**, and it is a search for the same reason: a
  concrete concept that conforms to the slot, whose `Applicability` allows that place, **and is not the concept
  the node already is**. A slot declared over one concrete concept therefore offers nothing - swapping a step for
  a step would delete it and put an empty one in its place - and that emptiness is the answer, not a gap.
  `Model.Replace(node, conceptKey)` is the primitive: delete and create at one place inside one transaction, so
  undo restores the **old subtree with its own identities** rather than a rebuilt copy. It cannot be assembled
  from `DeleteChild` + `AddChild` in an action, because a single-valued slot cannot take the new child until the
  old one has left and the two halves would be two undo steps. What the old node held deliberately does not
  travel - no declaration says which slot of one concept corresponds to which slot of another - and the deletion
  clears the references into the subtree as any deletion does. Both the action and the entry refuse a **model
  root**: the document a session draws *is* a root, so replacing it would take away the node the editor is built
  from.
- **The app registers the catalogue as the host's**, and that is why `Expressions.Intentions` no longer ships
  "wrap in parentheses": the catalogue derives `Wrap in "Parenthesized"` from the same declarations, and two rows for
  one operation is the thing the registration order would have made visible. What a language keeps is what a
  declaration cannot express - "Negate" is a `Unary` born with `operator = "!"`, and no declaration says which of the
  two unary operators a bare `Unary` should start with. Do not "fix" that by giving the concept a default operator
  without deciding what a fresh `Unary` should be; declared intentions (M5) are the general answer.
- **A permutation is not a program of moves, so the model takes the order.** `Model.ReorderChildren(parent, slot,
  order)` validates that the order is a rearrangement of the slot's children (each exactly once) before touching
  anything, then detaches them all and inserts them back in the new order - one transaction, one undo step. It is
  written that way rather than as the shortest sequence of moves because every entry it produces is a pair the
  journal already undoes: the removals replay in reverse, each putting a node back at the index it left, and the
  original order falls out. An order that already holds journals nothing, so sorting a sorted list cannot become an
  undo step. **The rule stays out of the model**: `ChildOrder.ByName` (in `Projectional.Editing`, public because
  `Projectional.Intentions` derives availability from it) is ordinal, case-sensitive and stable, and answers
  `null` for a single-valued slot, for fewer than two children, and for a list where **any** element's concept
  declares no name slot - a partial sort would put the unnamed ones in an order nobody asked for.
  `SortChildrenAction` moves the caret with the node it was in, because an element address names a *position*, so
  leaving the caret alone would stand the user in whatever took that position.
- **An addressed selection is idempotent; the ladder's is not.** `SelectNodeAction(NodeId)` selects a node's whole
  range through `SelectionRanges.OfNode` - the same computation `SelectAllAction` uses, so a menu row and a keystroke
  cannot disagree about what "this node" covers. The two differ in one way on purpose: the ladder must not stall, so
  it **skips** a rung the selection already covers, while the catalogue rows are always there and re-establish the
  range (hiding them would make the row appear and disappear while the caret moves inside one node). A node whose
  notation draws no cells answers `Unchanged` and leaves the caret alone rather than collapsing onto nothing.
- **A caret jump is an action, not a mode of `MoveCaretAction`.** `SelectSlotAction(CellAddress)` moves the
  caret into a slot and selects its text (a bare caret when the slot is empty), answering
  `EditResult.Unchanged` because a navigation is not an edit. It is what makes rename plain rather than a
  refactoring: nothing in another model can break, because no model was touched. Its address is always a
  *cell*, since a rename names the value a user can see.

- **A range entry has its own id and its own search.** `Wrap selection in "X"` surrounds every node the
  selection covers completely (`IntentionContext.SelectedNodes`, which is `SelectionRanges.NodesIn` - the
  same computation Delete uses), through the **run form** of `Model.Wrap` and `WrapInAction`, so it is one
  undo step. Three rules: the id prefix is `standard.wrap-selection.` and not `WrapPrefix`, because a
  selection keeps the caret inside one of the selected nodes and both rows are therefore listed; fewer than
  two nodes is the per-node row's business; and the nodes must be **consecutive siblings**, which
  `WrapperConcepts.Of(nodes, ...)` asks exactly as the model would refuse it, so an offer is never an edit
  that comes back `Rejected`. A single node may go into a many-valued slot (a wrapper is born empty), which
  is why a language whose only composition is a list gains both rows.
- **`NodeMove` has four members, and two of them are extremes.** `ToFront`/`ToBack` are named rather than
  "up, several times", because a caller that asked for the front and got one step would run the action
  repeatedly - several undo steps for one intention. `MoveNodeAction` answers `Unchanged` when the target index
  is where the node already is, which is what lets an entry that survived a document change do nothing instead
  of pushing an empty step onto the history.
- **`InsertSiblingAction(node, place)` is `Enter`'s other half**, not a second `InsertNodeAction`: it names the
  node and the side so a menu entry does not depend on where the caret is, it creates **the sibling's own
  concept** (inserting beside a step makes a step - `InsertNodeAction` creates the slot's default instead), and
  it refuses a root and a single-valued slot with a reason.

- **A value with no cell needs an action that names the node, not the cell.** `SetPropertyAction` addresses a
  `CellAddress` and is right when the user can see the value; `SetNodePropertyAction` addresses a `NodeId` + slot
  and is right for a value the drawing hides - behind a conditional (`abstract`), on a collapsed node, or in a
  slot the notation leaves out. Both go through `EditContext.SetProperty`, so a bad value is refused identically.
  `SetNodePropertyAction` must check `FindProperty` **before** reading `GetOrNull` - asking a node for the value
  of a child slot throws `KeyNotFoundException`, which is exactly `Test.Rule`'s `body`.

- **Some wrappers only mean something with a value, and the wrap has to carry it.** "Negate" is a `Unary`
  whose `operator` is `!`, and the caller cannot write that afterwards because the wrapper's id does not
  exist until the wrap runs. `Model.Wrap`/`WrapInAction` therefore take an optional
  `IReadOnlyDictionary<string, string> presets`, validated against the wrapper's declarations *before*
  anything is detached (a bad preset must not leave the node outside the document). Do not build a bespoke
  action per wrapper-with-a-value.
- **An intention that would change nothing is `Absent`, not `Disabled`.** Both expression intentions decline
  on a node that is already that wrapper, and "accept this rule" declines on a rule that already accepts: an
  entry that reproduces the current drawing (or writes the value it holds) is noise, and a *disabled* one
  would light the bulb for an operation nobody needs.
- **The app registers the providers once, at open**, in `MainWindow.RegisterIntentions`; registering them in
  `PublishFindings` (which runs on every change) would list every entry twice.

**The gesture and the list are named differently on purpose.** `EditorKey.QuickFix`, `QuickFixAction` and
`QuickFixBulb` keep the quick-fix name because they are the *affordance* the docs and ADR 0016 describe;
everything about the *list* is an offer: `OfferList` (of `Intention` entries), `EditorSession.Offered`,
`SelectOfferAction`, `AcceptOfferAction`, `OfferPresenter` (in `Projectional.Avalonia/Offers/`) and
`EditorSession.ApplyOffers()`. Do not "tidy" one half into the other's vocabulary - the split is what keeps
"a quick fix is an intention carrying its `DiagnosticFix`" readable.

- **`ApplyOffers()` asks `IntentionList.For(session)`**, so the popup lists host, language and fixes
  together, and it always opens a list - nothing is applied from the gesture, however few entries there are.
- **`AcceptOffer` calls `Apply(Intention)`**, which routes an entry carrying `Intention.Fix` through
  `Apply(DiagnosticFix)` - so accepting a repair keeps its re-resolution, and accepting an intention builds
  its action at that moment.
- **`OfferList` refuses an `Absent` entry** in its constructor: the list is what the user chooses from, and
  a row that says "not this caret" would be a row that cannot be chosen.
- **The bulb asks the same list** (`IntentionList.For(session).Count > 0`) and keeps that answer keyed on
  `(Layout, Caret, Markers, Fixes)`; a source registered *after* the document opened catches up on the next
  caret move, which is why hosts register at open.
- **Two offers with one id collapse to the first**, which is why the shell's `SHELL001` and the standard
  `STD001` must not both be published: the user would read one sentence twice with nowhere to see the
  duplicate. That is the same rule that lets a standard repair and its intention share an id.
- **`EditorMenu` derives the list once** and splits it: entries with `Fix` go to the "Quick fixes" submenu,
  the rest to "Intentions", fixes first, one separator after the group, and a submenu that would be empty is
  not added. Reading `QuickFixes.At` for the menu instead would let a fix reappear there after the merged
  list de-duplicated it. A disabled entry's `Description` is `Availability.Reason` (why it cannot run), not
  the offer's own sentence.

## How completion works

Ctrl+Space offers the concepts that could fill the slot at the caret, and an enumerated property
offers its values the moment the caret arrives. The list is **view state**, like the
representation choice: it lives on `EditorSession.Completion`, is never persisted and never
becomes its own undo step. `docs/architecture/completion.md` is the contract; the invariants
that are easy to break are these.

- **Proposals come from the model, not from text.** For a slot, the caret must sit in an
  **empty** one; the slot declares the concept its children must conform to; every non-abstract
  concept below it in the registry, in declaration order, is one entry. There is no word list,
  no frequency ranking and no learned order - declaration order is the language author's
  ranking, and the first entry is what Enter would have created anyway.
- **`CompletionSlot.At` is the only place that decides "can this caret be completed"**, and it
  accepts exactly four cell shapes: an empty `ChildCell`, an empty `CollectionCell`, a
  `PropertyCell` whose type names its values, and a caret **inside a leaf that fills a
  one-valued slot** - the last one is substitution (`Replace`), which offers what could take the
  child's place instead. A filled slot elsewhere or a constant gets nothing, and a child with a
  subtree of its own is never offered for replacement.
- **A type that names its values is the second kind of proposal.** `PropertyType.Symbols` is
  empty by default and an enumeration answers with its symbols in declaration order, so
  `CompletionSource.Values` needs no registry walk. `CompletionKind.Value` tells the host to
  label the item with the symbol verbatim instead of shortening it like a concept key, and
  accepting one is `EditContext.SetProperty` - the caret does not move, because a value replaces
  the text it stands for.
- **The value list opens on arrival, and only on arrival.** `EditorSession.Apply` compares the
  caret before and after a non-completion action and offers when it moved; `Select` offers
  unconditionally. The constructor and `Refresh` never offer, so opening a document and undoing
  stay quiet. This is not "auto-open while typing" - a request to type does not open anything.
- **Any non-completion action closes the list.** `EditorSession.Apply` hands the list to the
  action through `EditContext.Completion` only when the action *is* a `CompletionAction`, and
  drops it afterwards. So moving the caret, typing somewhere else and undoing all dismiss the
  list without any of them knowing completion exists. Do not add a second dismissal path.
- **Typing while a list is open filters, it does not edit.** `KeyRouter.Type(session, text)`
  routes to `NarrowCompletionAction` instead of inserting; Backspace with a non-empty prefix
  is `WidenCompletionAction`, and with an empty prefix it falls through, which closes the list.
  This is why the control must call the **session-aware** `Type` overload - the plain one would
  type into the model behind an open list. Over a value list this is not a nicety but the
  feature: every prefix of a symbol is an illegal value, so a filter is the only way a user can
  reach `Accept` from `Review`.
- **An empty match keeps the list open.** `EditorSession.Completion` stays non-null so Backspace
  can widen it again; the host hides a zero-row list instead. Hiding and closing are different
  states and the distinction is what makes backspacing past a wrong prefix feel right.
- **Icons are host `IImage`s, and the built-in set is vector geometry.** `CompletionItem`
  carries no palette and no colour, exactly like `TextStyleKind`: `CompletionKind` is a
  vocabulary of meanings and `CompletionPalette` maps it to a `CompletionIcon(IImage, size)`.
  The bundled icons are paths drawn by `GeometryImage` - a ~30-line `IImage` that applies its
  own translate/scale/clip - **not** `DrawingImage`.
- **Never go back to `DrawingImage` for these.** Measured on Avalonia 12.1.6: consecutive
  `DrawImage` calls with a `DrawingImage` came out translated a few pixels further right each
  time and then blank, and the icon column of every row after the first was empty on screen.
  `GeometryImage` and `RenderTargetBitmap` both paint correctly. A host that supplies a
  `Bitmap` (a real PNG) is fine - the bug is specific to `DrawingImage` reused across draws.
- **The list is painted in the editor's own surface, not in a toolkit popup.** The control is a
  single `Control`, and `Popup.Open()` needs a visual-tree parent it does not have.
  `CompletionPresenter` is a plain painter (`Measure`, `Render`, `RowAt`) and the control
  positions it from `CaretBounds` minus the scroll offset, flipping above the caret when it would
  overflow the bottom and clamping both axes. Pointer presses are tested against `RowAt`
  *before* the document hit test, and a clicked row runs `SelectCompletionAction` then
  `AcceptCompletionAction` - the same actions the keyboard would run.

## How the clipboard works

`Ctrl+A`/`Ctrl+C`/`Ctrl+X`/`Ctrl+V` select all, copy, cut and paste. Copy, cut and paste carry **either text
inside one property cell or a fragment of the model**; select-all widens. `docs/architecture/clipboard.md` is
the contract. Which of the two a gesture means is decided when it is pressed (`KeyRouter`), never stored: a
selection that covers nodes copies structure, a selection inside one value copies text, and a paste follows
the payload the clipboard holds, preferring the fragment.

- **The seam is `IEditorClipboard`**: `string? Text`, `ModelFragment? Fragment` and one default
  `Task RefreshAsync() => Task.CompletedTask`. It is not called `IClipboard` because Avalonia owns that name
  for its own abstraction, and the adapter is where the two meet. `null` means "no payload", so an empty
  clipboard is not an empty string, and **writing one payload clears the other** - a clipboard holds one
  thing, and a paste must act on what the user copied last.
- **Read and write are synchronous, and must stay synchronous.** A key arrives synchronously and
  an action applies synchronously; an asynchronous contract would put an `await` into every
  action that touches text. A platform whose clipboard can only be read asynchronously caches
  instead, and `RefreshAsync` is the hook that lets it pull the platform's text at a moment
  where waiting costs nothing - focus, or opening the context menu.
- **`RefreshAsync` returns `Task`, not `ValueTask`.** The callers that cannot await fire and
  forget, and a discarded `ValueTask` is an analyzer error because it may be pooled. A `Task`
  can be dropped without pretending anything was consumed. Do not "modernise" this signature.
- **`InMemoryClipboard` is what a session gets when the host supplies none.** It keeps both payloads, each
  write clearing the other; its `RefreshAsync` is the inherited default. Tests use it. A host with a real
  clipboard adapts it - `AvaloniaClipboard` in `Projectional.Avalonia/Input/` is that adapter, and it caches
  the platform's text so a synchronous `Ctrl+V` sees what another application put there. It also drops its
  fragment when a refresh reads text this editor did not write, because another application has written
  something since.
- **`SelectedText.Of(context)` is the single answer to "what does the selection say".** It is
  `internal`, clamps to the cell's length, and returns `null` unless the selection is inside one
  `PropertyCell`. Every text action goes through it; a second reading of the selection is a bug
  waiting to happen. Because `EditorSession.Select` already clamps, the clamp is unreachable
  through the public API - cover it through `CopyAction`/`CutAction`, not by calling it.
- **The actions are in `Projectional.Editing/Clipboard/`.** `SelectAllAction` climbs the
  ladder (see the editing section above) rather than selecting one cell. `CopyAction` writes the
  text and returns `EditResult.Unchanged` - a copy never changes the document. `CutAction`
  writes, then applies `DeleteTextAction(DeleteDirection.Forward)`. `PasteAction` hands the text
  to `TypeTextAction`, so a paste gets exactly the same validation and the same refusal as
  typing; an empty or null clipboard is `Unchanged` ("nothing to do"), never a rejection.
  `CopySelectionAction`, `CutSelectionAction` and `PasteFragmentAction` are the same three over
  nodes: `SelectionRanges.NodesIn` decides what is copied (the same computation Delete uses),
  Cut delegates the removal to `DeleteSelectionAction`, and Paste goes through
  `EditContext.InsertFragment`.
- **A fragment keeps what a paste has to put back.** `FragmentNode` records the concept key, the
  `Origin` id, the properties that were **written** (`Node.IsSet` - a defaulted value stays
  defaulted), both halves of every reference, and each child paired with its slot. `Origin` is not an
  identity - the copy gets a new id - it exists so that a reference *between* copied nodes is rebound to
  the copy; one that pointed outside keeps its id and `resolveInfo` and degrades to unresolved
  (ADR 0004). A value the target concept would refuse is an exception, never a skipped value.
- **A paste never replaces anything.** An empty slot takes the fragment (including an empty
  single-valued one), a node in a **list** takes it directly after itself, a node in a **filled
  single-valued slot** is refused with a reason (replacing is the labelled `Replace with "X"`
  entry), and a root is refused. Before anything is created the paste asks
  `ModelFragment.MissingConcept` and the slot's conformity, so a fragment from another language is
  refused by name instead of half-built.
- **A fragment does not leave the process.** There is no wire format: text is what crosses to another
  application. If that changes, the format belongs beside `ModelXml`, not in a second serializer here.
- **A paste never coalesces.** `PasteAction.CoalescingKey` is `null` (and `CutAction`'s too), so
  a paste is its own undo step and never merges into the typing around it. `ToString()` returns
  `"paste"`, which is what the undo label shows.
- **The keys are a third exception to "Control belongs to the host".** `EditorKeyMap.Map` returns
  `null` for Control combinations except the documented ones, and `A`, `C`, `X`, `V` join
  `Left`/`Right`/`Space` in that list. Shift does not change them: `Ctrl+Shift+C` is still Copy.
  A bare letter is never in the key map - it is text input, not a routed key.
- **The menu runs the routing, not a fixed action.** `EditorMenu.For` lists `Select All`, `Copy`,
  `Cut`, `Paste` after the edit entries, and each clipboard entry executes
  `session.Apply(KeyRouter.For(session, EditorKey.Copy))` (and so on) rather than constructing an
  action: the entry and its gesture cannot disagree about which payload they mean. `Select All` has
  **no** predicate - the ladder always has a rung left until the document is selected, and greying
  it out would make the node rung unreachable. `CanCopy` is true for text in a `PropertyCell` **or**
  for `KeyRouter.CopiesNodes(session)`; `CanPaste` is true for text of length **or** a non-empty
  fragment.
- **The Avalonia adapter resolves the platform clipboard lazily and swallows its failures.** A
  control that is not yet in a visual tree has no `TopLevel`, and a platform clipboard that
  refuses must not take the editor down with it - a copy that cannot reach the platform leaves
  the cached text alone. The control refreshes the clipboard on focus and before opening the
  context menu, and intercepts the platform's own paste so `Ctrl+V` goes through
  `PasteAction` (one undo step, one validation path) instead of the toolkit's default.
- **The shell hands one adapter to every tab.** `MainWindow._clipboard` goes into each `DocumentTab`
  (which passes it to the session) and onto each control. Without that, a subtree copied in one document
  could not be pasted in another, and - worse - the control would refresh an adapter the session does
  not read, so a text paste would use text the platform has already replaced.

Deliberate M1 limits, so nobody "fixes" them into the wrong milestone:

- **Text only, one cell.** Copying across cells, copying a subtree, and pasting a structural
  fragment all wait for M3. A multi-cell selection degrades to caret behaviour for every text
  action, exactly as it does for typing. `Delete` is the exception: `DeleteSelectionAction`
  removes the nodes a range fully covers in one transaction, so `Ctrl+A` twice then `Delete`
  removes the node - it used to do nothing, which is what made the editor feel broken.
- **No rich payloads.** No HTML, no images, no custom MIME types. The model is the source of
  truth and a formatted payload would be a second representation to keep in sync.
- **Cross-editor fragment paste is not supported.** Two editors in one process share the text
  through the platform clipboard, but not their identity or their references.

