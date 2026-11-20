# Import as a declaration (planned)

> Part of [import.md](../import.md), the design of record for text import. This file is the planned
> slices I9-I11; the contract, the failure discipline, the reference languages and the delivered
> slices stay in the parent.

Every other aspect of a language can be *declared* - its concepts, its notation, its types, its generator, its
constraints, its scopes, its intentions - and one cannot: **how to read its own files**. `ITextImporter` is a C#
interface a package implements, so a language written as a model (a `LanguageProject` folder) is a one-way street: it
prints its documents and cannot read them back. That asymmetry is what this section plans away.

### Why it has to be a language and not a hook

The same argument every declared aspect makes ([ADR 0014](../../adr/0014-both-authoring-paths-are-permanent.md)): a
declared importer must produce the same model as its hand-written twin on the corpus, compared as text through the
generator, because "both authoring paths are permanent" is only true if the second one can do the whole job. A hook -
"a language may point at a .NET type that parses" - would leave the declared path weaker at exactly the seam that makes
a language usable, which is the line the type rules, the generator and the constraints all refused to cross.

**Where it lives:** `Projectional.ImportRules`, beside `Projectional.TypeRules`, `Projectional.TextGen`,
`Projectional.ConstraintRules`, `Projectional.ScopeRules` and `Projectional.IntentionRules` - a new aspect is a new
language plus its lowering. A language folder gains an `import` folder next to `structure`, `editor`, `types`,
`generator`, `constraints`, `scopes` and `intentions`, one document per importer (one root per document, as everywhere
else), and `LanguageProject.Importers` hands the lowered `ITextImporter`s to a host. The C# path keeps `ITextImporter`
exactly as it is: the declared form lowers *into* it, so nothing above import learns a second contract.

### The rules are the declaration

Two layers, and the split is the design rather than an implementation detail:

| Layer | Answers | Vocabulary (closed) |
| --- | --- | --- |
| Tokens | what counts as a token, and where it started | `Import.Literal` (an anchor), `Import.Word` / `Import.Number` / `Import.Quoted` / `Import.Fixed` (the lexical classes a config format needs, one concept each), `Import.Skipped` (whitespace, comments: text that produces no node) |
| Rules | which concept a run of tokens becomes | `Import.Node` (concept + parts), `Import.Sequence`, `Import.Alternative`, `Import.Repeat` (with separator and minimum), `Import.Capture` (a run into a property, spelled as stored), `Import.Line` (the rest of the line into a property, the line break read and discarded), `Import.Nest` (a child slot taking another rule), `Import.Bind` / `Import.Matches` (a run remembered, then required to repeat), `Import.Raw` (a run the model does not map, kept verbatim), `Import.Fail` (the message a rule gives instead of "expected") |

- **No delegate, no expression, no "compute".** A rule either matches, captures, nests, repeats or chooses - the same
  small set of verbs a template has, for the same reason: a rule that could calculate is a programming language hiding
  in a model, and it would need an interpreter this framework does not have.
