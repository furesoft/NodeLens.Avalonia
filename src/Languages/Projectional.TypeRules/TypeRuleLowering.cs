using Projectional.Core;
using Projectional.Language;
using Projectional.Semantics;

namespace Projectional.TypeRules;

/// <summary>
/// Turns a written type rule into the <see cref="ITypeRule"/> the semantics layer runs.
/// </summary>
/// <remarks>
/// <para>
/// The lowering is where a declaration becomes behaviour, and it is the only place that knows both. It
/// validates what a compiler would have validated - a rule names a concept, a fixed body names a type the
/// framework has, a child body names a slot, a switch names a property.
/// </para>
/// <para>
/// The two questions only the concept registry can answer - whether the concept exists, and whether the slot
/// a child body names is a child of it - are asked of the registry when the caller hands one over, and
/// skipped when it does not. Asking is not duplicating: the knowledge stays in the registry, and the
/// lowering only refuses what the registry already says is wrong. That matters because a child body naming
/// a slot the concept does not declare as a child is not a slow answer but a crash: the type read asks the
/// node for a child slot that is not there. Refusing it at load, where the author is looking, is the only
/// place that failure can be a message instead of a stack trace.
/// </para>
/// <para>
/// A body that cannot be lowered is a <see cref="ConceptDefinitionException"/> at load, not a rule that
/// silently answers <see cref="UnknownType"/>: a language whose type rules are wrong should fail when it
/// is loaded, where the author is looking, rather than produce findings nobody can explain.
/// </para>
/// </remarks>
public static class TypeRuleLowering
{
    /// <summary>The rules a language's type documents declare, as a registry.</summary>
    /// <param name="documents">
    /// The roots of those documents: one <see cref="TypeRuleConceptKeys.Rule"/> or one
    /// <see cref="TypeRuleConceptKeys.Named"/> per document, in file order.
    /// </param>
    /// <param name="concepts">
    /// The language's concepts, when the caller has them, so a rule naming a concept that does not exist or a
    /// child body naming a slot the concept does not declare as a child is refused here instead of crashing
    /// the type read. <see langword="null"/> skips the graph check, which is what a caller that only wants
    /// the lowering's own validations asks for.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="documents"/> is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">A root is neither a rule nor a named type, a rule cannot be lowered, or a rule names a concept or child slot the language does not have.</exception>
    public static TypeRegistry CreateRegistry(IEnumerable<Node> documents, ConceptRegistry? concepts = null)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var roots = documents as IReadOnlyList<Node> ?? [.. documents];

        if (concepts is not null)
        {
            ValidateGraph(roots, concepts);
        }

        var aliases = Aliases(roots);
        var registry = TypeRegistry.Empty;

        foreach (var root in roots)
        {
            switch (root.Concept.Key)
            {
                case TypeRuleConceptKeys.Rule:
                    // A complaint states a diagnostic, not a type, so it contributes nothing to the registry:
                    // reporting is a separate answer from typing, and a concept's type is not changed by it.
                    if (IsComplaint(root))
                    {
                        break;
                    }

                    var conceptKey = Required(root, TypeRuleLanguage.ConceptKeySlot);

                    registry = registry.Add(conceptKey, LowerRule(root, conceptKey, aliases));
                    break;

                case TypeRuleConceptKeys.Named:
                    break;

                default:
                    throw new ConceptDefinitionException(
                        $"'{root.Concept.Key}' is not a type definition; a types document holds one '{TypeRuleConceptKeys.Rule}' or one '{TypeRuleConceptKeys.Named}'.");
            }
        }

