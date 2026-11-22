using Projectional.Core;
using Projectional.Language;

namespace Projectional.LanguageExport;

/// <summary>Writes a language's type rules as the C# that declares them.</summary>
/// <remarks>
/// <para>
/// The aspect an export could not write for longest, because a rule is behaviour: <c>FixedTypeRule</c> answers a
/// written type, <c>ChildTypeRule</c> answers a child's, <c>WidestTypeRule</c> joins two, <c>SwitchTypeRule</c>
/// chooses by a property and <c>EquationsTypeRule</c> states relations for the solver. Those five shapes are public in
/// <c>Projectional.Language</c> precisely so that this file can write them: a language written by hand and a language
/// exported from a folder declare the same five objects, and neither needs a lambda.
/// </para>
/// <para>
/// A named type is hoisted into a field of its own, because the lowering interns one instance per name: two rules that
/// name <c>place</c> mean the same type, and emitting the record twice would give them two.
/// </para>
/// </remarks>
internal static class TypeWriter
{
    /// <summary>Writes the <c>Types</c> member for <paramref name="types"/>.</summary>
    /// <param name="source">The file being written.</param>
    /// <param name="types">The rules the language declared, with the edges it states.</param>
    /// <exception cref="NotSupportedException">
    /// The registry holds a rule or a type this writer has no spelling for, or it declares comparable pairs or
    /// replacements, which are not written yet.
    /// </exception>
    internal static void Write(SourceWriter source, TypeRegistry types)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(types);

        // Named types first: a rule may name one, and the field has to exist before the member that uses it.
        var named = Named(types);

        foreach (var name in named.Keys.Order(StringComparer.Ordinal))
        {
            source.Line($"private static readonly IType {Field(name)} = {Expansion(named[name], named)};");
        }

        if (named.Count > 0)
        {
            source.Blank();
        }

        source.Line("/// <summary>What each concept evaluates to, and the type edges the language states.</summary>");
        source.Line("public static TypeRegistry Types { get; } = TypeRegistry.Empty");

        using (source.Indent())
        {
            foreach (var key in types.Rules.Keys.Order(StringComparer.Ordinal))
            {
                source.Line($".Add(\"{key}\", {Rule(types.Rules[key], named)})");
            }

            source.Line($".WithSubtypes({Subtypes(types.Subtypes, named)})");
            source.Line($".WithComparisons({Comparisons(types.Comparisons, named)})");
            source.Line($".WithReplacements({Replacements(types.Replacements, named)})");
        }

