using Projectional.Core;
using Projectional.Language;

namespace Projectional.EvaluationRules;

/// <summary>Turns the models a language writes its meaning in into the registry the interpreter runs.</summary>
/// <remarks>
/// <para>
/// The twin of <c>TypeRuleLowering</c>, and the same bargain: every body becomes one <see cref="DelegateEvaluationRule"/>,
/// which is exactly the delegate the C# path writes by hand. Nothing here evaluates anything - it reads a model and builds
/// the rule - so a rule can be edited, diffed, merged and documented like every other aspect.
/// </para>
/// <para>
/// A rule for a concept the model does not declare is a failure rather than a silent entry: the key is the whole link
/// between the two, and a typo would otherwise be a concept that quietly has no value.
/// </para>
/// </remarks>
public static class EvaluationRuleLowering
{
    /// <summary>The rules <paramref name="documents"/> declare, as one registry.</summary>
    /// <param name="documents">The models written in <see cref="EvaluationRuleLanguage"/>.</param>
    /// <param name="concepts">The registry the keys are checked against, or <see langword="null"/> to check nothing.</param>
    /// <returns>The registry, in document order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="documents"/> is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">A rule names a concept the registry does not declare.</exception>
    /// <exception cref="EvaluationRuleException">A body is not one this language can meaningfully build.</exception>
    public static EvaluationRegistry CreateRegistry(IEnumerable<Node> documents, ConceptRegistry? concepts = null)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var registry = EvaluationRegistry.Empty;

