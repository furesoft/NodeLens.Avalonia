# Refactorings: extract and inline

*Rename, move and inline are delivered; extract is still a plan, and this document says why it is a
plan rather than a patch.* They look like the same kind of feature as the rename that shipped with
M6 - "the editor changes several places at once, inside one undo step" - and they are not, because
the rename already knew what a name is and these two do not know what a definition is.

## What is delivered

| Refactoring | Where | What it does |
| --- | --- | --- |
| Rename | `RenameNodeAction`, **Edit ▸ Rename...** | sets the node's name slot and the fallback text of every reference to it in the model, in one transaction |
| Move within a slot | `MoveNodeAction`, `NodeMove`, **Alt+Up/Down** | reorders a node among its siblings; the node keeps its id, so references and the caret survive |
| Move to a place | the standard intentions' `INT004`/`INT005` entries | the same move as a named destination - to the front, to the back - so one intention is one undo step |
| Move across slots | planned, slices B-E of [node-moves.md](node-moves.md) | a node that changes parent: what indentation means in MPS |
| Inline a use | `Intent.Inline` in `IntentionLowering`, `samples/declared-workflow/intentions/inline-a-call.mps` | replaces the node at the caret with a copy of what its own reference points at, in one transaction; the definition stays |

Both delivered refactorings are **structural and reference-preserving**: they change where a node
stands or what a slot says, and nothing about their meaning depends on the language. That is exactly
what makes them implementable in the editing layer, and it is the property extract and inline do not
have.

## Why extract and inline are a different problem

A refactoring needs three things: a **use** to rewrite, a **definition** to move or copy, and a
**place** for the result. Rename gets all three for free - the use is a reference, the definition is
the node whose name slot holds the name, and the place is wherever it already is. Extract and inline
have to find all three, and none of them is a framework fact:

1. **What a use is.** This model has three shapes that can stand for "a name that means something
   else", and they are not interchangeable:

   | Shape | Example | What "inline" would have to do |
   | --- | --- | --- |
   | a `ReferenceSlot` on the owner | a transition's `source` pointing at a state | replace a *reference* with *children* - the slot kind, and every notation drawing it, would have to change |
   | the reference's target text | `Name` in an expression, resolved through the scope | neither: a name in an expression is a free variable, and its value comes from the run, not from a definition |
   | a reference plus a node standing for it | nothing ships this today | replace that node with a copy of the definition - the only shape a generic inline can act on |

   The first line is the important one: unlike MPS, a reference here is a slot on the owner rather
   than a child node, so there is no node to replace when the caret is on a use. That is a modelling
   decision this framework made deliberately (one flat slot map, `reference` declarable beside
   `property` and `children`), and "inline" is the first operation that runs into its cost.

2. **What a definition is.** `ReferenceIndex` answers "who points at this node", which is the
   reverse question. "This node is worth extracting" is the language's judgement: a state is not a
   subexpression, a parameter is not a rule, and a `Rules.Rule`'s body is not a declaration at all.

3. **Where the result goes.** Extract has to create a node somewhere the user did not point at: in
   the enclosing declaration, in a new document, in a slot of the model root. Which slot, of which
   parent, with which concept - the same question node-moves slice B has to answer for a move, and
   harder, because extract may want a *new* document and this framework's undo is per model.

None of the three can be answered by the framework without inventing semantics, and a refactoring
that guessed would be a silent rewrite of the user's document in a way the language did not ask for.
ADR 0014's rule - a language is written twice, as code and as a model, and both are permanent - means
the answer has to be **declarable**, not a hook only a C# language can fill.

## The proposed shape: two more intention bodies

The cheapest honest path is not a new aspect but two more bodies in the **intention** language,
which already has the machinery a refactoring needs: a declaration per concept, a per-caret
availability, a safety class, the offer list, and the preview that shows the edit against a discarded
copy ([intention-preview.md](intention-preview.md)). A body is a value that builds an `EditAction`,
so an extract that lands in several places is an `EditProgram` - which is already one undo step.

