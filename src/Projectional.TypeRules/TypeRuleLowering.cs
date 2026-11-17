using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.TypeRules;

/// <summary>
/// Turns a written type rule into the <see cref="ITypeRule"/> the semantics layer runs.
/// </summary>
/// <remarks>
/// <para>
/// The lowering is where a declaration becomes behaviour, and it is the only place that knows both. It
/// validates what a compiler would have validated - a rule names a concept, a fixed body names a type the
/// framework has, a child body names a slot, a switch names a property - and it deliberately does not
/// validate the graph: whether the concept exists and whether the slot is a child of it are questions the
/// language's own registry answers, and a second validator here would drift from it.
/// </para>
/// <para>
/// A body that cannot be lowered is a <see cref="ConceptDefinitionException"/> at load, not a rule that
/// silently answers <see cref="UnknownType"/>: a language whose type rules are wrong should fail when it
/// is loaded, where the author is looking, rather than produce findings nobody can explain.
/// </para>
/// </remarks>
public static class TypeRuleLowering
{
    /// <summary>The rules <paramref name="document"/> declares, as a registry.</summary>
    /// <param name="document">A node of <see cref="TypeRuleConceptKeys.Rules"/>.</param>
    /// <exception cref="ConceptDefinitionException">A rule cannot be lowered.</exception>
    public static TypeRegistry CreateRegistry(Node document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var registry = TypeRegistry.Empty;

        foreach (var rule in document.GetChildren(TypeRuleLanguage.RulesSlot))
        {
            var conceptKey = Required(rule, TypeRuleLanguage.ConceptKeySlot);

            registry = registry.Add(conceptKey, LowerRule(rule, conceptKey));
        }

        return registry;
    }

    private static ITypeRule LowerRule(Node rule, string conceptKey)
    {
        if (rule.GetChild(TypeRuleLanguage.BodySlot) is not { } body)
        {
            throw new ConceptDefinitionException(
                $"The type rule for '{conceptKey}' has no body, so it answers nothing.");
        }

        return LowerBody(body, conceptKey);
    }

    private static ITypeRule LowerBody(Node body, string conceptKey) => body.Concept.Key switch
    {
        TypeRuleConceptKeys.Fixed => new FixedRule(NamedType(Required(body, TypeRuleLanguage.TypeNameSlot), conceptKey)),
        TypeRuleConceptKeys.Child => new ChildRule(Required(body, TypeRuleLanguage.SlotSlot)),
        TypeRuleConceptKeys.Switch => LowerSwitch(body, conceptKey),
        _ => throw new ConceptDefinitionException(
            $"The type rule for '{conceptKey}' has a body of '{body.Concept.Key}', which is not a kind of body."),
    };

    private static SwitchRule LowerSwitch(Node body, string conceptKey)
    {
        var property = Required(body, TypeRuleLanguage.PropertySlot);
        var cases = new Dictionary<string, ITypeRule>(StringComparer.Ordinal);

        foreach (var branch in body.GetChildren(TypeRuleLanguage.CaseSlot))
        {
            var value = Required(branch, TypeRuleLanguage.ValueSlot);

            if (branch.GetChild(TypeRuleLanguage.BodySlot) is not { } caseBody)
            {
                throw new ConceptDefinitionException(
                    $"The case '{value}' of the type rule for '{conceptKey}' has no body.");
            }

            if (!cases.TryAdd(value, LowerBody(caseBody, conceptKey)))
            {
                throw new ConceptDefinitionException(
                    $"The type rule for '{conceptKey}' switches on '{property}' and declares '{value}' twice.");
            }
        }

        if (body.GetChild(TypeRuleLanguage.OtherwiseSlot) is not { } otherwise)
        {
            throw new ConceptDefinitionException(
                $"The type rule for '{conceptKey}' switches on '{property}' but says nothing about any other value.");
        }

        return new SwitchRule(property, cases, LowerBody(otherwise, conceptKey));
    }

    /// <summary>
    /// The primitive type <paramref name="name"/> spells, or a load-time error.
    /// </summary>
    /// <remarks>
    /// Only the four primitives are nameable. An enumeration is a type a language declares, and a rule
    /// that named one would have to be resolved against that language's registry - which is the declared
    /// stage's job, not this one's. Saying so at load is better than answering <c>Unknown</c> forever.
    /// </remarks>
    private static PrimitiveType NamedType(string name, string conceptKey) => name switch
    {
        "string" => PrimitiveType.Of(PropertyType.String),
        "int" => PrimitiveType.Of(PropertyType.Integer),
        "bool" => PrimitiveType.Of(PropertyType.Boolean),
        "date" => PrimitiveType.Of(PropertyType.Date),
        _ => throw new ConceptDefinitionException(
            $"The type rule for '{conceptKey}' names the type '{name}', which is not one of string, int, bool or date."),
    };

    private static string Required(Node node, string slot) =>
        node.GetOrNull(slot) is { Length: > 0 } value
            ? value
            : throw new ConceptDefinitionException(
                $"A '{node.Concept.Key}' is missing its '{slot}', so it cannot be lowered.");

    /// <summary>A type named outright.</summary>
    private sealed class FixedRule(PrimitiveType type) : ITypeRule
    {
        public IType TypeFor(TypeContext context) => type;
    }

    /// <summary>The type of one of the node's children.</summary>
    private sealed class ChildRule(string slot) : ITypeRule
    {
        public IType TypeFor(TypeContext context) => context.TypeOfChild(slot);
    }

    /// <summary>A choice between bodies, decided by a property's value.</summary>
    private sealed class SwitchRule(
        string property,
        IReadOnlyDictionary<string, ITypeRule> cases,
        ITypeRule otherwise) : ITypeRule
    {
        public IType TypeFor(TypeContext context)
        {
            var value = context.Node.GetOrNull(property);

            return value is not null && cases.TryGetValue(value, out var rule)
                ? rule.TypeFor(context)
                : otherwise.TypeFor(context);
        }
    }
}
