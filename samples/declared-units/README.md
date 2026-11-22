# A base language: quantities and units

The smaller half of the pair that proves a language project can *stand on* another one -
[`../declared-invoice`](../declared-invoice) extends this folder. It is a base and nothing else:
one concept, one notation, one rule, no document of its own.

| File | What it is |
| --- | --- |
| `language.mps` | the language itself: `Units`, version 1.0.0, standing on nothing |
| `structure/Units.Quantity.mps` | one concept: a `value` and the `unit` it is measured in |
| `editor/Units.Quantity.mps` | how it is drawn: `12 in kg` |
| `constraints/unit-whitespace.mps` | the rule it states about itself: a unit with surrounding whitespace |
| `evaluation/quantity.mps` | what a quantity means: a record of its value and its unit |

## Why it exists

A declared dependency was a sentence nobody acted on until `ILanguageResolver`. This folder is what a
dependent language names, and its one rule is the finding that proves behaviour follows `extends` and
not `depends on` - see
[ADR 0026](../../docs/adr/0026-a-dependency-resolves-to-the-vocabulary.md) and
[ADR 0027](../../docs/adr/0027-behaviour-is-inherited-through-extends.md).

The rule is the interesting part: a quantity whose unit reads `" kg "` is reported by a rule that lives
*here*, even when the document belongs to `declared-invoice`.

## Open it

**File ▸ Open language…**, choose this folder, then open [`../declared-invoice`](../declared-invoice) -
that one declares `extends Units`, and the shell resolves it against the languages its window already
holds. Opening the dependent first is refused with a message that names `Units`.
