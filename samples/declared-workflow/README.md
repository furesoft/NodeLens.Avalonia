# A workflow defined entirely in project files

A second sample language, beside `declared-pipeline`. Where the pipeline is the M2 gate (a language with no
reference in it), this one is the data demonstrator for the two aspects that arrived later: **scopes** and
**intentions**.

| File | What it is |
| --- | --- |
| `structure/` | `Workflow.Workflow` (a name and a first step) and `Workflow.Step` (a name, a command, the step to repeat on failure, and the next step) |
| `editor/` | one line each: `workflow <name> -> <step>` and `<name> : <command> <onFailure> then <next>` |
| `scopes/` | `Workflow.Step.onFailure` sees the steps above it - the chain a step is nested in |
| `intentions/` | what a step offers at the caret: `Delete this step` (destructive) and `Mark as a test`, which writes the command; and what an empty workflow offers: `Add the first step`, which fills the empty slot |
| `release.mps` | a document: `build` then `test` |
| `draft.mps` | a workflow with no first step, which is where the insert entry is offered |

The two concepts form a chain rather than a list, which is why the notation needs no collection cell: a step holds
the next step as its single child. That also makes `ancestors` the honest scope for `onFailure` - what a step may
point at is what encloses it.

## Why it is here rather than in the tests

It is what a user opens. `DeclaredWorkflowSampleTests` copies this folder next to the test assembly and reads the
same files the app does: it resolves `onFailure` through the declared scope, offers both declared intentions at a
step, draws the document, and re-saves every definition byte-identically.
