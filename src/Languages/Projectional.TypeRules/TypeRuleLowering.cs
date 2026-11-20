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
    /// <remarks>
    /// A registry carries both halves of a types aspect: the rules that answer what a node's type is, and the
    /// subtype edges the language declared. A <see cref="TypeRuleConceptKeys.Supertypes"/> document states one
    /// edge, and the relation they build is what a consumer hands to a <see cref="TypeSolver"/> or
    /// <see cref="TypeLattice"/>, so a language's own edges are used instead of only the framework's primitives.
    /// </remarks>
    /// <param name="documents">
    /// The roots of those documents: one <see cref="TypeRuleConceptKeys.Rule"/>, one
    /// <see cref="TypeRuleConceptKeys.Named"/> or one <see cref="TypeRuleConceptKeys.Supertypes"/> per document,
    /// in file order.
    /// </param>
    /// <param name="concepts">
    /// The language's concepts, when the caller has them, so a rule naming a concept that does not exist or a
    /// child body naming a slot the concept does not declare as a child is refused here instead of crashing
    /// the type read. <see langword="null"/> skips the graph check, which is what a caller that only wants
    /// the lowering's own validations asks for.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="documents"/> is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">A root is not a kind of type definition, a rule cannot be lowered, a rule names a concept or child slot the language does not have, or a declared edge cannot be held.</exception>
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
        var subtypes = SubtypeRelation.Primitives;
        var comparisons = ComparisonRelation.Empty;
        List<TypeReplacement> replacements = [];

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

                case TypeRuleConceptKeys.Supertypes:
                    subtypes = Declare(subtypes, root, aliases);
                    break;

                case TypeRuleConceptKeys.Comparable:
                    comparisons = LowerComparable(comparisons, root, aliases);
                    break;

                case TypeRuleConceptKeys.Replacement:
                    replacements.Add(LowerReplacement(root, aliases));
                    break;

                default:
                    throw new ConceptDefinitionException(
                        $"'{root.Concept.Key}' is not a type definition; a types document holds one '{TypeRuleConceptKeys.Rule}', one '{TypeRuleConceptKeys.Named}', one '{TypeRuleConceptKeys.Supertypes}', one '{TypeRuleConceptKeys.Comparable}' or one '{TypeRuleConceptKeys.Replacement}'.");
            }
        }

        return registry
            .WithSubtypes(subtypes)
            .WithComparisons(comparisons)
            .WithReplacements(replacements);
    }

    /// <summary>
    /// <paramref name="subtypes"/> with the edge <paramref name="declaration"/> states, or a load-time error.
    /// </summary>
    /// <remarks>
    /// The relation refuses an edge that mentions <c>unknown</c>, because "unknown may stand in for a type" is how a
    /// guess becomes a proof. That refusal is a load-time error in the author's vocabulary here rather than an
    /// argument exception from the relation's own: the author wrote a declaration, so the message names the
    /// declaration and quotes the edge.
    /// </remarks>
    private static SubtypeRelation Declare(SubtypeRelation subtypes, Node declaration, DeclaredTypes aliases)
    {
        var subtype = LowerType(RequiredChild(declaration, TypeRuleLanguage.SubtypeSlot), TypeRuleConceptKeys.Supertypes, aliases, []);
        var supertype = LowerType(RequiredChild(declaration, TypeRuleLanguage.SupertypeSlot), TypeRuleConceptKeys.Supertypes, aliases, []);

        try
        {
            return subtypes.Add(subtype, supertype);
        }
        catch (ArgumentException error)
        {
            throw new ConceptDefinitionException(
                $"The declared edge '{subtype}' <: '{supertype}' cannot be held: {error.Message}", error);
        }
    }

    /// <summary>
    /// <paramref name="comparisons"/> with the pair <paramref name="declaration"/> states, or a load-time error.
    /// </summary>
    /// <remarks>
    /// The comparison relation refuses a type it cannot relate and refuses <c>unknown</c>, for the reason the edges do;
    /// both are turned into a load-time error in the author's vocabulary rather than thrown as an argument exception.
    /// </remarks>
    private static ComparisonRelation LowerComparable(
        ComparisonRelation comparisons,
        Node declaration,
        DeclaredTypes aliases)
    {
        var left = LowerType(RequiredChild(declaration, TypeRuleLanguage.LeftSlot), TypeRuleConceptKeys.Comparable, aliases, []);
        var right = LowerType(RequiredChild(declaration, TypeRuleLanguage.RightSlot), TypeRuleConceptKeys.Comparable, aliases, []);

        try
        {
            return comparisons.Add(left, right);
        }
        catch (ArgumentException error)
        {
            throw new ConceptDefinitionException(
                $"The declared comparison between '{left}' and '{right}' cannot be held: {error.Message}", error);
        }
    }

    /// <summary>
    /// The rewrite <paramref name="declaration"/> states, or a load-time error.
    /// </summary>
    /// <remarks>
    /// The two patterns and the relations to prove instead share one variable table, which is the whole mechanism:
    /// <c>?element</c> written in a pattern and again in a relation is one variable, exactly as it is twice in one
    /// equations body. Three refusals are placed here rather than left to a rule that never matches: a pattern that
    /// is "either of these" or "both of these" (no single shape can claim it), a relation naming a variable no
    /// pattern binds (nothing would decide what it stands for), and a claim that is not one of the two it may be.
    /// </remarks>
    private static TypeReplacement LowerReplacement(Node declaration, DeclaredTypes aliases)
    {
        var claim = Required(declaration, TypeRuleLanguage.ClaimSlot);
        Dictionary<string, TypeVariable> variables = new(StringComparer.Ordinal);

        var left = LowerType(RequiredChild(declaration, TypeRuleLanguage.LeftSlot), TypeRuleConceptKeys.Replacement, aliases, [], variables);
        var right = LowerType(RequiredChild(declaration, TypeRuleLanguage.RightSlot), TypeRuleConceptKeys.Replacement, aliases, [], variables);

        RequirePattern(left, TypeRuleLanguage.LeftSlot);
        RequirePattern(right, TypeRuleLanguage.RightSlot);

        List<TypeEquation> instead = [];

        foreach (var equation in declaration.GetChildren(TypeRuleLanguage.InsteadSlot))
        {
            instead.Add(LowerEquation(equation, TypeRuleConceptKeys.Replacement, aliases, variables));
        }

        HashSet<TypeVariable> bound = [.. VariablesIn(left), .. VariablesIn(right)];

        foreach (var variable in instead.SelectMany(each => VariablesIn(each.Left).Concat(VariablesIn(each.Right))))
        {
            if (!bound.Contains(variable))
            {
                throw new ConceptDefinitionException(
                    $"The replacement relates the variable {variable.Name}, which neither of its patterns binds, so nothing decides what it stands for.");
            }
        }

        return new TypeReplacement(
            claim switch
            {
                "==" => TypeEquationKind.Equal,
                "<:" => TypeEquationKind.Subtype,
                _ => throw new ConceptDefinitionException(
                    $"The replacement claims a relation of '{claim}', which is not one of == or <:."),
            },
            left,
            right,
            instead);
    }

    /// <summary>Refuses <paramref name="pattern"/> unless a single shape can claim it.</summary>
    /// <remarks>
    /// "Either of these" is a type a value may conform to, not a shape a claim can be about: matching it would have
    /// to mean "both sides", and a rule that never matches is worse than one refused where it is written.
    /// </remarks>
    private static void RequirePattern(IType pattern, string slot)
    {
        switch (pattern)
        {
            case JoinType:
                throw new ConceptDefinitionException(
                    $"The replacement's '{slot}' pattern is a join, which is 'either of these' rather than one shape, so no claim can be about it.");

            case MeetType:
                throw new ConceptDefinitionException(
                    $"The replacement's '{slot}' pattern is a meet, which is 'both of these' at once rather than one shape, so no claim can be about it.");

            case ListType list:
                RequirePattern(list.Element, slot);
                break;

            case OptionalType optional:
                RequirePattern(optional.Inner, slot);
                break;

            case RecordType record:
                foreach (var (_, field) in record.Fields)
                {
                    RequirePattern(field, slot);
                }

                break;
        }
    }

    /// <summary>Every variable <paramref name="type"/> mentions, at any depth.</summary>
    private static IEnumerable<TypeVariable> VariablesIn(IType type) => type switch
    {
        TypeVariable variable => [variable],
        ListType list => VariablesIn(list.Element),
        OptionalType optional => VariablesIn(optional.Inner),
        RecordType record => record.Fields.SelectMany(field => VariablesIn(field.Type)),
        JoinType join => VariablesIn(join.Left).Concat(VariablesIn(join.Right)),
        MeetType meet => VariablesIn(meet.Left).Concat(VariablesIn(meet.Right)),
        _ => [],
    };

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
                new ComplaintConstraint(
                    Required(body, TypeRuleLanguage.MessageSlot),
                    Severity(body),
                    Condition(body)));
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

            switch (root.GetChild(TypeRuleLanguage.BodySlot))
            {
                case { Concept.Key: TypeRuleConceptKeys.Child } child:
                    RequireChildSlot(concept, conceptKey, Required(child, TypeRuleLanguage.SlotSlot));
                    break;

                // A condition reads the same two child slots a child body does, so it is refused the same way: a
                // condition naming a reference slot would throw when the check runs, where the author is not looking.
                case { Concept.Key: TypeRuleConceptKeys.Complain } complaint
                    when complaint.GetChild(TypeRuleLanguage.WhenSlot) is { } when:
                    RequireChildSlot(concept, conceptKey, Required(when, TypeRuleLanguage.LeftSlot));
                    RequireChildSlot(concept, conceptKey, Required(when, TypeRuleLanguage.RightSlot));
                    break;
            }
        }
    }

    /// <summary>Refuses <paramref name="slot"/> unless <paramref name="concept"/> declares it as a child slot.</summary>
    private static void RequireChildSlot(Concept concept, string conceptKey, string slot)
    {
        if (concept.FindChild(slot) is null)
        {
            throw new ConceptDefinitionException(
                $"The type rule for '{conceptKey}' names the slot '{slot}', which '{conceptKey}' declares as no child slot.");
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
    private static DeclaredTypes Aliases(IReadOnlyList<Node> roots)
    {
        Dictionary<string, Node> declarations = new(StringComparer.Ordinal);

        foreach (var root in roots)
        {
            if (root.Concept.Key != TypeRuleConceptKeys.Named)
            {
                continue;
            }

            var name = Required(root, TypeRuleLanguage.NameSlot);

            if (!declarations.TryAdd(name, root))
            {
                throw new ConceptDefinitionException(
                    $"The language declares the type '{name}' twice, so nothing says which one a rule means.");
            }
        }

        return new DeclaredTypes(declarations);
    }

    /// <summary>
    /// The types the language declares under a name, and the instances a lowering hands out for them.
    /// </summary>
    /// <remarks>
    /// A name is a classifier, and the language's answer to "are two declarations with one name one type?" is yes:
    /// it says so by handing out one instance per name. That matters because the relation compares named types by
    /// identity - a name reaches the relation only through the instance it was given - so a declared edge over a
    /// name and a rule writing that name must be given the same instance, or the edge joins nothing. The expansion
    /// is therefore computed once per name, which also means a name written twice costs one expansion.
    /// </remarks>
    private sealed class DeclaredTypes(Dictionary<string, Node> declarations)
    {
        /// <summary>The instance each name stands for, once it has been lowered.</summary>
        public Dictionary<string, NamedType> Interned { get; } = new(StringComparer.Ordinal);

        /// <summary>The document that declares <paramref name="name"/>, if the language declares it.</summary>
        public bool TryGetDeclaration(string name, out Node declaration) =>
            declarations.TryGetValue(name, out declaration!);
    }

    private static ITypeRule LowerRule(Node rule, string conceptKey, DeclaredTypes aliases)
    {
        if (rule.GetChild(TypeRuleLanguage.BodySlot) is not { } body)
        {
            throw new ConceptDefinitionException(
                $"The type rule for '{conceptKey}' has no body, so it answers nothing.");
        }

        return LowerBody(body, conceptKey, aliases);
    }

    private static ITypeRule LowerBody(Node body, string conceptKey, DeclaredTypes aliases) => body.Concept.Key switch
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
        DeclaredTypes aliases,
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
        DeclaredTypes aliases,
        HashSet<string> expanding,
        Dictionary<string, TypeVariable>? variables = null)
    {
        if (aliases.Interned.TryGetValue(name, out var interned))
        {
            return interned;
        }

        if (!aliases.TryGetDeclaration(name, out var declaration))
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
            var named = new NamedType(
                name,
                LowerType(RequiredChild(declaration, TypeRuleLanguage.TypeSlot), conceptKey, aliases, expanding, variables));

            aliases.Interned[name] = named;

            return named;
        }
        finally
        {
            expanding.Remove(name);
        }
    }

    private static RecordType LowerRecord(
        Node expr,
        string conceptKey,
        DeclaredTypes aliases,
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

    private static SwitchRule LowerSwitch(Node body, string conceptKey, DeclaredTypes aliases)
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
    private static EquationsRule LowerEquations(Node body, string conceptKey, DeclaredTypes aliases)
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
        DeclaredTypes aliases,
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

    /// <summary>The two child slots a complaint's condition compares, or <see langword="null"/> when it always reports.</summary>
    private static (string Left, string Right)? Condition(Node body) =>
        body.GetChild(TypeRuleLanguage.WhenSlot) is { } when
            ? (Required(when, TypeRuleLanguage.LeftSlot), Required(when, TypeRuleLanguage.RightSlot))
            : null;

    /// <summary>How much a complaint matters, as the severity its spelling names.</summary>
    private static DiagnosticSeverity Severity(Node body) =>
        body.GetOrNull(TypeRuleLanguage.SeveritySlot) switch
        {
            "warning" => DiagnosticSeverity.Warning,
            "hint" => DiagnosticSeverity.Hint,
            _ => DiagnosticSeverity.Error,
        };

    /// <summary>
    /// A complaint: a message with a severity, reported wherever the concept the rule names appears.
    /// </summary>
    /// <remarks>
    /// A condition makes the complaint report only when the types of two child slots are not interchangeable - the
    /// "operation is not applicable to these operands" case. The types are read at check time, from the context's
    /// own type layer, so a condition costs nothing until a check runs and follows whatever the language's rules and
    /// declared edges say. Without a condition the complaint always reports, which is what a statement about the
    /// concept itself is.
    /// </remarks>
    private sealed class ComplaintConstraint(
        string message,
        DiagnosticSeverity severity,
        (string Left, string Right)? when) : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            if (when is { } condition && IsComparable(context, condition.Left, condition.Right))
            {
                return;
            }

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

        /// <summary>
        /// Whether the two child slots' types are interchangeable, or <see langword="true"/> when either slot is
        /// empty.
        /// </summary>
        /// <remarks>
        /// A condition asks about two operands, so without them there is nothing to say and the complaint stays
        /// silent - reporting "not comparable" for a missing operand would blame the author for a node that is
        /// merely incomplete. Comparability is asked of the type itself first, then of the declared edges and the
        /// declared comparisons: a name compares by its expansion, a language that declares two types interchangeable
        /// is honoured, and so is one that declares a pair interchangeable without making either a subtype of the
        /// other.
        /// </remarks>
        private static bool IsComparable(ConstraintContext context, string left, string right)
        {
            if (context.Node.GetChild(left) is not { } leftNode
                || context.Node.GetChild(right) is not { } rightNode)
            {
                return true;
            }

            var leftType = context.TypeOf(leftNode);
            var rightType = context.TypeOf(rightNode);

            return leftType.IsComparableTo(rightType)
                || context.Types.Subtypes.IsComparableTo(leftType, rightType)
                || context.Types.Comparisons.IsComparableTo(leftType, rightType);
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
