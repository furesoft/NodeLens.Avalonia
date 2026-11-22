# Model-aware diff and merge

*The rename half of the M6 gate is delivered. This is the other half: two branches editing different parts of one
model merge without hand-editing XML.*

Two decisions carry the design, and both are the opposite of what a text tool does: the merge is **keyed on node ids**
rather than on lines, and a conflict is **refused, never written**. What follows says why each is forced, what already
exists, and what one slice of it would deliver.

## Why a text merge is not enough

A `.mps` file is byte-stable XML ([ADR 0005](../adr/0005-byte-stable-xml.md)), so git can merge it, and for a file
nobody touched in two places at once it does. The failures are not hypothetical:

| Git's text merge | What comes out |
| --- | --- |
| two branches each add a rule to the same body | **a conflict**, although they added two different nodes: the hunks are adjacent because the nodes are |
| both branches edit properties of *different* nodes that sit next to each other | the same conflict, for the same reason - lines, not nodes, are the unit |
| a clean merge where one side deleted a node the other side added a reference to | a **valid file that is not a valid model**: the reference points at nothing, and the reader reports a diagnostic rather than the merge reporting a problem |
| a clean merge where one side renamed a slot's concept and the other added a child to it | a document the language refuses, discovered only after the merge is committed |

The first two are why this exists: the unit of an edit here is a node with a stable id, so "both sides changed this
file" and "both sides changed the same thing" are different questions, and only the second one is a conflict.

## What already answers it

Nothing has to be invented to *see* a change:

- **`ModelDiff.Of(before, after)`** (in `Projectional.Core/Diff/`) reports five kinds - `Added`, `Removed`, `Moved`,
  `PropertyChanged`, `ReferenceChanged` - one `NodeDifference` per change, keyed by `NodeId`, carrying the slot, both
  `NodePlace`s and both values. It is the same machinery the intention preview already uses, so it is not new code
  written for a plan.
- **`Model.CopyTo(Project target, string? name)`** copies a model into another project *keeping every id*. That is what
  a merge needs and what `InsertCopy` deliberately does not do (a copy gets new ids, because a copy is a second node).
  The part that does it, `CopySubtree`, is private to `Model`; the merge uses `CopyTo` or asks for a narrower public
  entry beside it.
- **`ModelFile.Load` / `Save`** are the canonical reader and writer, so a merged model is written as the bytes the next
  load reads back - the merge cannot produce a file that round-trips differently from how it was built.

## Three inputs, three projects

A merge needs `base`, `ours` and `theirs` - the file as both branches last agreed on it, and the two edited versions.
Without `base` there is no way to tell "ours changed this" from "theirs changed it back", and every 3-way tool would
degenerate into "take one side".

A `Project` is a set of ids: `CreateDetached(id, concept)` refuses one that is already there. The three files share
its ids by construction, so they cannot live in one project. The shape is therefore:

1. load `base`, `ours`, `theirs` into three projects of their own;
2. describe both sides with `ModelDiff.Of(base, ours)` and `ModelDiff.Of(base, theirs)`;
3. build the result by copying **ours** into a fourth (fresh) project and applying to that copy what only theirs did -
   which keeps every id ours has and brings theirs' additions in with the ids they already carry.

## The rule

One node at a time, from the two `NodeDifference` lists:

| Ours | Theirs | Result |
| --- | --- | --- |
| unchanged | changed | theirs' version |
| changed | unchanged | ours' version |
| changed | changed to the **same** value | that value (a change made twice is not a conflict) |
| changed | changed to a different value | **conflict**, naming the node, the slot and both values |
| deleted | unchanged | deleted |
| deleted | changed | **conflict**: one side edited what the other removed |
| deleted | deleted | deleted |
| moved | moved to the same place | moved |
| moved | moved elsewhere | **conflict** |
| moved | deleted | **conflict** |
| added | - | added, with its whole subtree |
| - | added | added, with its whole subtree |

"It changed to the same value" matters more than it looks: two branches that both fix the same typo must merge, not
conflict, and comparing the two *values* rather than the two diffs is what makes that true.

## Order inside a slot

This is where the gate actually bites: two branches that add a rule to one body add two nodes to one list, and a
list is an ordered sequence. Comparing indices would call that a conflict; comparing *ids with anchors* does not.

