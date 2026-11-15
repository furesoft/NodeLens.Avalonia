# Node moves

*Slice A is implemented; the rest of this document is the plan.* Reordering is the first edit
of this editor that changes neither text nor a value but the **order** of the model, and it is
worth its own document because it is the first operation that cannot be expressed as "type
something": a projectional editor has no text selection to drag and no character to cut, so
the operation only exists if the editing layer offers it.

The gesture is **Alt+Up / Alt+Down**, the shortcut every mainstream editor uses for the same
job. Everything else in this document is either what that shortcut must guarantee or what it
deliberately does not do yet.

## What moving means when the model is the text

In a text editor the order of the lines *is* the text, so moving a line is an edit of the
document like any other. Here the picture is derived: the order the user sees is the order of
the children of a slot, and the notation decides what that order looks like — a vertical list,
a comma-separated row, a column of a table. So "move the rule down" is not a statement about
pixels, it is `rules[i]` becoming `rules[i + 1]`, and it is the model that has to change.

That leaves two possible implementations, and only one of them is honest:

| | delete and insert | detach and insert |
| --- | --- | --- |
| Node id | new id | the same id |
| References into the subtree | break or have to be repaired | keep pointing at the node |
| Caret in the moved cell | address now names nothing | address still resolves |
| Undo entry | deletion plus creation, two shapes | one move |
| Cost | copy or rebuild the subtree | unlink and relink |

Identity wins on every line, which is why the model already had the primitive: `Detach()` is
documented as exactly this operation — "the node left its place and the caller is about to
give it another one" — and `InsertChild(slot, index, child)` calls it before inserting, so a
node that is already attached moves instead of being copied. A move is therefore *not*
`RemoveChild`, which goes through `DeleteSubtree` and destroys the subtree it removes.

Because the model journals `NodeRemoved` and `NodeAdded` inside one `BeginMutation`, the whole
move is one `ModelChangedEventArgs` and one undo step. The editing layer does not have to
group anything: it asks the model to move a child and undo puts it back at the index it came
from.

## The action

```csharp
public sealed class MoveNodeAction : EditAction
{
    public MoveNodeAction(NodeMove move);   // Up or Down
    public NodeMove Move { get; }
    public override EditResult Apply(EditContext context);
}
```

The node to move is the one the caret is in, resolved the way every action resolves a node:
`context.NodeOf(selection.Caret.Address)`. That is what makes the shortcut work while the user
is typing the rule's name — the caret is in the rule's `name` slot, the address carries the
rule's id, and the rule is the node that moves.

The action needs one mutation the context did not have, so `EditContext.MoveChild` was added
next to `AddChild` and `SetProperty`:

- it refuses a parent from another model, like `AddChild` does;
- it calls `InsertChild`, so the identity, the journal and the transaction are the model's
  business, not the editing layer's.

A move that cannot happen — the node is already first and the direction is up, or last and the
direction is down — returns `EditResult.Unchanged` and touches nothing. That is the same
answer `TabAction` gives at the ends of the document, and it is a signal the host may use: a
control that treats "the key did nothing" as "leave the key to the window" behaves correctly
for Alt+Up at the top of a list without knowing anything about lists.

## The caret after a move

The caret does not have to be told about the move, and that is a property worth stating
because it is easy to lose: a `CellAddress` carries a `NodeId`, not a position, so a caret in
`rule.name` is still in `rule.name` after the rule changed index. The session re-resolves the
address against the tree the move produced and lands on the same cell — now one line further
up. Moving a node therefore feels like moving the line under the cursor, which is what the user
asked for.

The one exception is a node whose cells the new notation does not show. A variant that hides
the slot the caret was in moves the caret to the nearest surviving place, which is the rule
the representation choice already follows.

## The gesture

| Key | Where it comes from |
| --- | --- |
| Alt+Up | `EditorKeyMap` maps it to `EditorKey.AltUp`, `KeyRouter` routes it to `MoveNodeAction(NodeMove.Up)` |
| Alt+Down | the same, to `EditorKey.AltDown` and `NodeMove.Down` |

This is a **documented exception** to a rule the input layer states in its own remarks: Alt
combinations belong to the host, because Alt+Left is a back navigation in many applications
and swallowing it would take a shortcut away from the very program that embeds the editor. The
exception is justified twice over — reordering a node is a model edit that no host can offer
on the editor's behalf, so nothing else can provide it; and the combination is the one a user
already knows from Visual Studio and VS Code, so following the convention costs no learning.
Alt+Left, Alt+Right and every other Alt combination stay with the host, which is asserted by a
test.

Ctrl+Up and Ctrl+Down are *not* taken: those are scrolling and paragraph motion in many hosts,
and there is no such thing as a paragraph here.

## Slices