        foreach (var document in documents)
        {
            foreach (var rule in Walk(document))
            {
                if (!rule.Concept.IsKindOf(EvaluationRuleConceptKeys.Rule))
                {
                    continue;
                }

                var key = rule.GetOrNull(EvaluationRuleLanguage.ConceptKeySlot);

                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new EvaluationRuleException(rule, "a rule must name the concept it answers for.");
                }

                if (concepts is not null && !concepts.TryGet(key, out _))
                {
                    throw new ConceptDefinitionException(
                        $"a rule names the concept '{key}', which this language does not declare.");
                }

                registry = registry.Add(key, Lower(rule));
            }
        }

        return registry;
    }

    /// <summary>The rule one model declares, as the interpreter runs it.</summary>
    /// <exception cref="EvaluationRuleException">The body is missing, or is not one this language builds.</exception>
    private static DelegateEvaluationRule Lower(Node rule)
    {
        var body = rule.GetChild(EvaluationRuleLanguage.BodySlot)
            ?? throw new EvaluationRuleException(rule, "a rule must say what it answers with.");

        return new DelegateEvaluationRule(Compile(body), Describe(body));
    }

    /// <summary>One body as the function that answers with its value.</summary>
    /// <remarks>
    /// Recursive rather than a single delegate per body, because a choose body holds two bodies of its own: those arms are
    /// part of the rule, not part of the document being evaluated, so they have to be built now and closed over - asking the
    /// interpreter to evaluate them later would ask it about a node of the rule model, which has no value.
    /// </remarks>
    /// <exception cref="EvaluationRuleException">The body is not one this language builds a rule from.</exception>
    private static Func<EvaluationContext, Value> Compile(Node body)
    {
        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Property))
        {
            return context => context.PropertyValue(Required(body, EvaluationRuleLanguage.SlotSlot, context));
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Child))
        {
            return context => context.EvalChild(Required(body, EvaluationRuleLanguage.SlotSlot, context));
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Variable))
        {
            return context => context.Variable(Required(body, EvaluationRuleLanguage.SlotSlot, context));
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Operator))
        {
            return context => ApplyOperator(body, context);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Choose))
        {
            var condition = SlotOf(body, EvaluationRuleLanguage.ConditionSlot);
            var then = Compile(Arm(body, EvaluationRuleLanguage.ThenSlot));
            var otherwise = Compile(Arm(body, EvaluationRuleLanguage.OtherwiseSlot));

            return context =>
            {
                var chosen = context.EvalChild(condition) is BoolValue flag
                    ? flag.Content
                    : throw context.Error($"'{condition}' must hold a truth value to decide this rule.");

                return chosen ? then(context) : otherwise(context);
            };
        }

        throw new EvaluationRuleException(
            body,
            $"'{body.Concept.Key}' is not a body this language builds a rule from.");
    }

    /// <summary>One arm of a choose body, which is a body of its own.</summary>
    /// <exception cref="EvaluationRuleException">The arm is missing, so the rule cannot answer.</exception>
    private static Node Arm(Node body, string slot) =>
        body.GetChild(slot)
            ?? throw new EvaluationRuleException(body, $"the '{slot}' arm of a choose body is missing.");

    /// <summary>The slot a body names, read while the rule is built.</summary>
    /// <exception cref="EvaluationRuleException">The body names no slot.</exception>
    private static string SlotOf(Node body, string slot) =>
        body.GetOrNull(slot) is { Length: > 0 } value
            ? value
            : throw new EvaluationRuleException(body, $"'{body.Concept.Key}' needs a value in '{slot}'.");

    /// <summary>An operator body: the symbol from its property, the values from its one or two child slots.</summary>
    private static Value ApplyOperator(Node body, EvaluationContext context)
    {
        var symbol = Required(body, EvaluationRuleLanguage.OperatorSlot, context);
        var left = Required(body, EvaluationRuleLanguage.LeftSlot, context);
        var right = body.GetOrNull(EvaluationRuleLanguage.RightSlot);

        // An empty right operand is how a unary operator is written: one child, one symbol, one answer.
        return string.IsNullOrWhiteSpace(right)
            ? OperatorSemantics.ApplyUnary(
                context,
                context.Node.GetOrNull(symbol) ?? throw context.Error($"'{symbol}' holds no operator."),
                context.EvalChild(left))
            : OperatorSemantics.Apply(
                context,
                context.Node.GetOrNull(symbol) ?? throw context.Error($"'{symbol}' holds no operator."),
                context.EvalChild(left),
                context.EvalChild(right));
    }

    /// <summary>A choose body: the condition's own value decides which of the two bodies answers.</summary>
    private static Value Choose(Node body, EvaluationContext context)
    {
        var condition = Required(body, EvaluationRuleLanguage.ConditionSlot, context);
        var chosen = context.EvalChild(condition) is BoolValue flag
            ? flag.Content
            : throw context.Error($"'{condition}' must hold a truth value to decide this rule.");

        var arm = body.GetChild(chosen ? EvaluationRuleLanguage.ThenSlot : EvaluationRuleLanguage.OtherwiseSlot)
            ?? throw context.Error($"the {(chosen ? "then" : "otherwise")} body of this rule is missing.");

        return context.Eval(arm);
    }

    /// <summary>The property value a body needs, which a half-written rule does not have.</summary>
    private static string Required(Node body, string slot, EvaluationContext context) =>
        body.GetOrNull(slot) is { Length: > 0 } value
            ? value
            : throw context.Error($"'{body.Concept.Key}' needs a value in '{slot}'.");

    /// <summary>What the body computes, in the one sentence a documentation page shows beside the concept.</summary>
    /// <remarks>
    /// Holdings are written as <c>{slot}</c> so the sentence names the slot the rule reads rather than the value it happened
    /// to have in this document, and hold for every document the rule applies to.
    /// </remarks>
    private static string Describe(Node body) => body.Concept switch
    {
        var concept when concept.IsKindOf(EvaluationRuleConceptKeys.Property) =>
            $"The value of its {Named(body, EvaluationRuleLanguage.SlotSlot)} property, read through its declared type.",
        var concept when concept.IsKindOf(EvaluationRuleConceptKeys.Child) =>
            $"The value of its {Named(body, EvaluationRuleLanguage.SlotSlot)} child.",
        var concept when concept.IsKindOf(EvaluationRuleConceptKeys.Variable) =>
            $"The value the run binds to the name in its {Named(body, EvaluationRuleLanguage.SlotSlot)} property.",
        var concept when concept.IsKindOf(EvaluationRuleConceptKeys.Operator) =>
            body.GetOrNull(EvaluationRuleLanguage.RightSlot) is { Length: > 0 } right
                ? $"Its {Named(body, EvaluationRuleLanguage.OperatorSlot)} operator applied to the values of its "
                    + $"{Named(body, EvaluationRuleLanguage.LeftSlot)} and {right} children."
                : $"Its {Named(body, EvaluationRuleLanguage.OperatorSlot)} operator applied to the value of its "
                    + $"{Named(body, EvaluationRuleLanguage.LeftSlot)} child.",
        var concept when concept.IsKindOf(EvaluationRuleConceptKeys.Choose) =>
            $"One of two bodies, decided by the truth value of its {Named(body, EvaluationRuleLanguage.ConditionSlot)} child.",
        _ => string.Empty,
    };

    /// <summary><paramref name="root"/> and everything under it, in document order.</summary>
    /// <remarks>
    /// Local rather than <c>NodeWalk</c>, which lives one layer up: this package is a language definition and may reference
    /// only the language layer, so the three lines it needs are written here instead of pulling the interpreter in.
    /// </remarks>
    private static IEnumerable<Node> Walk(Node root)
    {
        yield return root;

        // Child slots only: asking GetChildren about a property is a question the node refuses, and a rule document holds
        // both kinds in the same slot list.
        foreach (var declaration in root.Concept.Slots.OfType<ChildSlot>())
        {
            foreach (var child in root.GetChildren(declaration.Name))
            {
                foreach (var nested in Walk(child))
                {
                    yield return nested;
                }
            }
        }
    }

    /// <summary>A slot name as a sentence names it, read from the model so a page cannot drift from the rule.</summary>
    private static string Named(Node body, string slot) =>
        body.GetOrNull(slot) is { Length: > 0 } value ? value : $"<{slot}>";
}
