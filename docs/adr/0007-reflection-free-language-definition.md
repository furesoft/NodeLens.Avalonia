# ADR 0007 — Reflection-free language definition

- Status: Accepted
- Date: 2026-11-14
- Related: [language.md](../architecture/language.md#aspects)

## Context

A language framework needs a way for language authors to attach behaviour (validation,
types, scopes, generation) to concepts. The habitual .NET approach is attributes plus
reflection: `[Concept("Rules.Rule")] public class Rule { ... }` and a scanner that
builds the registry at startup.

That approach has problems in this specific component:

- Host applications embed the editor. Reflection scanning at startup is slow, and the
  set of loaded assemblies is not under our control.
- Trimming and AOT (increasingly relevant for desktop apps) break attribute scanning.
- Errors in concept declarations surface at runtime, if at all.
- Declared languages must be loadable as *data*; they cannot go through the same
  reflection path as compiled languages.

## Decision

Language definition is **explicit and reflection-free**:

- Concepts are built through a fluent builder that registers delegates:
  `concept.Validate((node, ctx) => ...)`.
- All aspects are delegates held in a `ConceptRegistry`. No attribute scanning, no
  `Assembly.GetTypes()`, no dynamic invocation.
- Declared (data-driven) languages produce the same delegates through a lowered
  representation, so the runtime has exactly one code path.
- Registration happens once at startup from an explicit list.

```csharp
public sealed class RulesLanguage : ILanguage
{
    public string Key => "Rules";
    public int Version => 3;

    public void Register(ConceptRegistry registry) =>
        registry.Add(Concept.Define("Rules.RuleSet", "ruleset")
            .Property("name", PropertyType.String)
            .Children("rules", ChildCardinality.ZeroOrMore, "Rules.Rule")
            .Validate(ValidateRuleSet)
            .Build());
}
```

## Consequences

### Positive

- Compile-time checking for built-in languages: a typo in a concept key or an aspect
  signature is a build error.
- Startup cost is proportional to the number of concepts, not the number of loaded
  types.
- Trimming/AOT stays feasible; the option is preserved at no cost.
- Built-in and declared languages share one runtime path, so behaviour cannot diverge.
- Delegates are plain objects — easy to wrap, instrument, and test in isolation.

### Negative / costs

- More verbose than attributes: each language writes a `Register` method.
- No "convention over configuration" magic; language authors must be explicit. This is
  judged a feature, not a cost.
- Delegates hold closures over language state; lifetime management is explicit
  (languages are singletons per project/registry).

### Rules

1. No reflection anywhere in the language or model layer. Enforced by review and by
   keeping the trimmer warnings clean.
2. Aspect delegates must be pure with respect to the model: no mutation, no I/O.
3. Registration order is irrelevant; concepts resolve lazily and cycles in
   superconcepts are rejected at build time.

## Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Attribute + reflection scanning** | Familiar, but slow, AOT-hostile, and runtime-failing. Also cannot express the declared-language path. |
| **Source generators** | Solves AOT and startup cost, but makes the declared-language path a second implementation and complicates the build for consumers. Possible later as an *additive* convenience on top of the explicit API. |
| **External DSL for concept declarations only** | Would make the built-in stage depend on a parser — the very thing this project removes. Declared languages must go through the model, not a text grammar. |
