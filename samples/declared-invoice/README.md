# A language that extends another one

This folder is [`../declared-units`](../declared-units) plus two concepts: `Invoice.Line` holds a
`label` and the `quantity` it bills, and `Invoice.Bill` holds the lines it is made of. It exists to prove
the two decisions behind `language.mps` - that a declared base resolves into this language's registry,
and that an *extension* adopts everything the base declares
([ADR 0026](../../docs/adr/0026-a-dependency-resolves-to-the-vocabulary.md),
[ADR 0027](../../docs/adr/0027-behaviour-is-inherited-through-extends.md)).

| File | What it is |
| --- | --- |
| `language.mps` | the language itself: `Invoice`, version 1.0.0, `extends Units` |
| `structure/Invoice.Line.mps` | one concept, with a child slot declared over `Units.Quantity` |
| `structure/Invoice.Bill.mps` | a second concept: a bill that holds the lines it is made of |
| `editor/Invoice.Line.mps` | how it is drawn: `Paper = 12 in kg` |
| `editor/Invoice.Bill.mps` | how a bill is drawn: a header line over an indented list of its lines |
| `evaluation/line.mps` | what a line means: the amount of the quantity it holds, read out of the record the base language answers with |
| `evaluation/total.mps` | what a bill means: the total of its lines, which is a fold over the slot that holds them |
| `invoice.mps` | a document of the language: one line whose unit has surrounding whitespace |
| `bill.mps` | a document with three lines, so the total has something to add: 12 kg and 5 l and 30 pieces are `47` |

## What it proves

- **The slot resolves.** `Invoice.Line.quantity` is declared over a concept this folder never
  declares; without a resolved `Units` the language does not even load.
- **The behaviour follows.** `invoice.mps` is reported by `units.quantity.unit`, a rule that lives in
  the base folder - which is what `extends` means and `depends on` would not give.
- **The meaning follows too, not only the constraint.** `evaluation/line.mps` reads a field out of the
  record `Units` says a quantity means - a rule this folder never writes and a value kind it never
  mentions. Before that inheritance reached the evaluation aspect, the run stopped with "nobody said
  what this means" for a concept the base explains.
- **A total is a fold, not a chain of additions.** `evaluation/total.mps` is `sum` over the `lines` slot,
  whose each-body reads one field of the record each line's quantity answers with. Units are not added
  and no conversion happens: the amounts are, and the answer is `47`.
- **The declarations stay apart.** `Invoice` declares its own two concepts and nothing else; the base's
  concepts are in its registry, not in its declaration list, because that list is what the shell
  composes from.

## Open it

**File ▸ Open language…** with [`../declared-units`](../declared-units) first, then this folder, then
**File ▸ Open…** on `invoice.mps`. The other order is refused, and the message names the language that
is missing.
