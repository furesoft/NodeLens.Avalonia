# A language that extends another one

This folder is [`../declared-units`](../declared-units) plus one concept: `Invoice.Line` holds a
`label` and the `quantity` it bills. It exists to prove the two decisions behind `language.mps` - that
a declared base resolves into this language's registry, and that an *extension* adopts the base's
behaviour ([ADR 0026](../../docs/adr/0026-a-dependency-resolves-to-the-vocabulary.md),
[ADR 0027](../../docs/adr/0027-behaviour-is-inherited-through-extends.md)).

| File | What it is |
| --- | --- |
| `language.mps` | the language itself: `Invoice`, version 1.0.0, `extends Units` |
| `structure/Invoice.Line.mps` | one concept, with a child slot declared over `Units.Quantity` |
| `editor/Invoice.Line.mps` | how it is drawn: `Paper = 12 in kg` |
| `invoice.mps` | a document of the language: one line whose unit has surrounding whitespace |

## What it proves

- **The slot resolves.** `Invoice.Line.quantity` is declared over a concept this folder never
  declares; without a resolved `Units` the language does not even load.
- **The behaviour follows.** `invoice.mps` is reported by `units.quantity.unit`, a rule that lives in
  the base folder - which is what `extends` means and `depends on` would not give.
- **The declarations stay apart.** `Invoice` declares `Invoice.Line` and nothing else; the base's
  concepts are in its registry, not in its declaration list, because that list is what the shell
  composes from.

## Open it

**File ▸ Open language…** with [`../declared-units`](../declared-units) first, then this folder, then
**File ▸ Open…** on `invoice.mps`. The other order is refused, and the message names the language that
is missing.
