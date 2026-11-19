# Notation, variants and triggers - how a language says what it looks like

> Part of the `projectional-editor` skill. `SKILL.md` is the index and holds the invariants, the
> conventions and the build commands; this file is one layer of the rest, read on demand.

## How notation is declared

`Projectional.Language` is where notation lives: declarative data that the projection engine
walks, validated once at registration against the `ConceptRegistry`. Nothing about it is code,
and nothing about it lives on `Concept` - Core must not know that projection exists.

- **The inline/block split is carried by the type hierarchy, not by validation.**
  `NotationCell` is abstract; `InlineNotationCell` runs horizontally inside a line, and
  `BlockNotationCell` occupies whole lines. `LineNotationCell` takes `ReadOnlyList<InlineNotationCell>`
  and `SequenceNotationCell`/`IndentNotationCell` take `ReadOnlyList<BlockNotationCell>`, so
  `ed.Line(ed.Indent(...))` does not compile. Do not weaken a collection type to make a
  notation convenient; the compile error is the feature.
- **`Sequence` vs `Indent`.** A sequence stacks blocks unchanged and is usually the root of a
  notation; an indent stacks them one level deeper. They are separate on purpose, so a
  notation says exactly one thing at a time.
- **`Horizontal` is the one cell where the parent decides a direction.** It takes blocks like a
  sequence and lays them out in one row, so a notation whose cells are each a small diagram can put a
  bracket beside them (`[ cells ]` in the editor language). Everywhere else the orientation is inherited -
  an inline child whose notation root is vertical breaks the line - because the parent cannot see the
  child's notation. It adds no gap of its own; a blank is a `Space` cell the notation asks for.
- **The runtime editor tree is a different vocabulary.** Notation cells are `…NotationCell`
  (`ConstantNotationCell`, `PropertyNotationCell`, `SingleChildNotationCell`,
  `ChildNotationCell`, `CollectionNotationCell`, `ConditionalNotationCell`, `WhenFilledNotationCell`,
  `LineNotationCell`, `SequenceNotationCell`, `IndentNotationCell`, `HorizontalNotationCell`,
  `SpaceNotationCell`) precisely so they cannot be
  confused with the runtime cells `docs/architecture/projection.md` names (`ConstantCell`,
  `PropertyCell`, ...).
