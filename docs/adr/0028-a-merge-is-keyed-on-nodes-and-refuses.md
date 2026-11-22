# ADR 0028 — A merge is keyed on nodes and refuses rather than writes

- Status: Accepted
- Date: 2026-11-22
- Related: [diff-merge.md](../architecture/diff-merge.md), [ADR 0002](0002-model-as-source-of-truth.md),
  [ADR 0005](0005-byte-stable-xml.md), open question 14

## Context

The M6 gate has two halves: "a model can be renamed project-wide without breaking references", which is delivered, and
"two branches editing different parts of one model merge without hand-editing XML", which was a roadmap line and an
open question. A `.mps` file is byte-stable XML, so git can merge it - and for a file two branches touched in different
places it does.

It fails exactly where this framework is different. Two branches that each add a rule to the same body add two
*different nodes*, and git reports a conflict because the *lines* are adjacent. And a clean text merge can produce a
valid file that is not a valid model: a reference pointing at a node the other branch deleted, or a child in a slot the
other branch's concept no longer declares. Those failures are not cosmetic - they are the difference between a document
and a file.

Two questions had to be answered before any code: what is the unit of comparison, and what happens when the three
versions do not agree.

## Decision

**1. The unit is a node with its id.** A change is a `NodeDifference` keyed by `NodeId`, not a hunk of lines. The
framework already has that vocabulary (`ModelDiff`, with `Added`, `Removed`, `Moved`, `PropertyChanged` and
`ReferenceChanged`), and it already has the identity it depends on: a node id is stable, never renumbered, and
round-trips through the file. Therefore two insertions into one list are two insertions, and both survive.

**2. Three models are required, and never two.** Without the version both sides started from there is no way to tell
"ours changed this" from "theirs changed it back", so every question would degrade to "take one side". The engine takes
`base`, `ours` and `theirs`, and nothing else.

**3. A conflict is refused, never written.** When the three versions do not decide a question - the same property
written twice, a node one side edited and the other deleted, a reference the merge would leave pointing at a node it
removed - the answer is the list of conflicts and **no model**. Conflict markers in XML would be precisely the
unreadable file this exists to prevent, and a partially merged document that looks whole is worse than a refusal.

**4. The engine is a library call in `Projectional.Core`, and the surface is a headless verb.** A merge is model-level
policy over `ModelDiff` and needs nothing above Core. The first caller is
`Projectional.Workbench --merge <language> <base> <ours> <theirs> -o <merged>`, which runs before Avalonia starts the
way `--self-check` and `--docs` do, so a git merge driver can call it. A UI is a later caller of the same call, not a
second implementation.

**5. The language is an argument.** A document names its concepts, but the slots that make a property comparable and a
child a child live in the language's declarations. The verb therefore takes the language project's folder, and the
three files are read with it.

**6. Two additions of "the same thing" are two nodes.** The merge never matches nodes by name or by content. Both
branches adding a `build` rule produce two `build` rules; matching them would be a heuristic that silently drops one of
the user's edits, and the framework has a check (`STD002`) that reports the duplicate afterwards in the language's own
words.

**7. The merge does not run the language's checks.** It creates nodes and sets values; it does not ask constraints,
scopes or type rules whether the result is good. A merge that refused a document because a constraint fails would refuse
the intermediate state every refactoring passes through.

## Consequences

- **The exit code is the contract.** `0` merged and written, `2` conflicts and nothing written, `1` something could not
  be read. Git treats any non-zero answer as "this needs a person", which is what both failures mean.
- **A merge driver needs the executable quoted.** Git runs the configured command through `sh`, so a path with a space
  in it silently produces a conflict where the merge would have succeeded. This was found by doing it, and it is worth
  a sentence in the documentation because the failure looks exactly like a real conflict.
- **Three projects are needed to read the three files.** A project is a set of ids and the three files share them, so the
  reader would refuse the duplicate. The result is built in a fourth, fresh project.
- **A merged model is a new model, not a patched copy.** Every surviving node is created with its own id and attached
  where the merge decided it stands, so the result is a function of the three inputs and can be saved without touching
  a file the user has open.
- **The conflicts are data.** `MergeConflict` carries the node, the slot and both sides as text, so a report, a log line
  and a test all read the same thing without keeping three projects alive.

## Rejected alternatives

- **Writing a partially merged file beside the conflicts.** It looks merged and the report lives somewhere else; a
  document nobody can trust is worse than no document.
- **Conflict markers in the XML.** They produce a file the reader refuses, which is the failure the feature exists to
  remove.
- **Matching nodes by content to detect "the same addition twice".** A heuristic with no undo: the user's second edit
  disappears and nothing says so.
- **Doing the merge in the editor only.** It would be unavailable in a hook, which is where a merge is actually needed,
  and it would tie model-level policy to a UI.
- **A new package for the engine.** It would be a package for one algorithm that needs nothing but `Core`; `Merge/`
  beside `Diff/` is where the question it answers already lives.