For every child list (and for the model's roots) the three sequences of ids are compared:

1. **Deletion** removes a base id, from whichever side says so.
2. **Insertion** is placed after the base node it follows in its own side - its anchor. Insertions with the same anchor
   are emitted **ours first, then theirs**, each in its own order. Two unrelated additions to one list are therefore
   not a conflict, which is the case the milestone is named after.
3. **Reordering** is a conflict only when the two sides put the *same* base node in different relative positions. One
   side reordering while the other leaves the list alone is simply that side's order.

An inserted node whose anchor was deleted on the other side falls back to that anchor's own anchor, so an addition
does not vanish when the node it followed is gone.

## What it refuses to guess

- **Two additions of "the same thing" are two nodes.** Both branches adding a `build` rule produce two `build` rules,
  because matching them by name or by content would be a heuristic that silently drops one of the user's edits. The
  merge says nothing about it; a check that reports two children of one slot under one name (`STD002`) is what tells
  the user afterwards, in the language's own words.
- **A conflict is never written.** The tool writes no file, prints every conflict with the node, the slot and both
  values, and exits non-zero. Conflict markers inside XML would produce exactly the invalid document this exists to
  prevent - and a marker is a merge's way of asking a human, which is the same thing a report does, without pretending
  the file is loadable.
- **No three-way match of nodes that never met.** Two independently created nodes are different nodes; only ids decide.

## Where it runs

**A headless verb of the shipped app, first** (open question 14's recommendation: usable in a hook, before there is a
UI to put it in):

```powershell
Projectional.Workbench.exe --merge base.mps ours.mps theirs.mps -o merged.mps
```

It runs before Avalonia starts, the way `--self-check` and `--docs` do, so it needs no display and no window. The exit
codes are the contract: `0` merged and written, `2` conflict and nothing written, `1` a file that could not be read -
which is what a git merge driver reads, and git then leaves ours in place for a human.

A clone opts in, and nothing happens without that:

```text
# .gitattributes       *.mps merge=projectional
# git config           merge.projectional.driver "<app> --merge %O %A %B -o %A"
```

**Where the code lives**: `Projectional.Core/Merge/`, beside `Diff/`, because it is model-level policy over
`ModelDiff` and needs nothing above Core - no language, no projection. The verb in `Projectional.Workbench` calls it,
so no new package appears and the CI pack count does not move.

**What the UI would add later**: a conflict is a place in a tree, so the editor can show both sides of one node side by
side and let the user take either - the same preview machinery the intentions use. That is a slice of its own, and the
reason the engine is a library call rather than logic inside the verb.

## Proof

The gate is a test before it is a feature:

- a base model, edited on two sides in different slots, merges to the union - byte for byte the file a single editor
  would have written;
- two insertions into **one** list merge, both kept, ours before theirs;
- the same property written to two different values is a conflict that names the node, the slot and both values, and no
  file is written;
- delete against edit is a conflict; delete against delete is not;
- a move on one side and an unchanged node on the other is a move, and two different destinations are a conflict;
- a merged model round-trips: save, load, save are the same bytes;
- the verb's exit codes are pinned against real files, including a file that does not parse.

## Slices

| Slice | Delivers | Effort |
| --- | --- | --- |
| D1 | `ModelDiff` used as the side descriptor: a `MergeSide` per difference list, and the node-level rule table above with the conflict record. No writing yet - the answer for a given triple is data a test can assert | 2-3 days |
| D2 | The per-slot sequence merge with anchored insertions, which is the gate's own case | 2-3 days |
| D3 | Building the result: `ours.CopyTo` a fresh project, applying theirs' additions with their ids, and the refusal that writes nothing | 2-3 days |
| D4 | `--merge` in `Program`/`DocsExport`'s pattern, the exit codes, and the sample triple under `tests/` so the gate is one `dotnet test` | 1-2 days |
| D5 | The UI (two sides of one node, take either), once D1-D4 prove the engine |

## Open questions

- **May the merge consult the language?** It needs the concept registry to create a node in the result, and the
  registry is already in the project. What it must *not* do is run constraints or checks: a merge that refused a
  document because a constraint fails would refuse the intermediate state every refactoring passes through. The
  language's own findings are how a user learns the merged document needs a look - after the merge, not instead of it.
- **Does `.gitattributes` belong in this repository?** It changes what `git merge` does for everyone who clones, and
  the fallback for a driver that is not configured has to be measured before the line is committed. Recommendation:
  ship the documentation and the verb first, and add the line only with a measurement of both cases.
- **What does a conflict name for a moved-and-deleted node?** "Moved on one side, deleted on the other" has no single
  place to point at, so the report names the node and both places and lets the user look. Whether that needs a
  conflict *kind* beyond a sentence is something D1 will show.
