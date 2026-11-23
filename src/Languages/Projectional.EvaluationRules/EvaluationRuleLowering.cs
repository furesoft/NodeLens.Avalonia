using System.Globalization;

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
/// Every body is compiled into a function rather than evaluated when the rule runs, because a body is part of the rule and
/// not part of the document under it: <see cref="EvaluationRuleConceptKeys.Choose"/> holds two bodies, and asking the
/// interpreter about them at run time would be asking it about nodes of the rule model, which have no value.
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
    /// <exception cref="EvaluationRuleException">A rule or body is not one this language can meaningfully build.</exception>
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
    /// <exception cref="EvaluationRuleException">The body is not one this language builds a rule from.</exception>
    private static Func<EvaluationContext, Value> Compile(Node body)
    {
        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Property))
        {
            var slot = SlotOf(body, EvaluationRuleLanguage.SlotSlot);

            return context => context.PropertyValue(slot);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Child))
        {
            var slot = SlotOf(body, EvaluationRuleLanguage.SlotSlot);

            return context => context.EvalChild(slot);
        }

        // The one body that asks a question instead of naming a value: a rule that guards a slot before reading it is the
        // reason it exists, because `child` refuses on an empty slot and nothing but a truth value can stand in a condition.
        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Has))
        {
            var slot = SlotOf(body, EvaluationRuleLanguage.SlotSlot);

            return context =>
            {
                var concept = context.Node.Concept;

                return concept.FindProperty(slot) is not null
                    ? new BoolValue(context.Node.GetOrNull(slot) is { Length: > 0 })
                    : concept.FindChild(slot) is not null
                        ? new BoolValue(context.Node.GetChildren(slot).Count > 0)
                        : concept.FindReference(slot) is null
                            ? throw context.Error($"'{slot}' is not a slot of {concept.Key}, so this rule cannot ask whether it holds anything.")
                            : new BoolValue(context.Node.GetReference(slot) is not null);
            };
        }

        // Delegation: the node the reference points at answers through its own rule, so following a reference composes the
        // same way a child does and the trace names the node the answer actually came from.
        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Reference))
        {
            var slot = SlotOf(body, EvaluationRuleLanguage.SlotSlot);

            return context => context.EvalReference(slot);
        }

        // A slot the concept does not declare answers nothing rather than zero: a count of a slot that does not exist is a
        // rule written against the wrong concept, and zero is a number a document could have meant.
        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Count))
        {
            var slot = SlotOf(body, EvaluationRuleLanguage.SlotSlot);

            return context => context.Node.Concept.FindChild(slot) is null
                ? throw context.Error($"'{slot}' is not a child slot of {context.Node.Concept.Key}, so this rule cannot count it.")
                : new IntValue(context.Node.GetChildren(slot).Count);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Variable))
        {
            var slot = SlotOf(body, EvaluationRuleLanguage.SlotSlot);

            // The name is read from a property of the node, so a node without that property is a rule written against the
            // wrong concept - a sentence rather than the framework's KeyNotFoundException, which names a slot the rule
            // author never typed in this document.
            return context => context.Node.Concept.FindProperty(slot) is null
                ? throw context.Error($"'{slot}' is not a property of {context.Node.Concept.Key}, so this rule cannot say which name it reads.")
                : context.Variable(context.Node.GetOrNull(slot) ?? string.Empty);
        }

        // A name the rule itself bound, read without asking the node: the name is written into the body, so a rule reads the
        // value it named rather than a name the document happens to carry. This is the body `bind` composes with.
        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Bound))
        {
            var name = SlotOf(body, EvaluationRuleLanguage.NameSlot);

            return context => context.Variable(name);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Constant))
        {
            return Constant(body);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Operator))
        {
            return Operator(body);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Choose))
        {
            return Choose(body);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Sum))
        {
            return Sum(body);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Record))
        {
            return Record(body);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Field))
        {
            return Field(body);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.List))
        {
            return List(body);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.First))
        {
            return First(body);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Bind))
        {
            return Bind(body);
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Fail))
        {
            var message = SlotOf(body, EvaluationRuleLanguage.MessageSlot);

            return context => throw context.Error(message);
        }

        throw new EvaluationRuleException(
            body,
            $"'{body.Concept.Key}' is not a body this language builds a rule from.");
    }

    /// <summary>A value written into the rule: its kind decides how the text is read.</summary>
    /// <exception cref="EvaluationRuleException">The text is not a value of the kind it claims.</exception>
    private static Func<EvaluationContext, Value> Constant(Node body)
    {
        var kind = SlotOf(body, "kind");
        var text = SlotOf(body, EvaluationRuleLanguage.ValueSlot);

        return kind switch
        {
            "int" when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) =>
                _ => new IntValue(number),
            "bool" when bool.TryParse(text, out var flag) => _ => new BoolValue(flag),
            "string" => _ => new StringValue(text),
            _ => throw new EvaluationRuleException(
                body,
                $"'{text}' is not a {kind} value, so this constant cannot answer with anything."),
        };
    }

    /// <summary>An operator body: the symbol it names or the one its node holds, and the values of its operand bodies.</summary>
    /// <remarks>
    /// <para>
    /// The symbol written on the body wins, which is what lets a rule compare against a constant - <c>points &gt;= 50</c> - on a
    /// node that has no operator property at all. Leaving it empty is how a rule whose operator varies with the node is
    /// written, and the property name then says where to read it.
    /// </para>
    /// <para>
    /// A logical symbol short-circuits here exactly as it does in the hand-written expression rules, through
    /// <see cref="OperatorSemantics.ShortCircuits"/>: a declared language and a shipped one must not disagree about
    /// whether the right operand ran.
    /// </para>
    /// </remarks>
    private static Func<EvaluationContext, Value> Operator(Node body)
    {
        var written = body.GetOrNull(EvaluationRuleLanguage.SymbolSlot);
        var from = body.GetOrNull(EvaluationRuleLanguage.OperatorSlot);
        var left = Compile(Required(body, EvaluationRuleLanguage.LeftSlot));
        var right = body.GetChild(EvaluationRuleLanguage.RightSlot) is { } second ? Compile(second) : null;

        string SymbolOf(EvaluationContext context) => written is { Length: > 0 }
            ? written
            : from is { Length: > 0 } slot && context.Node.GetOrNull(slot) is { Length: > 0 } held
                ? held
                : throw context.Error("this operator body names no symbol and its node holds none.");

        return right is null
            ? context => OperatorSemantics.ApplyUnary(context, SymbolOf(context), left(context))
            : context =>
            {
                var symbol = SymbolOf(context);
                var first = left(context);

                return OperatorSemantics.ShortCircuits(symbol, first)
                    ? first
                    : OperatorSemantics.Apply(context, symbol, first, right(context));
            };
    }

    /// <summary>A choice: the condition's own value decides which body answers.</summary>
    private static Func<EvaluationContext, Value> Choose(Node body)
    {
        var condition = Compile(Required(body, EvaluationRuleLanguage.ConditionSlot));
        var then = Compile(Required(body, EvaluationRuleLanguage.ThenSlot));
        var otherwise = body.GetChild(EvaluationRuleLanguage.OtherwiseSlot) is { } arm ? Compile(arm) : null;

        return context =>
        {
            if (condition(context) is not BoolValue flag)
            {
                throw context.Error("the condition of a choose body must be a truth value.");
            }

            return flag.Content
                ? then(context)
                : otherwise is null
                    ? throw context.Error("the condition of this choose body is false and it has no otherwise body.")
                    : otherwise(context);
        };
    }

    /// <summary>A record value, built from the named values the rule states.</summary>
    /// <exception cref="EvaluationRuleException">Two fields share a name, or one states no value.</exception>
    private static Func<EvaluationContext, Value> Record(Node body)
    {
        var fields = body.GetChildren(EvaluationRuleLanguage.FieldsSlot)
            .Select(field => (Name: SlotOf(field, EvaluationRuleLanguage.NameSlot), Answer: Compile(Required(field, EvaluationRuleLanguage.ValueSlot))))
            .ToArray();

        // Refused while the rule is built rather than while it runs: two fields of one name are a field written twice, and which
        // value survived would be the order of a dictionary rather than a decision the author made.
        if (fields.GroupBy(field => field.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            throw new EvaluationRuleException(body, $"this record names the field '{duplicate.Key}' twice.");
        }

        return context => new RecordValue(
            fields.Select(field => new KeyValuePair<string, Value>(field.Name, field.Answer(context))));
    }

    /// <summary>One named value of a record the body answers.</summary>
    /// <remarks>
    /// Two refusals, because they are two different corrections: a value that is not a record is a rule that asked the wrong
    /// body, and a name the record does not hold is a rule and a record that disagree about a field.
    /// </remarks>
    private static Func<EvaluationContext, Value> Field(Node body)
    {
        var name = SlotOf(body, EvaluationRuleLanguage.NameSlot);
        var from = Compile(Required(body, EvaluationRuleLanguage.FromSlot));

        return context => from(context) is RecordValue record
            ? record.Field(name) ?? throw context.Error($"the record this rule reads holds no field '{name}'.")
            : throw context.Error($"'{name}' is a field of a record, and this body answers with something else.");
    }

    /// <summary>Every entry of a child slot, as one list.</summary>
    private static Func<EvaluationContext, Value> List(Node body)
    {
        var slot = SlotOf(body, EvaluationRuleLanguage.SlotSlot);

        return context => context.Node.Concept.FindChild(slot) is null
            ? throw context.Error($"'{slot}' is not a child slot of {context.Node.Concept.Key}, so this rule cannot list it.")
            : new ListValue(context.Node.GetChildren(slot).Select(context.Eval));
    }

    /// <summary>The total of what the entries of a slot answer, each asked for itself.</summary>
    /// <remarks>
    /// Accumulated in a <see cref="long"/> and range-checked per step, because a wrapped <see cref="int"/> is a wrong answer
    /// rather than a refusal - the same reason a power refuses a result it cannot represent.
    /// </remarks>
    private static Func<EvaluationContext, Value> Sum(Node body)
    {
        var over = SlotOf(body, EvaluationRuleLanguage.OverSlot);
        var each = Compile(Required(body, EvaluationRuleLanguage.EachSlot));

        return context =>
        {
            long total = 0;

            foreach (var entry in context.Node.GetChildren(over))
            {
                if (each(context.For(entry)) is not IntValue number)
                {
                    throw context.Error($"the entries of '{over}' must answer with whole numbers to be summed, and one answered with something else.");
                }

                total += number.Content;

                if (total is > int.MaxValue or < int.MinValue)
                {
                    throw context.Error($"the total of '{over}' is beyond the whole numbers this framework holds.");
                }
            }

            return new IntValue((int)total);
        };
    }

    /// <summary>The first entry that applies, answered by the body beside the condition, for that entry.</summary>
    /// <remarks>
    /// Both bodies are asked with the entry as their subject, which is the whole point: "the first rule that applies" is
    /// useless unless what it answers is read from the rule that applied. See <see cref="EvaluationContext.For"/>.
    /// </remarks>
    private static Func<EvaluationContext, Value> First(Node body)
    {
        var over = SlotOf(body, EvaluationRuleLanguage.OverSlot);
        var when = Compile(Required(body, EvaluationRuleLanguage.WhenSlot));
        var then = Compile(Required(body, EvaluationRuleLanguage.ThenSlot));
        var otherwise = body.GetChild(EvaluationRuleLanguage.OtherwiseSlot) is { } arm ? Compile(arm) : null;

        return context =>
        {
            foreach (var entry in context.Node.GetChildren(over))
            {
                var subject = context.For(entry);

                if (when(subject) is not BoolValue flag)
                {
                    throw context.Error($"the condition of a first body must be a truth value, and '{over}' answered with something else.");
                }

                if (flag.Content)
                {
                    return then(subject);
                }
            }

            return otherwise is null
                ? throw context.Error($"no entry of '{over}' applies, and this rule says nothing about that case.")
                : otherwise(context);
        };
    }

    /// <summary>A name bound to one body while another answers, which is a value computed once.</summary>
    private static Func<EvaluationContext, Value> Bind(Node body)
    {
        var name = SlotOf(body, EvaluationRuleLanguage.NameSlot);
        var value = Compile(Required(body, EvaluationRuleLanguage.ValueSlot));
        var rest = Compile(Required(body, EvaluationRuleLanguage.BodySlot));

        return context => rest(context.With(new Dictionary<string, Value>(StringComparer.Ordinal)
        {
            [name] = value(context),
        }));
    }

    /// <summary>The child a body needs, which a half-written rule does not have.</summary>
    private static Node Required(Node body, string slot) =>
        body.GetChild(slot)
            ?? throw new EvaluationRuleException(body, $"'{body.Concept.Key}' needs a child in '{slot}'.");

    /// <summary>The slot or text a body names, read while the rule is built.</summary>
    private static string SlotOf(Node body, string slot) =>
        body.GetOrNull(slot) is { Length: > 0 } value
            ? value
            : throw new EvaluationRuleException(body, $"'{body.Concept.Key}' needs a value in '{slot}'.");

    /// <summary>What the body computes, in the one sentence a documentation page shows beside the concept.</summary>
    /// <remarks>
    /// Holdings are written as the slot name the rule reads rather than the value it happened to have, so the sentence holds
    /// for every document the rule applies to. A body whose sentence needs more than one clause gets one: a reader of the
    /// page is choosing whether to trust the rule with their model.
    /// </remarks>
    private static string Describe(Node body)
    {
        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Property))
        {
            return $"The value of its {SlotOf(body, EvaluationRuleLanguage.SlotSlot)} property, read through its declared type.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Child))
        {
            return $"The value of its {SlotOf(body, EvaluationRuleLanguage.SlotSlot)} child.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Has))
        {
            return $"Whether its {SlotOf(body, EvaluationRuleLanguage.SlotSlot)} slot holds anything.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Count))
        {
            return $"How many entries its {SlotOf(body, EvaluationRuleLanguage.SlotSlot)} child slot holds.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Reference))
        {
            return $"The value of the node its {SlotOf(body, EvaluationRuleLanguage.SlotSlot)} reference points at.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Variable))
        {
            return $"The value the run binds to the name in its {SlotOf(body, EvaluationRuleLanguage.SlotSlot)} property.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Bound))
        {
            return $"The value the enclosing bind introduced under the name {SlotOf(body, EvaluationRuleLanguage.NameSlot)}.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Constant))
        {
            return $"The {SlotOf(body, "kind")} value written into the rule.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Operator))
        {
            var applied = body.GetOrNull(EvaluationRuleLanguage.SymbolSlot) is { Length: > 0 } written
                ? $"Its {written} operator"
                : "The operator of the node";

            return body.GetChild(EvaluationRuleLanguage.RightSlot) is null
                ? $"{applied} applied to the value of its left operand."
                : $"{applied} applied to the values of its two operands.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Choose))
        {
            return "The body whose arm the condition chooses.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Sum))
        {
            return $"The total of what each of its {SlotOf(body, EvaluationRuleLanguage.OverSlot)} entries answers.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Record))
        {
            return "A record of the named values this rule states.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Field))
        {
            return $"The field {SlotOf(body, EvaluationRuleLanguage.NameSlot)} of the record its body answers with.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.List))
        {
            return $"The values of its {SlotOf(body, EvaluationRuleLanguage.SlotSlot)} entries, as one list.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.First))
        {
            var otherwise = body.GetChild(EvaluationRuleLanguage.OtherwiseSlot) is null
                ? " and refuses when none does"
                : " and answers with the other body when none does";

            return $"The answer of the first entry of its {SlotOf(body, EvaluationRuleLanguage.OverSlot)} children whose "
                + $"condition holds{otherwise}.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Bind))
        {
            return $"The second body, with the name {SlotOf(body, EvaluationRuleLanguage.NameSlot)} bound to the first one's value.";
        }

        if (body.Concept.IsKindOf(EvaluationRuleConceptKeys.Fail))
        {
            return "A refusal, with the sentence this rule states.";
        }

        return string.Empty;
    }

    /// <summary><paramref name="root"/> and everything under it, in document order.</summary>
    /// <remarks>
    /// Local rather than <c>NodeWalk</c>, which lives one layer up: this package is a language definition and may reference
    /// only the language layer, so the few lines it needs are written here instead of pulling the interpreter in. Child
    /// slots only, because asking a property slot for children is a question the node refuses.
    /// </remarks>
    private static IEnumerable<Node> Walk(Node root)
    {
        yield return root;

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
}