| Slice | Branch | Delivers |
| --- | --- | --- |
| A | `feat/node-moves` | `NodeMove`, `MoveNodeAction`, `EditContext.MoveChild`, `EditorKey.AltUp`/`AltDown`, the key-map exception, tests for the order, the identity, the caret and the undo step |
| B | `feat/node-move-slots` | moving a node to another slot of the same parent, which is what indentation does in MPS: Alt+Left promotes a child into its grandparent's slot, Alt+Right demotes it into a slot of its previous sibling |
| C | `feat/node-drag` | the same move with the mouse: a drag that starts on a cell and ends between two cells, resolved through the hit test, with a drop indicator drawn by the control |
| D | `feat/node-move-selection` | moving a contiguous range of siblings as one step, which needs a selection that names a range of nodes rather than a range of text |
| E | `feat/node-move-caret` | a caret that follows the node visually through a move — with slice A it already does, so this slice is only about the case where the new notation hides the caret's cell |

Slice B is the one that makes the feature feel like MPS. Slice A deliberately stops at the
bounds of one slot, because moving across slots has to answer a question this slice does not:
*which* slot of *which* parent, given that the model may have several of them and the notation
decides which are visible.

## Performance

- **One transaction, one rebuild.** The move records two changes in one mutation, so a move
  costs the same as an insertion: one `ModelChangedEventArgs`, one tree rebuild, one layout.
- **No sibling copy.** `GetChildren(slot)` returns a live view, so the bounds check is a
  `Count` on a list that already exists — the action allocates nothing before it decides to
  move.
- **The whole subtree moves, not its cells.** Nothing in the projection layer is touched: the
  editor tree is rebuilt from the model like after any other change, and no cell is measured
  twice.

## What this plan deliberately leaves out

| Left out | Why |
| --- | --- |
| Cut / copy / paste of a subtree | a clipboard is a second model-like store with its own lifetime, ownership and reference-fixing rules; it is a feature of its own, not a variant of a move |
| Moving a node between documents | two models, one of which has to accept nodes it did not create; that is a persistence and ownership question before it is an editing one |
| Undo that restores the caret to the moved node | undo replays model changes and has never restored carets; a move is consistent with everything else, and pretending otherwise needs a caret journal |
| An automatic scroll to the moved node | slice A's caret already stays in the node, so the viewport follows the caret through the normal path |
| Wrapping around at the ends of a list | a shortcut that jumps from the last node to the first is indistinguishable from one that fired at the wrong moment |
| Climbing to the nearest movable ancestor | which ancestor the user meant is a guess; the caret names one node and that node moves, or nothing does |

## Decisions this plan proposes

| # | Decision |
| --- | --- |
| D1 | A move preserves node identity: it uses `Detach` plus `InsertChild`, never `RemoveChild` |
| D2 | A move is one undo step, because it is one mutation, without any help from the editing layer |
| D3 | The node that moves is the one the caret's address belongs to, not the line the caret is drawn on |
| D4 | A move with no sibling to trade places with returns `Unchanged` and changes nothing |
| D5 | Alt+Up / Alt+Down is the one Alt combination the editor takes, and the reason is stated in the input layer's remarks and asserted by a test |

## Testing

`NodeMoveTests` in the single test project holds the contract of slice A, over the rules of a
ruleset because they are the cheapest real list in the corpus:

- moving a rule up and down swaps it with exactly one neighbour;
- the caret is still in the moved rule's `name`, and the node still has the same id;
- the first rule moving up and the last rule moving down are `Unchanged`, leave the order
  alone and add no undo step;
- a move is one undo step, and undo puts the node back at the index it came from;
- two moves in a row are two steps and undo twice restores the original order.

`EditorKeyMapTests` holds the input side: Alt+Up and Alt+Down map, Alt+Left and Alt+Right do
not, and Alt+Ctrl+Up does not either.

A future slice B needs tests for the slot it picks when the model has several, which is why it
is a slice and not a patch.

## Open questions

- **Should the gesture be configurable?** A host with its own Alt+Up binding currently has to
  intercept the key before the control sees it, because `EditorKeyMap` takes it. The input
  layer's rule says a host's shortcut must win, and this gesture is the exception to it; an
  explicit "take this key" switch in the control's settings would make the exception a choice
  rather than a default.
- **Should the context menu offer it?** The menu is built from the session, so a "Move up"
  entry needs a session-level answer to "which node is under the caret", which
  `EditContext.NodeOf` only answers inside an action. Adding that lookup is small, but it is
  public API and belongs with the slice that also wants it for slice B.
- **What should Alt+Up do in a node that is its parent's only child?** Nothing, today. Climbing
  to the nearest movable ancestor is what a user may expect in a deeply nested document, and it
  is the one place where "the caret names the node" produces a surprising answer.
- **Should a move keep the scroll offset or follow the node?** Following the caret is the
  current behaviour and it is right for a one-line move; a move near the edge of the viewport
  may still want its own rule.