        source.Line(";");
    }

    /// <summary>Every named type the rules mention, keyed by name, with the field each one gets.</summary>
    private static Dictionary<string, NamedType> Named(TypeRegistry types)
    {
        var named = new Dictionary<string, NamedType>(StringComparer.Ordinal);

        foreach (var rule in types.Rules.Values)
        {
            Collect(rule, named);
        }

        foreach (var (subtype, supertype) in types.Subtypes.Edges)
        {
            Collect(subtype, named);
            Collect(supertype, named);
        }

        foreach (var (left, right) in types.Comparisons.Pairs)
        {
            Collect(left, named);
            Collect(right, named);
        }

        foreach (var replacement in types.Replacements)
        {
            Collect(replacement.Left, named);
            Collect(replacement.Right, named);

            foreach (var equation in replacement.Instead)
            {
                Collect(equation.Left, named);
                Collect(equation.Right, named);
            }
        }

        return named;
    }

    /// <summary>Adds the named types inside <paramref name="rule"/> to <paramref name="named"/>.</summary>
    private static void Collect(ITypeRule rule, Dictionary<string, NamedType> named)
    {
        switch (rule)
        {
            case FixedTypeRule fixedRule:
                Collect(fixedRule.Type, named);

                break;

            case SwitchTypeRule choice:
                foreach (var each in choice.Cases.Values)
                {
                    Collect(each, named);
                }

                Collect(choice.Otherwise, named);

                break;

            case EquationsTypeRule equations:
                foreach (var equation in equations.Equations)
                {
                    Collect(equation.Left, named);
                    Collect(equation.Right, named);
                }

                break;

            case ChildTypeRule or WidestTypeRule:
                break;

            default:
                throw new NotSupportedException(
                    $"The type rule of kind '{rule.GetType().Name}' has no export spelling; write it in this project's C#.");
        }
    }

    private static void Collect(IType type, Dictionary<string, NamedType> named)
    {
        switch (type)
        {
            case NamedType named1:
                named[named1.Name] = named1;
                Collect(named1.Expansion, named);

                break;

            case RecordType record:
                foreach (var (_, field) in record.Fields)
                {
                    Collect(field, named);
                }

                break;

            case ListType list:
                Collect(list.Element, named);

                break;

            case OptionalType optional:
                Collect(optional.Inner, named);

                break;

            case JoinType join:
                Collect(join.Left, named);
                Collect(join.Right, named);

                break;

            case MeetType meet:
                Collect(meet.Left, named);
                Collect(meet.Right, named);

                break;

            case PrimitiveType or TypeVariable or UnknownType:
                break;

            default:
                throw new NotSupportedException(
                    $"The type '{type.Name}' of kind '{type.GetType().Name}' has no export spelling; write it in this project's C#.");
        }
    }

    /// <summary>One rule, as the constructor that makes it.</summary>
    private static string Rule(ITypeRule rule, Dictionary<string, NamedType> named) => rule switch
    {
        FixedTypeRule fixedRule => $"new FixedTypeRule({Type(fixedRule.Type, named)})",
        ChildTypeRule child => $"new ChildTypeRule(\"{child.Slot}\")",
        WidestTypeRule widest => $"new WidestTypeRule(\"{widest.Left}\", \"{widest.Right}\")",
        SwitchTypeRule choice => Choice(choice, named),
        EquationsTypeRule equations => $"new EquationsTypeRule([{string.Join(", ", equations.Equations.Select(each => Equation(each, named)))}])",
        _ => throw new NotSupportedException(
            $"The type rule of kind '{rule.GetType().Name}' has no export spelling; write it in this project's C#."),
    };

    private static string Choice(SwitchTypeRule choice, Dictionary<string, NamedType> named)
    {
        var cases = choice.Cases
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"[\"{pair.Key}\"] = {Rule(pair.Value, named)}");

        return $"new SwitchTypeRule(\"{choice.Property}\", new Dictionary<string, ITypeRule> {{ {string.Join(", ", cases)} }}, {Rule(choice.Otherwise, named)})";
    }

    private static string Equation(TypeEquation equation, Dictionary<string, NamedType> named)
    {
        var factory = equation.Kind switch
        {
            TypeEquationKind.Equal => "Equal",
            TypeEquationKind.Subtype => "Subtype",
            TypeEquationKind.Supertype => "Supertype",
            _ => throw new NotSupportedException(
                $"The type equation '{equation.Kind}' has no export spelling; write it in this project's C#."),
        };

        return $"TypeEquation.{factory}({Type(equation.Left, named)}, {Type(equation.Right, named)})";
    }

    /// <summary>The declaration of a named type: its name, and the type that name stands for.</summary>
    /// <remarks>
    /// The name is not decoration - a named type compares and unwraps by it - so the field holds a
    /// <see cref="NamedType"/> and not the expansion alone. Two rules that name <c>place</c> then mean the same type,
    /// which is what the lowering's interning says and what a reader of the generated file would assume.
    /// </remarks>
    private static string Expansion(NamedType type, Dictionary<string, NamedType> named) =>
        $"new NamedType(\"{type.Name}\", {Type(type.Expansion, named)})";

    /// <summary>One type expression, as the constructor that makes it.</summary>
    private static string Type(IType type, Dictionary<string, NamedType> named) => type switch
    {
        PrimitiveType primitive => $"PrimitiveType.Of({Declaration(primitive.Declaration)})",
        NamedType named1 => Field(named1.Name),
        TypeVariable variable => $"new TypeVariable(\"{variable.Identifier}\")",
        UnknownType => "UnknownType.Instance",
        RecordType record => $"new RecordType([{string.Join(", ", record.Fields.Select(field => $"(\"{field.Name}\", {Type(field.Type, named)})"))}])",
        ListType list => $"new ListType({Type(list.Element, named)})",
        OptionalType optional => $"new OptionalType({Type(optional.Inner, named)})",
        JoinType join => $"new JoinType({Type(join.Left, named)}, {Type(join.Right, named)})",
        MeetType meet => $"new MeetType({Type(meet.Left, named)}, {Type(meet.Right, named)})",
        _ => throw new NotSupportedException(
            $"The type '{type.Name}' of kind '{type.GetType().Name}' has no export spelling; write it in this project's C#."),
    };

    /// <summary>A property type, as the static that names it - the five the framework has, and no other.</summary>
    /// <remarks>
    /// An enum property is a <c>PropertyType</c> too, but its symbols are the language's own: writing one here would
    /// need the whole symbol list, and a primitive over an enum is not something a declared type rule can express -
    /// <c>TypeRule.Primitive</c> names <c>string</c>, <c>int</c>, <c>float</c>, <c>bool</c> or <c>date</c>. A registry
    /// built by hand that holds one is refused rather than written wrong.
    /// </remarks>
    private static string Declaration(PropertyType type) => type.Name switch
    {
        "string" => "PropertyType.String",
        "int" => "PropertyType.Integer",
        "bool" => "PropertyType.Boolean",
        "float" => "PropertyType.Float",
        "date" => "PropertyType.Date",
        _ => throw new NotSupportedException(
            $"The primitive type '{type.Name}' has no export spelling; write it in this project's C#."),
    };

    /// <summary>The subtype edges the language stated, on top of the primitives.</summary>
    private static string Subtypes(SubtypeRelation subtypes, Dictionary<string, NamedType> named)
    {
        var expression = "SubtypeRelation.Primitives";

        foreach (var (subtype, supertype) in subtypes.Edges)
        {
            expression += $".Add({Type(subtype, named)}, {Type(supertype, named)})";
        }

        return expression;
    }

    /// <summary>The interchangeable pairs the language stated.</summary>
    private static string Comparisons(ComparisonRelation comparisons, Dictionary<string, NamedType> named)
    {
        var expression = "ComparisonRelation.Empty";

        foreach (var (left, right) in comparisons.Pairs)
        {
            expression += $".Add({Type(left, named)}, {Type(right, named)})";
        }

        return expression;
    }

    /// <summary>The replacements the language stated, each as the claim it rewrites and what it proves instead.</summary>
    private static string Replacements(IReadOnlyList<TypeReplacement> replacements, Dictionary<string, NamedType> named)
    {
        if (replacements.Count == 0)
        {
            return "[]";
        }

        var written = replacements.Select(replacement =>
            $"new TypeReplacement(TypeEquationKind.{Claim(replacement.Claim)}, {Type(replacement.Left, named)}, {Type(replacement.Right, named)}, [{string.Join(", ", replacement.Instead.Select(equation => Equation(equation, named)))}])");

        return $"[{string.Join(", ", written)}]";
    }

    /// <summary>A replacement's claim, which is never a <see cref="TypeEquationKind.Supertype"/>.</summary>
    private static string Claim(TypeEquationKind claim) => claim switch
    {
        TypeEquationKind.Equal => "Equal",
        TypeEquationKind.Subtype => "Subtype",
        _ => throw new NotSupportedException(
            $"The replacement claim '{claim}' has no export spelling; a replacement rewrites equal or subtype claims."),
    };

    /// <summary>The field a named type gets, derived from its name so two exports are the same text.</summary>
    private static string Field(string name) =>
        new([.. name.Where(char.IsLetterOrDigit).Select((character, index) => index == 0 ? char.ToUpperInvariant(character) : character)]);
}