- **Every token carries its offset and length**, because `ImportDiagnostic` promises a line and a column and a parser
  that only answers a tree cannot keep that promise - the same requirement that decides which third-party TOML or
  HOCON reader may be used ([open question 98](../../open-questions.md#text-import)).
- **Ambiguity is a load error, not a runtime surprise.** `Alternative` branches must start with distinct anchors where
  the author used anchors at all, and a repeat whose separator cannot be told from its item is refused - the earliest
  moment both halves exist, which is the rule `NotationRegistry` and `TextGenLowering` already follow.
- **Tier 2 has a place in the vocabulary.** `Import.Raw` is what lets a declared importer be honest about a construct
  the model does not model: the run survives as a raw node and regenerates to the same text, exactly as `Xml.Raw` does
  today.
- **Every word beyond the template's four came from a proof case, not from imagination.** Each is the smallest verb that makes a format the
  framework already ships declarable, and the case that demands it is written down here so the vocabulary cannot grow
  into a grammar by accident:

| Word | Is | Because |
| --- | --- | --- |
| `Import.Bind` + `Import.Matches` | a run remembered under a name, then required to be the same run again | XML's `<a>...</a>`: the closing tag must equal the opening one, and nothing else in the vocabulary compares two runs |
| `Import.Fixed` | a run between two marks in which nothing is escaped | CDATA. `Import.Quoted` resolves the escapes it declares, so a section that must not is a different class rather than a flag |
| `Import.Fail` | the message a rule gives when it is the one that should have matched | "a trailing comma is not allowed" is what the file's author needs; "expected `}`" is what the parser happened to think. It carries a code and a message and never branches |
| `Import.Line` | the rest of the line into a property, with the line break read and discarded | a value that ends at the line break, in a format whose files are written on both Windows and Unix. A terminator literal matches one spelling of the break, so the other spelling stays in the value and the model carries a carriage return it can never generate back; "the end of the line" is also not expressible as a literal at all, because the newline is the terminator *and* the text |
| `Import.Importer.target` | the generator this importer reads back | the inverse of a template is the inverse of *one* template set, and a language has as many generators as it declares ([One importer per target](#one-importer-per-target)) |

### The shape of the declaration

The vocabulary above says what an importer *means*; this is what its author writes. Projectional means there is no text
syntax to type, so every line below is a drawing the notation layer can already build from `Line`, `Sequence`, `Indent`,
`Collection`, `Conditional` and `WhenFilled`. **No new cell kind is introduced** - a shape that needed one would make
the declared path need C# after all, which is the line
[ADR 0014](../../adr/0014-both-authoring-paths-are-permanent.md) draws for every aspect. Whether the drawings themselves
are the C# builder or an `editor` document is the usual order of work: the builder first, the declared notation once a
second language wants to describe one.

One document holds one importer and the importer *is* the document root, because there is no container concept anywhere
else either: a container with one child is a second place to say which definitions exist. Inside it there is one rule
per concept, the way a generator holds one mapping per concept - the concept key identifies the rule, which is what
gives the inverse an owner.

```text
importer Ini                                        // Import.Importer
  target   Ini                                      // the generator whose templates this importer inverts
  suffixes .ini .cfg                                // a fallback only: the file's content decides first
  default  inverse                                  // Import.Inverse - "read what the generator writes"

  token Word    plus . -                            // Import.Word: letters, digits, underscore, then these
  token Number  no leading zero, fraction, exponent // Import.Number: which spellings are legal
  token Text    quoted  open "  close "  escape \   // Import.Quoted: resolves the escapes it declares
  token Block   fixed   open <![CDATA[ close ]]>    // Import.Fixed: a run in which nothing is escaped
  skip  Comment from ; to end of line               // Import.Skipped: consumed between parts, no node

  rule  Ini.Pair                                    // Import.Node: the concept, then its body
    capture key   from Word
    literal "="
    capture value from Text
```

Five things about that shape are decisions rather than formatting:

- **One concept per token class, so no concept carries a parameter it does not use.** `open`/`close` on a `Word` would
  be a state the lowering had to reject; four concepts make it unrepresentable, the same reason `Structure.Concept`
  holds `properties`, `children` and `references` as three slots instead of one.
- **`skip` is not a rule part.** It is consumed greedily between any two parts, because a file's layout is not a
  construct - and a format whose whitespace *is* content (`Xml.Element`'s text today) reads it in a rule instead
  ([question 107](../../open-questions.md#text-import)).
- **A literal may stand inline** (`literal "="`) because it is an anchor, while a named token exists where a class is
  reused. That is what keeps a short rule short without adding a second way to write a keyword.
- **Escaping lives in the token** ([question 102](../../open-questions.md#text-import)): a capture writes what the token
  resolved, so the template vocabulary stays free of an escaping part and cannot disagree with the importer.
- **Consecutive parts are a sequence.** `Import.Sequence` exists only to group, for a branch of an alternative.

The parts, and the one thing each says:

| Part | Drawn as | Means |
| --- | --- | --- |
| Literal | `literal "="` | this anchor must be here |
| Capture | `capture key from Word` | the run becomes the property `key`, spelled as the token resolved it |
| Nest | `nest value from Ini.Value` | the child in slot `value` is read by that concept's rule, and the slot decides which concepts may stand there |
| Repeat | `repeat pairs from Ini.Pair separated by "," minimum 0` | a list of that rule; the separator is a literal or a token, and `minimum` is what the file must hold |
| Alternative | `alternative { literal "{"; ... \| literal "["; ... }` | one branch, chosen by its anchor |
| Bind | `bind open from Word` | remember this run under a name, fill no slot |
| Matches | `matches open` | the next run must be the same run again (XML's closing tag) |
| Raw | `raw until "</"` | tier 2: keep the run verbatim, generate it back |
| Fail | `otherwise fail "INI0140" "a trailing comma is not allowed"` | this rule's own message when it is the one that should have matched |

**Every refusal is a load error**, and `ImportLowering` shares the discipline `ConceptRegistry.Create` and
`TextGenLowering` already follow: a concept the language does not have, a slot named as the wrong kind, a property
filled twice, a `target` nobody declares, two `Alternative` branches that start alike, a repeat whose separator cannot
be told from its item, a `matches` whose `bind` is not earlier in the same rule, a `skip` that can match nothing (an
endless loop written as a model), and a `raw` whose terminator no token can produce. None of them is a surprise at
import time, because none of them is knowable only at import time.

### The default is the inverse of the template

This is the part that makes the feature tractable rather than a second grammar language, and it is the recommendation
recorded as an open question rather than assumed:

- **A language already says how it is written twice** - the notation says how it is drawn, the generator says how it is
  printed - and a separate grammar would be a *third* statement of the same syntax, free to drift from both.
- **The generator's parts invert one for one**: `TextPart` → an anchor, `PropertyPart` → a capture, `ChildPart` → a
  nest, `ChildrenPart` → a repeat with the separator template as the separator, `WhenFilledPart` → an optional branch,
  `ConditionalPart` → an alternative chosen by a property, `LoopPart` → a repeat of the body.
- **So the first reading of a document is "the same text the generator would write"**, derived from its templates, with
  a declared rule only where the file spells something the generator cannot produce: a comment, `<a/>` next to
  `<a></a>`, a number in a spelling the generator would not choose.
- **The places where the inverse does not exist are exactly the declared exceptions**, and naming them is part of the
  design rather than a gap: escaping (the reference languages settle spelling on the way *in*), insignificant
  whitespace (a file's layout is not a construct), and precedence (no part asks how an operand binds, which is why the
  C# generator parenthesises). A declared importer says what it does about each of them, or refuses the file.
- **"The generator" here always means the one the importer names**, because a language may declare several and their
  inverses are different readings of the same model ([One importer per target](#one-importer-per-target)).
- **Resolution is per slot, not per document.** The slot a rule fills declares which concepts may stand there, and each
  candidate's template begins with an anchor, so the dispatch table is built once at load and the parser has no state
  but the model and the slot it is filling. Two candidates with one anchor in one slot is a load error, the same check
  the `Alternative` branches get - which is why the inverse can be a table rather than a search with backtracking.

### One importer per target

A language has as many generators as it declares - `RulesTextGenerators` ships a JSON one and a C# one - and the inverse
of one template set is not the inverse of another, so no single document can read a language back into two spellings at
once. **The importer names the generator it inverts**, and a language that prints two formats ships two `import`
documents.

- **`target` is a name, not a concept**, the same kind of name `TextGen.Generator` has, resolved against the language's
  generator set at load. A name nobody declared is a load error rather than an importer that reads nothing.
- **The shell asks in the other order**, which is what keeps this from becoming a compatibility table: detection is the
  file's *content* first and the importer's `suffixes` only as a fallback
  (`ReferenceLanguages.Detect` already works that way), so an importer that declares no suffix is reachable exactly by
  what it can read.
- **Two importers of one language may accept different files**, and that is the point rather than a defect: each answers
  for one printed form. A test that demanded identical acceptance from both would be asking the framework to have one
  reading of two spellings.

### What must not happen

- **No second source of truth.** An importer still runs once and throws the string away; the model is what the editor,
  the checks and the generator see ([ADR 0002](../../adr/0002-model-as-source-of-truth.md)).
- **No parser in the editing path.** No re-parse per keystroke, no error recovery into a live document: tier 1 refuses
  the whole text and tier 2 keeps a run as raw, which are the two outcomes the contract already has.
- **No round trip as a promise for free.** A declared importer that reads a construct differently from the way the
  generator writes it breaks `generate(import(text))`, and the corpus test is where that is caught - not a note in a
  manual.
- **No pattern language.** No regular expression, no character ranges, no lookahead beyond `Import.Matches`, no
  backtracking across alternatives, no left recursion, no semantic action: token classes are parameterised data and the
  rules are the verbs above. A format the vocabulary cannot describe is a gap in the vocabulary
  ([question 101](../../open-questions.md#text-import)), not a reason to embed an engine.