        return registry;
    }

    /// <summary>The complaints a language's type documents declare, as the constraints that report them.</summary>
    /// <param name="documents">
    /// The documents <see cref="CreateRegistry"/> reads: a complaint states a diagnostic rather than a type,
    /// so it contributes nothing to the registry and everything here.
    /// </param>
    /// <remarks>
    /// <para>
    /// A complaint is not a type, so lowering it changes nothing about what a concept evaluates to: it adds a
    /// finding beside the type. That is why the two products are separate - a language that declares a
    /// complaint still has the type its other rules answer, and a language that declares none is unaffected.
    /// </para>
    /// <para>
    /// The message and the severity are read here, once, and captured by the constraint: a complaint is a
    /// sentence about the concept, not about the node, so the same text is reported wherever the concept
    /// appears. A document that is neither a rule nor a named type is refused by <see cref="CreateRegistry"/>,
    /// which a language loads first, so this walk skips what it does not handle.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="documents"/> is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">A complaint is missing its concept key or its message.</exception>
    public static ConstraintSet CreateComplaints(IEnumerable<Node> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var complaints = ConstraintSet.Empty;

        foreach (var root in documents)
        {
            if (root.Concept.Key != TypeRuleConceptKeys.Rule || !IsComplaint(root))
            {
                continue;
            }

            var conceptKey = Required(root, TypeRuleLanguage.ConceptKeySlot);
            var body = RequiredChild(root, TypeRuleLanguage.BodySlot);

            complaints = complaints.Add(
                conceptKey,
                new ComplaintConstraint(Required(body, TypeRuleLanguage.MessageSlot), Severity(body)));
        }

        return complaints;
    }

    /// <summary>
    /// Refuses a rule that names a concept the language does not declare, or a child body that names a slot
    /// the concept does not declare as a child.
    /// </summary>
    /// <remarks>
    /// The knowledge stays in the registry - this asks it and refuses what it says is wrong. A child body
    /// naming a reference slot, or a slot that does not exist, is not a slow answer: the type read asks the
    /// node for a child slot that is not there and throws, so the only place to make it a message is here.
    /// A complaint is checked too: its concept key decides where the diagnostic appears, and a key no concept
    /// answers would leave the complaint silently never firing.
    /// </remarks>
    private static void ValidateGraph(IReadOnlyList<Node> roots, ConceptRegistry concepts)
    {
        foreach (var root in roots)
        {
            if (root.Concept.Key != TypeRuleConceptKeys.Rule)
            {
                continue;
            }

            var conceptKey = Required(root, TypeRuleLanguage.ConceptKeySlot);

            if (!concepts.TryGet(conceptKey, out var concept))
            {
                throw new ConceptDefinitionException(
                    $"The type rule names the concept '{conceptKey}', which the language does not declare.");
            }

            if (root.GetChild(TypeRuleLanguage.BodySlot) is not { Concept.Key: TypeRuleConceptKeys.Child } body)
            {
                continue;
            }

            var slot = Required(body, TypeRuleLanguage.SlotSlot);

            if (concept.FindChild(slot) is null)
            {
                throw new ConceptDefinitionException(
                    $"The type rule for '{conceptKey}' names the slot '{slot}', which '{conceptKey}' declares as no child slot.");
            }
        }
    }

    /// <summary>
    /// The types the language declares under a name, which are documents of their own beside the rules.
    /// </summary>
    /// <remarks>
    /// A named type is a document rather than a second list inside one, so that a language's types aspect keeps
    /// exactly one shape a reader has to learn: the rules, and the names they may use. Duplicates are a
    /// load-time error here because nothing later could choose between them.
    /// </remarks>
    private static Dictionary<string, Node> Aliases(IReadOnlyList<Node> roots)
    {
        Dictionary<string, Node> aliases = new(StringComparer.Ordinal);

        foreach (var root in roots)
        {
            if (root.Concept.Key != TypeRuleConceptKeys.Named)
            {
                continue;
            }

            var name = Required(root, TypeRuleLanguage.NameSlot);

            if (!aliases.TryAdd(name, root))
            {
                throw new ConceptDefinitionException(
                    $"The language declares the type '{name}' twice, so nothing says which one a rule means.");
            }
        }

        return aliases;
    }

    private static ITypeRule LowerRule(Node rule, string conceptKey, Dictionary<string, Node> aliases)
    {
        if (rule.GetChild(TypeRuleLanguage.BodySlot) is not { } body)
        {
            throw new ConceptDefinitionException(
                $"The type rule for '{conceptKey}' has no body, so it answers nothing.");
        }

        return LowerBody(body, conceptKey, aliases);
    }

    private static ITypeRule LowerBody(Node body, string conceptKey, Dictionary<string, Node> aliases) => body.Concept.Key switch
    {
        TypeRuleConceptKeys.Fixed => new FixedRule(LowerType(RequiredChild(body, TypeRuleLanguage.TypeSlot), conceptKey, aliases, [])),
        TypeRuleConceptKeys.Child => new ChildRule(Required(body, TypeRuleLanguage.SlotSlot)),
        TypeRuleConceptKeys.Switch => LowerSwitch(body, conceptKey, aliases),
        TypeRuleConceptKeys.Equations => LowerEquations(body, conceptKey, aliases),
        _ => throw new ConceptDefinitionException(
            $"The type rule for '{conceptKey}' has a body of '{body.Concept.Key}', which is not a kind of body."),
    };

    /// <summary>
    /// The type <paramref name="expr"/> writes, or a load-time error.
    /// </summary>
    /// <remarks>
    /// A type expression is static by construction - it has no way to ask about the rule's node - so this is
    /// a structural walk and nothing more. Which is why a record field's type cannot be "the type of my
    /// left child": there is no node there to ask.
    /// </remarks>
    private static IType LowerType(
        Node expr,
        string conceptKey,
        Dictionary<string, Node> aliases,
        HashSet<string> expanding,
        Dictionary<string, TypeVariable>? variables = null) => expr.Concept.Key switch
    {
        TypeRuleConceptKeys.Primitive => Primitive(Required(expr, TypeRuleLanguage.NameSlot), conceptKey),
        TypeRuleConceptKeys.Record => LowerRecord(expr, conceptKey, aliases, expanding, variables),
        TypeRuleConceptKeys.List => new ListType(
            LowerType(RequiredChild(expr, TypeRuleLanguage.ElementSlot), conceptKey, aliases, expanding, variables)),
        TypeRuleConceptKeys.Optional => new OptionalType(
            LowerType(RequiredChild(expr, TypeRuleLanguage.InnerSlot), conceptKey, aliases, expanding, variables)),
        TypeRuleConceptKeys.Ref => Alias(Required(expr, TypeRuleLanguage.NameSlot), conceptKey, aliases, expanding, variables),
        TypeRuleConceptKeys.Join => new JoinType(
            LowerType(RequiredChild(expr, TypeRuleLanguage.LeftSlot), conceptKey, aliases, expanding, variables),
            LowerType(RequiredChild(expr, TypeRuleLanguage.RightSlot), conceptKey, aliases, expanding, variables)),
        TypeRuleConceptKeys.Meet => new MeetType(
            LowerType(RequiredChild(expr, TypeRuleLanguage.LeftSlot), conceptKey, aliases, expanding, variables),
            LowerType(RequiredChild(expr, TypeRuleLanguage.RightSlot), conceptKey, aliases, expanding, variables)),
        TypeRuleConceptKeys.TypeVariable => Variable(expr, conceptKey, variables),
        _ => throw new ConceptDefinitionException(
            $"The type rule for '{conceptKey}' names a type of '{expr.Concept.Key}', which is not a kind of type."),
    };

    /// <summary>
    /// The variable <paramref name="expr"/> names, one per name within the rule that states it.
    /// </summary>
    /// <remarks>
    /// A variable is an identity, not a name: two nodes writing <c>?element</c> in one rule must lower to the
    /// same <see cref="TypeVariable"/>, or the equations over them would relate two unrelated unknowns and the
    /// solve would answer nothing. The table therefore lives for one rule's lowering and is keyed by the name
    /// the author wrote.
    /// </remarks>
    private static TypeVariable Variable(Node expr, string conceptKey, Dictionary<string, TypeVariable>? variables)
    {
        var name = Required(expr, TypeRuleLanguage.NameSlot);

        if (variables is null)
        {
            throw new ConceptDefinitionException(
                $"The type rule for '{conceptKey}' writes the type variable '{name}' outside a body that states equations, so nothing constrains it.");
        }

        if (!variables.TryGetValue(name, out var variable))
        {
            variable = new TypeVariable(name);
            variables.Add(name, variable);
        }

        return variable;
    }

    /// <summary>
    /// The type a declared name stands for, expanded where it is used.
    /// </summary>
    /// <remarks>
    /// <paramref name="expanding"/> is the chain of names currently being expanded, which is how a
    /// declaration written in terms of itself is caught: <c>type A = [A]</c> is not a recursive type but a
    /// definition with no base, and answering it would recurse forever. A name may of course be used twice
    /// side by side - only a name on its own expansion path is the mistake.
    /// </remarks>
    private static NamedType Alias(
        string name,
        string conceptKey,
        Dictionary<string, Node> aliases,
        HashSet<string> expanding,
        Dictionary<string, TypeVariable>? variables = null)
    {
        if (!aliases.TryGetValue(name, out var declaration))
        {
            throw new ConceptDefinitionException(
                $"The type rule for '{conceptKey}' names the type '{name}', which the types model does not declare.");
        }

        if (!expanding.Add(name))
        {
            throw new ConceptDefinitionException(
                $"The type '{name}' is declared in terms of itself ({string.Join(" -> ", expanding)} -> {name}), so it means nothing.");
        }

        try
        {
            return new NamedType(
                name,
                LowerType(RequiredChild(declaration, TypeRuleLanguage.TypeSlot), conceptKey, aliases, expanding, variables));
        }
        finally
        {
            expanding.Remove(name);
        }
    }

    private static RecordType LowerRecord(
        Node expr,
        string conceptKey,
        Dictionary<string, Node> aliases,
        HashSet<string> expanding,
        Dictionary<string, TypeVariable>? variables = null)
    {
        List<(string Name, IType Type)> fields = [];

        foreach (var field in expr.GetChildren(TypeRuleLanguage.FieldsSlot))
        {
            fields.Add((
                Required(field, TypeRuleLanguage.NameSlot),
                LowerType(RequiredChild(field, TypeRuleLanguage.TypeSlot), conceptKey, aliases, expanding, variables)));
        }

        try
        {
            return new RecordType(fields);
        }
        catch (ArgumentException exception)
        {
            // A record refuses one field name twice. A language error has to read as one, not as an
            // argument exception naming a parameter the author never saw.
            throw new ConceptDefinitionException(
                $"The type rule for '{conceptKey}' declares a record {exception.Message}", exception);
        }
    }

    private static SwitchRule LowerSwitch(Node body, string conceptKey, Dictionary<string, Node> aliases)
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

            if (!cases.TryAdd(value, LowerBody(caseBody, conceptKey, aliases)))
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

        return new SwitchRule(property, cases, LowerBody(otherwise, conceptKey, aliases));
    }

    /// <summary>
    /// A body that states relations instead of answering a type.
    /// </summary>
    /// <remarks>
    /// The variables are lowered once per rule, so <c>?a</c> written in two equations is one variable. That is
    /// the whole reason a body of this kind exists: a relation between two unknowns says nothing unless the
    /// unknowns are shared, and sharing them is a property of the document, not of the solver.
    /// </remarks>
    private static EquationsRule LowerEquations(Node body, string conceptKey, Dictionary<string, Node> aliases)
    {
        Dictionary<string, TypeVariable> variables = new(StringComparer.Ordinal);
        List<TypeEquation> equations = [];

        foreach (var equation in body.GetChildren(TypeRuleLanguage.EquationsSlot))
        {
            equations.Add(LowerEquation(equation, conceptKey, aliases, variables));
        }

        if (equations.Count == 0)
        {
            throw new ConceptDefinitionException(
                $"The type rule for '{conceptKey}' states equations but states none, so it means nothing.");
        }

        return new EquationsRule(equations);
    }

    private static TypeEquation LowerEquation(
        Node node,
        string conceptKey,
        Dictionary<string, Node> aliases,
        Dictionary<string, TypeVariable> variables)
    {
        var relation = Required(node, TypeRuleLanguage.RelationSlot);
        var left = LowerType(RequiredChild(node, TypeRuleLanguage.LeftSlot), conceptKey, aliases, [], variables);
        var right = LowerType(RequiredChild(node, TypeRuleLanguage.RightSlot), conceptKey, aliases, [], variables);

        if (left is TypeVariable variable && ReferenceEquals(variable, right))
        {
            throw new ConceptDefinitionException(
                $"The type rule for '{conceptKey}' states that {variable.Name} relates to itself, which every type satisfies and no type decides.");
        }

        return relation switch
        {
            "==" => TypeEquation.Equal(left, right),
            "<:" => TypeEquation.Subtype(left, right),
            ":>" => TypeEquation.Supertype(left, right),
            _ => throw new ConceptDefinitionException(
                $"The type rule for '{conceptKey}' states a relation of '{relation}', which is not one of ==, <: or :>."),
        };
    }

    /// <summary>
    /// The primitive type <paramref name="name"/> spells, or a load-time error.
    /// </summary>
    /// <remarks>
    /// Only the four primitives are nameable. An enumeration is a type a language declares, and a rule
    /// that named one would have to be resolved against that language's registry - which is the declared
    /// stage's job, not this one's. Saying so at load is better than answering <c>Unknown</c> forever.
    /// </remarks>
    private static PrimitiveType Primitive(string name, string conceptKey) => name switch
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

    private static Node RequiredChild(Node node, string slot) =>
        node.GetChild(slot) ?? throw new ConceptDefinitionException(
            $"A '{node.Concept.Key}' is missing its '{slot}', so it cannot be lowered.");

    /// <summary>Whether <paramref name="rule"/>'s body is a complaint, which states a diagnostic rather than a type.</summary>
    private static bool IsComplaint(Node rule) =>
        rule.GetChild(TypeRuleLanguage.BodySlot)?.Concept.Key == TypeRuleConceptKeys.Complain;

    /// <summary>How much a complaint matters, as the severity its spelling names.</summary>
    private static DiagnosticSeverity Severity(Node body) =>
        body.GetOrNull(TypeRuleLanguage.SeveritySlot) switch
        {
            "warning" => DiagnosticSeverity.Warning,
            "hint" => DiagnosticSeverity.Hint,
            _ => DiagnosticSeverity.Error,
        };

    /// <summary>A complaint: a message with a severity, reported wherever the concept the rule names appears.</summary>
    private sealed class ComplaintConstraint(string message, DiagnosticSeverity severity) : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Warning:
                    context.Warning(message);
                    break;

                case DiagnosticSeverity.Hint:
                    context.Hint(message);
                    break;

                default:
                    context.Error(message);
                    break;
            }
        }
    }

    /// <summary>A type written out, which is the same whatever the node holds.</summary>
    private sealed class FixedRule(IType type) : ITypeRule
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

    /// <summary>A body that states relations, which the solver satisfies instead of the rule answering a type.</summary>
    /// <remarks>
    /// <see cref="TypeFor"/> answers <see cref="UnknownType"/> because a rule that states relations has not said
    /// what type the node is - it has said what must hold for the types around it. Answering anything else would
    /// be the guess the solver exists to avoid, and the solver reads the equations from
    /// <see cref="ITypeRule.EquationsFor"/> precisely so the two answers stay separate.
    /// </remarks>
    private sealed class EquationsRule(IReadOnlyList<TypeEquation> equations) : ITypeRule
    {
        public IType TypeFor(TypeContext context) => UnknownType.Instance;

        public IEnumerable<TypeEquation> EquationsFor(TypeContext context) => equations;
    }
}