- **`ConditionalNotationCell` is the only cell that may draw nothing.** It holds a property name, the
  text that property must equal, and inline cells; when the condition does not hold the builder emits
  *no cells at all* for it. Never "fix" that into an empty or zero-width cell - a zero-width cell is a
  caret stop on an address with no text. Two rules follow, and both bit on the first use:
  **the condition is a property + value, never a delegate** (a delegate cannot be written into a notation
  model, so the declared path would become weaker than C#), and **the gap belongs inside the conditional**
  (`ed.Conditional(..., ed.Keyword("abstract"), ed.Space)`, or every drawing without the word starts with a
  blank). `NotationRegistry` validates the slot is a property and the value is one its type accepts.
  Inline only - a block conditional is the same idea one level up, when something needs it.
- **Every slot-bearing cell has an `EmptyText`** (`<empty>`, `<none>`, and for a property
  whatever the notation wants to say - `<<operator>>`). Empty is never invisible: the caret
  needs something visible to attach to, and an empty list must stay fillable. Do not make it
  empty-string to "tidy up" a rendering. A property's stand-in is display only: the caret
  stands in the empty `Text`, so typing still starts from nothing.
- `NotationWalker.ChildrenOf` is the **single** place that knows a cell's children. Anything
  that walks notation goes through it - adding a cell type without adding it there makes the
  cell invisible to validation and to `Notation.SelfAndDescendants()`.
- `NotationRegistry.Add` is the only validator, and it is total: unknown concept, duplicate
  notation, property/reference/child cells naming slots that do not exist, and a
  `SingleChildNotationCell` on a many-child slot (it would hide the siblings). A rejected
  notation is not half-added - the registry stays clean.
- `NotationRegistry.FindFor(concept)` inherits: own notation first, then the super concepts in
  declaration order, so a concept that only extends a language reuses its notation.
- `NotationEditor`'s members touch no instance state and therefore trip CA1822, which is an
  error here. The suppression is class-level and deliberate: the documented surface is
  `ed.Keyword("if")`, which a static class cannot express.
- **`ITypingRules` sits in `Projectional.Language/Typing/`, beside `ICellEmphasis` and for the
  same reason** - it says how the language is written, so a language package must be able to
  declare it without referencing `Projectional.Editing`. `TypingRuleRegistry` is immutable
  (`Add` returns a new one), refuses a duplicate concept key with `ArgumentException`
  (`ParamName` `"conceptKey"`), and its `ClaimFor` answers `null` for a concept that declares
  no rule. `ExpressionsLanguage.TypingRules` is the reference declaration; `RulesLanguage`
  forwards it. What the editor does with a claim is in
  `docs/architecture/language.md` §Typing rules and `editing.md` §Typing into an empty slot.
- **`Applicability` is the "conditional concept"**: a concept may name the places it may stand in
  (`Applicability.Only(new ConceptPlace(Class, "attributes"), ...)`), because some concepts are not valid
  everywhere their type is accepted - a C# attribute, an XML comment. It is consulted by completion, by
  Enter's default fill and by the typing rules, and **enforced** in `Model.InsertChild`/`InsertChildAt`/
  `InsertRootAt`, which is why a filter bug cannot produce an illegal document. A concept that names places
  can never be a root (no parent, no slot). Do not merge it with `isOfferedInCompletion`: that one silences
  the list only, this one is legality. `ConceptRegistry.Create` validates every place at registration.
- **A trigger is declared on the concept it creates**, in `ConceptDeclaration.triggers`, and says what a typed
  character means at the *edge* of a node that already exists. Two reasons and both still hold: every slot it
  names is a slot of that concept, and a language that borrows another's `ConceptDeclarations` gets the gestures
  with them - there is no registry to forward (there was one, and it was deleted). The concept key is absent
  because the trigger creates its declaring concept, and triggers are **not inherited** (a subtype reusing the
  gesture would create the super concept). `ConceptRegistry.TriggersFor(Concept)` answers in concept declaration
  order, which is the only tie-break; `ConceptRegistry.Create` validates every trigger, so a typo is a language
  error at load. See `docs/architecture/triggers.md`.

## How notation variants work

A concept may be drawn more than one way - the same ruleset as a block and as a table - so
notation is registered per concept **and** variant
([ADR 0012](../../../docs/adr/0012-concept-rendered-more-than-one-way.md),
`docs/architecture/language.md` §Variants, `docs/architecture/editing.md` §Representations).

- **A variant is view state, never model state.** One variant is drawn at a time; the choice
  lives on `EditorSession.Choice` as an `INotationChoice`/`NotationChoice`, is never persisted
  and never creates an undo step. Do not move it onto `Node`, `Model` or the notation.
- **`Notation.Variant` defaults to `Notation.DefaultVariant`** (`"default"`), so the
  single-variant case is written exactly as before and every existing language keeps working.
- **Resolution is a chain walk with three fallbacks**: requested variant (own notation, then
  supers in declaration order) -> the default (own, then supers) -> the first declared variant
  of the chain -> `MissingNotationCell`. `FindFor(concept, choice)` is the only resolver
  (private `FindVariant`/`FindAny` do the walking); `VariantsOf(conceptKey)` answers "what is
  this concept declared as" from its *own* notations only. Do not add a second lookup that
  resolves differently.
- **`NotationChoice` is an immutable map from concept key to variant name** (`With`, `Without`,
  `VariantOf`, `IsEmpty`, `ToString` -> `"Rules.Ruleset=table"`). It is keyed by concept, not by
  node: per-node choice is open question 26.
- **`EditorSession.Alternatives()` walks the document, not the registry.** It returns the
  concepts the document actually uses that resolve to more than one variant, in document order,
  with the variant in force. A menu built from the registry would offer forms for concepts
  nobody wrote.
- **`ChooseVariant(conceptKey, variant)` validates twice** - the concept must occur in the
  document and the variant must apply to it - then `Choose`s and rebuilds. It throws
  `ArgumentException` with `ParamName` `"conceptKey"` or `"variant"`, because a bad menu entry
  is a programming error, not a user outcome.
- **A variant may hide slots; a shown slot keeps its natural address.** That single rule is why
  caret, selection, hit test and undo need no knowledge of variants at all. Never give a
  variant-carrying cell a synthetic address.
- **`ConceptVariants` (`ConceptKey`, `Variants`, `Selected`)** is the public, Avalonia-free shape
  a host binds a menu to. It is public because `Projectional.Editing` has no
  `InternalsVisibleTo`.

### Tables

`TableNotationCell`/`TableColumn` are the first variant that is not a rearrangement of lines
(`docs/architecture/projection.md` §Tables).

- **A column names a slot of the *row* concept**, it does not carry a cell: a property column
  becomes a property cell, a single-child column a child cell, a reference column a reference
  cell. That is what lets one table draw rows of different concepts that declare the same slot,
  and it is how a foreign key is drawn.
- **A header is a cell of the table's own notation** and the `TableColumn` constructor refuses
  anything but a constant or a space. A header that named a slot would silently depend on
  whichever row came first.
- **The row concept comes from the rows slot**, so `NotationRegistry.ValidateTable` checks the
  columns against the child concept of the `Slot` - not against the concept owning the table.
  A column naming no slot, a name that is no property, child or reference, and a many-valued
  child are all rejected at registration.
- **A reference column is projected by the same builder call a notation's reference cell is**
  (`EditorTreeBuilder.BuildReferenceCell`), with the column's `EmptyText` standing in for the
  notation's `UnsetText` - so unset, unresolved and resolved mean one thing wherever they are
  drawn. Do not give a table its own reference state machine.
- **An empty table draws its header and one `AddRowCell` row.** The header is what says what the
  columns are, and the affordance row is the only place the caret can stand to create the first
  row: `CaretPlace.IsEmptySlot` counts it and `InsertNodeAction` fills the slot through the same
  `InsertChildAction` an empty child slot uses. `TableCell.Flatten` refuses a table with **no
  columns** instead. The affordance's text is the notation's `EmptyText`, so
  `ed.Table("rules", "+ rule", ...)` is how a language says what an empty table looks like.
- **The affordance carries the slot's address, not the table's.** A table that is the root of its
  node's notation carries the node's address, which its headers carry too, so an affordance
  addressed that way is indistinguishable from the first header and the caret lands on "rule".
  `TableCell` therefore takes the slot address separately and the builder always passes it.
- **Layout reads the drawn grid from `Content`, not from `Rows`**, because an empty table draws a
  row the model does not have. `Rows` stays the model's rows; `IsEmpty` is `Rows.Count == 0`.
- **Headers are projected from the node that owns the table, not from a row**, so a table with
  zero rows still knows its shape and a header can never be answered by a row.
- **A column width is view state and a floor.** `IColumnWidths` (in `Projection/Layout`) is the
  question layout asks, `ColumnWidths` (in `Editing/Tables`) the answer; `EditorSession.SetColumnWidth`
  re-lays out without a transaction, so it is never saved and never an undo step. A width narrower
  than the content is ignored - clipping a value would hide what the model says.
  `ColumnResize.HandleAt` finds the boundary (the right edge of a column, **inside the header row
  only** - the body is where the reader selects and types), and the control only turns a drag into
  `SetColumnWidth`. The tolerance is horizontal only: a vertical one is a whole line in a monospaced
  measure and would make the first body row a handle.
- **`Editor.Table`/`Editor.Column` make a table declarable**, so a language defined in project files
  can draw one. The lowering goes through the same `TableNotationCell`/`TableColumn` the C# path
  uses; a column's `header` is lowered as an inline cell, so a header naming a slot is refused by
  `TableColumn` rather than answered by whichever row came first.

## How concept triggers work

Text entry has two halves, and neither can be derived from the model.
[`ITypingRules`](../../../docs/architecture/language.md#typing-rules) answers for an **empty** slot;
a trigger declared on a concept answers for the edge of a node that already exists: `5` then `+` gives
`5 + <empty>`. `docs/architecture/triggers.md` is the design of record.

- **A trigger is declared on the concept it creates** (`ConceptDeclaration.triggers`), and does *not* name
  that concept. It names the anchor concept (`Concept.IsKindOf` decides, so declaring it over an abstract
  concept covers every concrete one), the `TriggerSide`, the single-child `AnchorSlot` that receives the
  node, and - both optional - the `PropertySlot` the typed text goes into and the `CaretSlot` the caret
  continues in. `ConceptRegistry.TriggersFor(Concept)` answers in **concept declaration order**, which is the
  only tie-break. Triggers are **not inherited** - a subtype reusing the gesture would create the super
  concept. There is no registry beside the language: one existed (`ITriggerRules`/`TriggerRegistry`) and was
  deleted, because a language that borrows `ConceptDeclarations` gets the gestures with them and has nothing
  to forward.
- **A trigger is asked only where typing would otherwise do nothing.** In `TypeTextAction.Apply`: a
  property that *accepts* the text wins outright (`-` in front of `5` writes `-5`, the negation is never
  reached), a refused property falls through to the trigger, an empty slot stays the typing rules'
  business, and any other leaf asks the trigger. This is why the trigger lives *inside*
  `TypeTextAction` instead of being a second action under `KeyRouter`: asking first would duplicate the
  property validation the model owns.
- **`TriggerAnchors.Of(context, place, offset)` is the only answer to "what could this be an edge of"**,
  and it reads ownership, not containment: the cells whose `Address.Owner` is the node itself. Interior
  property offset - no anchor. First place the node owns - `Left`. Last - `Right`. A node drawn as a
  single cell - both, `Right` first. A keyword in the middle of a notation - nothing.
- **The wrap is `Model.Wrap` with the typed text as a preset** (the wrapper's id does not exist before
  the wrap), and the trigger's `CaretSlot` is where the caret lands, so `5` `+` `3` gives `5 + 3` with
  no arrow keys in between. Without a `CaretSlot` the caret stays inside the wrapped node, which is what
  a prefix operator wants.
- **A gesture may be spelt over several keystrokes.** `ConceptTrigger.Presets` gives the concept values it is born
  with whatever was typed (`and` -> `operator = "&&"`), and the run of keystrokes is kept in
  `EditContext.PendingText`: seeded by `EditorSession.ContextFor` from the place the last keystroke stopped at,
  read back in `ApplyCore`, cleared by `Select` and by every action that is not a `TypeTextAction`. Nothing is
  applied and nothing is journaled while a word is being spelt, so the wrap is the only undo step. `TypeTextAction`
  asks `Candidates` - the run plus this character first, then this character alone - so one meaningless letter
  (`x` before `+`) can never block a gesture. There is no timer: the caret says when a word ends. A preset wins
  over the typed text where both name one slot, and a preset value the property refuses is dropped.
- **A wrong declaration is a load-time error, not a dead keystroke.** `ConceptRegistry.Create` validates every
  trigger: the anchor concept exists, `AnchorSlot` is a single child the concept declares and the anchor
  conforms to it, `PropertySlot` is a property and `CaretSlot` a child slot, every `Presets` key is a property,
  and the concept is concrete.
  At the keystroke `TypeTextAction.Fits` asks only the two things registration cannot know - does the concept
  conform to the slot the anchor stands in, and does `Applicability` allow it there - so a gesture that cannot
  run does nothing instead of reaching `Model.Wrap`, which would throw. A `PropertySlot` whose type refuses the
  text drops the text and still wraps, the same rule `AddChild(parent, slot, concept, propertySlot, text)` uses.
- **A structural keystroke gets no coalescing key.** `TypeTextAction.CoalescingKey` returns `null` when
  `WouldRestructure` - the property would refuse *and* a trigger claims the text - so `5`, `+`, undo
  gives back `5` and a second undo gives back nothing. `WouldRestructure` re-asks the property
  (`Compose` + `Type.IsValid`) because a key is read before the action runs; that duplication is
  deliberate and cheaper than threading a plan through the session.
- **A selection is never a trigger.** Wrapping a range needs a wrapper that can hold one, which is the
  intention list's job; typing over a selection stays "replace the text".
- The shipped catalogue lives on `Binary`, `Unary`, `Parenthesized` and `If` in `ExpressionsLanguage`: operators on
  either side become `Binary` (caret in the empty operand), `-`/`!` on the left become `Unary`, `(` on the
  left becomes `Parenthesized`, `and`/`or` on the right become `Binary` with `&&`/`||` as a preset, and `?` on
  the right becomes the `condition` of an `If`. `Binary`'s left trigger deliberately does **not** claim `-`, so the negation is
  the single reading of a leading minus without needing Unary to outrank Binary in the concept order.

