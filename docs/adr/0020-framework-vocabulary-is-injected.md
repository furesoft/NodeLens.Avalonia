# ADR 0020 — The framework's vocabulary is injected into every registry

- Status: Accepted
- Date: 2026-11-18
- Related: [ADR 0007](0007-reflection-free-language-definition.md),
  [ADR 0014](0014-both-authoring-paths-are-permanent.md), [ADR 0019](0019-toolkit-is-the-language-surface.md),
  [interfaces.md](../architecture/interfaces.md)

## Context

Every language that names what it declares writes the same thing: a string property called 
ame and the
designation that makes it the slot a reference displays. That repetition is not the problem - three lines
of declaration are cheap. The problem is that **a consumer had to trust the convention**: nothing a
declaration says lets a slot be declared over "anything with a name", so a concept that wants to point at
whatever is named has to name a concept of its own language and hope every implementer remembers the
property.

An interface ([interfaces.md](../architecture/interfaces.md)) answers that, and the role it describes -
"something a reader can refer to by name" - is not a language's property. Every language has it, including
the ones a host composes out of borrowed declarations.

So the question this record settles is not *what* Framework.Named is, but **where it comes from**:

1. every language declares it and hopes the keys agree, or
2. the framework declares it, and each language either implements it or does not.

## Decision

ConceptRegistry.Create adds the framework's interface declarations
(FrameworkInterfaces.All, today just Framework.Named) to the set it resolves, before it validates it.

- **Injected, not composed.** A host composes a language by concatenating declaration lists
  ([.. ExpressionsLanguage.ConceptDeclarations, .. ConceptDeclarations]), and the one declaration every
  language must share would be reported as a duplicate by exactly that composition. Injection is what makes
  a host free to concatenate without knowing what is in the lists.
- **The key is namespaced like any other.** Framework.Named, not Named: concept keys are
  Language.Name and the prefix is the namespace, so a bare Named would be the one key without one and
  would collide with the first language that declares a concept by that name.
- **A language may implement it, not declare it.** A declaration using the reserved key is refused with a
  message that says so, because two answers to "what is a name" in one registry is the thing this record
  prevents.
- **Framework.Named is not closed set.** It is the framework's *one* piece of vocabulary, not a catalogue:
  a language still declares its own interfaces, and FrameworkInterfaces.All grows only when the framework
  has a role every language has.

## Consequences

What this costs, and what makes the cost acceptable:

| Consequence | Why it is acceptable |
| --- | --- |
| A registry is no longer literally the list it was given: Concepts starts with the framework's declarations | Anything that counts concepts adds the count of FrameworkInterfaces.All. The order of the *language's* own declarations is untouched, so default fill and completion ranking do not move - the injected declarations are abstract and not offered |
| A code path that re-declares resolved concepts has to skip them | WorkbenchLanguage.Redeclare does, and it has to pass the extension/interface split back too - passing the union would name each interface twice. That is the one place where "the registry is not the list" became visible, and a test covers it |
| A definition document of a language sees the key but cannot declare it | implements accepts it; the reserved-key refusal names the framework |
| LanguageProject.Declarations keeps what the author wrote | "what the author said" and "what the registry resolves" were already two answers; the loader still posts the documents, not the merged set |

Not injecting would have been smaller, and worse: a host would have to remember to add the declaration, and
two hosts that forgot it differently would produce two registries where "named" means two things.

## Alternatives considered

- **Each language declares its own Named.** Then implements Named means "named the way *this* language
  says", a host cannot ask one question of every document it opens, and two languages that both borrow
  Expressions still collide on the declaration they both need.
- **No interface at all: keep the 
ameSlot designation and let consumers guess.** That is what shipped
  before this record. It works until a concept names its property something else, or is named by a value, or
  is written in a language nobody taught the convention.
- **Inject at the toolkit level instead of in ConceptRegistry.Create.** The toolkit aggregates assemblies;
  it does not resolve languages. A registry built by LanguageProject, by a test or by ConceptRegistry
  directly would then be missing the vocabulary, which is precisely the divergence to avoid.