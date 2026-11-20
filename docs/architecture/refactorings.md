# Refactorings: extract and inline

*Rename and move are delivered; extract and inline are a plan, and this document says why they are a
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
| R2 | **the framework half is delivered.** `Intent.Inline` is a body: the declaration names the reference slot whose target stands for what the node means, and the edit is a program of `InsertCopyAction` (a copy of the target into the use's own place, new identities, the definition untouched) and `DeleteNodeAction` of the use - one undo step. It is `Destructive`, because replacing a node is. **Two parts remain and both are deliberate**: the deletion of a definition nobody references (the open question below, not a detail to slip in), and a *shipped* sample - no language in the repository has the "node standing for a use" shape yet, so the end-to-end proof lives in `InlineLanguage`, a fixture that has exactly that shape | - / 1 week for the rest |
| R3 | `Intent.Extract`: five more declarations (the enclosing concept, its slot, the new concept, the reference slot, the name), the lowering, and the empty-declaration cleanup | 1-2 weeks |
| R4 | the shell's **Edit ▸ Refactor** submenu, once two refactorings exist to put in it | 2-3 days |

**What measuring R1 found.** A session draws one root and `EditContext.NodeOf` walks from it, so a node of another
root of the same model is not in the session's tree at all: a move is a *within-one-tree* operation, which is the
boundary this plan already draws for extract ("into the nearest enclosing declaration", never a new document). It also
found that the only reference slots in the test language live on the rule rather than on the node a refactoring would
move, so "leave a reference where it stood" cannot be exercised until the sample language for extract exists - the
order the plan already has, now with a reason.

R1 was the only slice with no semantics to decide, which is why it came first and why it is shared with
the move plan.

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
  destructive in a way `EditSafety.Destructive` describes, and the offer list already has the word.
  Recommendation: delete when `ReferenceIndex` says this was the only use, and say so in the label
  ("Inline and remove 'build'").
- **May extract create a document?** A new file is outside one model's undo, so it is not one
  transaction. Recommendation: no; extract into the nearest enclosing declaration, and let "move to
  a new document" be its own feature once multi-document undo exists.
- **Where does the new node go when the ancestor has several slots that could hold it?** The same
  question node-moves slice B answers for a move, and it should be answered once: the declaration
  names the slot, so the answer is "the language said", not "the framework guessed".