| Body | Declares | Applies |
| --- | --- | --- |
| `Intent.Inline` | a concept and the slot that holds the reference to inline from, plus the concept of the node that stands for the use | replaces the node at the caret with a copy of the reference's target, then - when nothing else references it - deletes the declaration, as one program |
| `Intent.Extract` | a concept, the slot whose children are extracted, the enclosing ancestor concept to extract into, the slot on it that receives the new node, and the concept it is created as | moves the subtree into the declared place and leaves a reference to it where it stood, as one program |

Both are declarations a language already has the vocabulary for, and both keep the framework's
promise: it ships the mechanism (the model's own move, copy and delete), and the language ships the
policy (what a definition is, and where it belongs).

### What that requires first

- **A move that changes parent.** `Node.InsertChild` already detaches a node from wherever it was, so
  the model can do it in one transaction today - but nothing exposes it, because same-parent
  reordering goes through `ReorderChildren`. Node-moves slice B needs exactly the same primitive, so
  it is the first slice here.
- **A copy that keeps identity out of it.** `Model.InsertCopy` gives a subtree new ids, which is what
  "inline" wants (the copy is a second instance) and what "extract" does not (the moved node keeps
  its references, which is why a move is not a copy).
- **A decision about the first line of the table above.** Until a language asks for a use that is a
  reference rather than a node, `Intent.Inline` can only serve the third shape. Naming it in the
  declaration - "the node standing in slot S is the use" - is what keeps the body from pretending.

## Slices

| Slice | Delivers | Effort |
| --- | --- | --- |
| R1 | **delivered.** The cross-parent move was already possible - `Node.InsertChild` detaches the node from wherever it stood inside one mutation, and `EditContext.MoveChild` takes any parent - but nothing proved it and no action stated a *place*. `MoveNodeToAction` is that action: a parent, a slot and an index instead of a direction, answered as a reason when the computed place has gone stale, because a program must not throw halfway through. `MoveNodeToActionTests` pins what the primitive promises (identity kept, the old slot emptied, one undo step, a reference into the moved subtree still resolving, and a program of a move plus an edit undone once) and what it refuses (a node that is gone, a slot the parent does not declare, a concept the slot does not hold, a one-child slot already filled, a node into itself) | - |
| R2 | **delivered, with its subject.** `Intent.Inline` is a body: the declaration names the reference slot whose target stands for what the node means, and the edit is a program of `InsertCopyAction` (a copy of the target into the use's own place, new identities, the definition untouched) and `DeleteNodeAction` of the use - one undo step. It is `Destructive`, because replacing a node is. The subject is the **workflow sample**, which now has the shape the plan said nothing shipped: `Workflow.Call` extends `Workflow.Step` and references the step it runs, and `Workflow.Workflow.library` holds the reusable steps - so a call in the chain can be inlined into the commands of a step it shares. **The subject found a crash and a gap**: inlining a call whose target is an *ancestor* of the use builds a copy that contains the place it goes, and `Model.InsertCopy` recursed until the stack gave out - `InsertCopyAction` now refuses that with a reason, the way a move refuses a node into itself. What remains open is the plan's own question: whether inlining the last use should delete the definition | - / open: the cleanup |
| R3 | `Intent.Extract`, shaped in the section below: five properties on the body, one action rather than a program, one rule in the workflow sample, and the refusals and load checks that go with them | 1-2 weeks |
| R4 | the shell's **Edit ▸ Refactor** submenu, once two refactorings exist to put in it | 2-3 days |

**What measuring R1 found.** A session draws one root and `EditContext.NodeOf` walks from it, so a node of another
root of the same model is not in the session's tree at all: a move is a *within-one-tree* operation, which is the
boundary this plan already draws for extract ("into the nearest enclosing declaration", never a new document). It also
found that the only reference slots in the test language live on the rule rather than on the node a refactoring would
move, so "leave a reference where it stood" cannot be exercised until the sample language for extract exists - the
order the plan already has, now with a reason.

**What measuring R2 found.** A copy of a node that *contains* the place it goes is an infinite structure, and
`Model.InsertCopy` builds it by recursing until the stack gives out - a public API that a host can hang with one
call. The refactoring that runs into it is obvious in hindsight: inlining a call whose target is an ancestor of the
use copies the use's own parent chain into the use's place. `InsertCopyAction` refuses it with a reason, the way a
move refuses a node into itself - and so does the model now: `Model.InsertCopy` asks the same question of the
*source* that `InsertChild` asks of the child, because the node it inserts is a fresh copy whose own check passes
while the copy still contains the place it is going into. A host calling the model directly used to hang the
process; the action's check stays beside it, as the sentence a user reads rather than an exception.

It also found what the plan predicted: a chain of steps cannot host the shape at all, because every step's target is
either above it (an ancestor) or below it, so the only non-cyclic inline is a *forward* reference - which is not
what "reuse this step" means. Hence `Workflow.Workflow.library`: a workflow declares its reusable steps beside its
chain, a call in the chain references one of them, and the copy has somewhere to go that is not inside itself.

R1 was the only slice with no semantics to decide, which is why it came first and why it is shared with
the move plan.

## Slice R3: what an extract declares

An extract has three facts the framework cannot derive - a definition to make reusable, a use to leave where
it stood, and a place for the definition - and the table further up says the language states them. Five
properties on the body say exactly that:

| Slot | Names | In the workflow sample |
| --- | --- | --- |
| `into` | the concept of the ancestor that receives the extracted node | `Workflow.Workflow` |
| `slot` | the child slot of that ancestor which receives it | `library` |
| `as` | the concept of the node left in its place - the use | `Workflow.Call` |
| `reference` | the reference slot on the use that points at the extracted node | `calls` |
| `name` | the property of the use that receives the extracted node's name, or empty for none | `name` |

The subject is the rule's own `conceptKey`, so a rule reads "a node of this concept, pulled out of where it
stands, becomes reusable there". `into` and `slot` are the destination, `as` and `reference` are the use, and
`name` is the only thing written on it: a use is born with the definition's name, because a node in a slot
whose concept declares a name slot has to have one and an extract cannot ask for it. The user renames it
afterwards when the two should read differently - `rebuild : shared build` is what that looks like.

### One action, not a program

The plan above predicted that a body building several edits would be an `EditProgram`, and measuring the
three primitives says it cannot be one. A program is a list of **built** actions applied against a tree that
is rebuilt between the steps (`EditProgram.Apply` throws for exactly that reason), so a later step cannot
address the node an earlier one creates: `EditContext.AddChild` answers the new `Node` only when it runs, and
nothing carries that identity into the next step. The alternative - a placeholder id that a program resolves
to "what I created" - would be a new addressing rule every action had to honour.

The body therefore lowers to **one action**, `ExtractNodeAction`, which is also the honest unit: its mutations
are one edit with one result, and no half of it may be observed. Like `MoveNodeToAction` it is built from ids
and a stated destination, so a declaration and a C# caller produce the same edit.

### What it does, in order

1. **Move** the node into the destination slot, at the end of it. A library and an aspect folder both read as
   a list of declarations, so the one just extracted belongs after the ones already there.
2. **Create the use** in the exact place the node left: same parent, same slot, same index. The slot is free
   because the move emptied it, which is why the move comes first - a single-valued slot refuses a second
   child.
3. **Write the name** into the use's property, when the rule named one. The text is `Node.Name`, which is
   the concept's own function first and its name slot second, so a definition that computes what it is called
   still names its use - and a node with no name at all writes nothing.
4. **Point the reference** at the node. `EditContext.SetReference` writes the target's id and its name as the
   fallback, so the declaration states nothing about resolve info and a use still reads when its target is
   later deleted.

The caret is left alone, which means it follows the node into the destination: the address still names a
cell, because the node kept its identity through the move, and "the thing I extracted now stands there" is
the answer the gesture asked for. (`WrapInAction` keeps the caret for the opposite reason - nothing moved.)

The entry's safety is `EditSafety.Plain`: nothing is removed - the node moves and a use appears where it
stood - which is the class the two move entries already have.

### What it refuses, before it changes anything

An action that has already moved the node cannot take that back - a `Rejected` result does not roll the model
back - so every refusal is decided before the first mutation. The list is what a stale declaration or a caret
in the wrong place can produce:

| Refused | Because |
| --- | --- |
| the node is no longer in the document | a program or a fix outlived it |
| the node is a root | there is nothing to extract it out of |
| no ancestor of `into` stands above it | the declaration is not about this node's enclosing tree |
| `into` has no child slot `slot` | the destination the rule names does not exist |
| the node does not fit the destination | its concept is not the slot's concept or below it |
| the destination holds one child and is filled | the place is taken, and by another node |
| the node already stands in the destination | "extract this" would move nothing |
| `as` is not a concept of the language | a rule outlived its language |
| `as` cannot stand where the node stood | the use does not fit the slot it is meant to replace |
| `as` declares no reference `reference` | the use could not point at its own definition |
| the reference would not accept the node | its target concept is not the node's concept or a base of it |
| `as` declares no property `name` | the name write the rule asks for has nowhere to go |

The same questions are asked at load time in `IntentionLowering` for the ones a declaration can answer
without a document: the concepts exist, the slot holds the subject's concept, the use fits the slot it
replaces, the reference accepts the subject, the property exists. That is the check the wrap body already
gets, and it is what makes a rule fail when the language loads instead of when a user runs it.

### Offered only where it can run

`Intent.Insert` taught the rule: an offer that comes back rejected is worse than no offer. So the lowering
installs a place condition for extract as well - an ancestor of `into` stands above the node, and the node
does not already stand in the receiving slot. Without it the entry appears on every step that is already in
the library, where the only possible answer is a refusal.

### The subject

`Workflow` already has both halves of the shape, and R2 built them: steps chain through `next`,
`Workflow.Workflow.library` holds the reusable ones, and `Workflow.Call` is the use that inline consumes. So
the sample gains one rule - `samples/declared-workflow/intentions/extract-to-library.mps` - and no new
concept. Caret on `test` in `release.mps`, before and after:

```text
workflow Release ->                           workflow Release ->
  build: dotnet build then                      build: dotnet build then
    test: dotnet test then                        test : test then <empty>
      rebuild : shared build then <empty>

library                                       library
  shared build: dotnet build then <empty>       shared build: dotnet build then <empty>
                                                test: dotnet test then
                                                  rebuild : shared build then <empty>
```

The two refactorings are inverses on this document: inlining the call that extract left behind puts the
commands back in the chain, and the library entry stays. That is the open question below, and it now has a
test that shows what either answer would change.

**What the shape costs.** Nothing new: `EditContext.MoveChild`, `AddChild` and `SetReference` exist, and R1
already proved that a cross-parent move keeps identity. One action, one body with five properties, one sample
rule, and the tests that pin them - the one-to-two-week estimate was for finding this shape.

## What this plan deliberately leaves out

| Left out | Why |
| --- | --- |
| a find-and-replace-style "extract everywhere" | the value comes from doing it once at a place the user pointed at; a bulk rewrite needs a preview per site, which is the intention preview times N |
| undoing a refactoring separately from the edit that preceded it | one transaction is the rule everywhere else in this framework, and a refactoring is not special |
| extract across models | undo is per model, so two models is two histories; the rename made the same choice for reference fallbacks ([open-questions.md](../open-questions.md), question 13) |
| a refactoring that rewrites text properties | the model is the only source of truth; a property value is text a user typed, and a rewrite of it is a search and replace |

## Open questions

- **Does `Intent.Inline` delete the declaration?** Inlining the last use leaves a definition nobody
  points at. Deleting it is what MPS does, and it is what the user meant - but it makes the edit
  destructive in a way `EditSafety.Destructive` describes, and there is a second obstacle the plan did not
  know about: a declared rule's label is built once, when the language loads (`DeclaredIntentions` builds each
  entry once and varies only availability per caret), so "Inline and remove 'build'" is not reachable through
  a declaration. The label would have to become a function of the caret, in `Intention` and in every builder
  that produces one.
  Recommendation: **do not delete.** Extract and inline are then exact inverses, a refactoring never removes
  something the user did not point at, and a definition nobody uses can be removed by the entry that already
  exists for it ("Delete this step"). The computed label is a question about the offer list, not about this
  body.
- **May extract create a document?** A new file is outside one model's undo, so it is not one
  transaction. Recommendation: no; extract into the nearest enclosing declaration, and let "move to
  a new document" be its own feature once multi-document undo exists.
- **Where does the new node go when the ancestor has several slots that could hold it?** The same
  question node-moves slice B answers for a move, and it should be answered once: the declaration
  names the slot, so the answer is "the language said", not "the framework guessed".
