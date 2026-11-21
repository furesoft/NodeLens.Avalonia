using Projectional.Core;

namespace Projectional.CSharp;

/// <summary>
/// The C# base language: the concepts a document of C# is made of, and the vocabulary a DSL declares a slot over
/// when a value is not enough and some code is.
/// </summary>
/// <remarks>
/// <para>
/// It is a *base* language in the MPS sense. `jetbrains.mps.baseLanguage` is always available, so a DSL never has to
/// grow an expression language of its own: it declares a slot over <see cref="CSharpConceptKeys.Expression"/> and the
/// user writes a call, a comparison or a literal there. That is why this assembly is registered in the shell's own
/// registry rather than opened from a folder - a base language that had to be opened first would not be one.
/// </para>
/// <para>
/// The subset is chosen by "what does a behaviour, a generator or a constraint body need", not by C# grammar order:
/// three literal kinds, names, member access, calls, the ten binary operators, parentheses and <c>new</c>; blocks,
/// expression statements, local declarations, assignment, <c>if</c>/<c>else</c>, <c>return</c> and <c>throw</c>; and
/// the declarations needed to make the result compile - a namespace, a class, fields, methods and parameters. What is
/// deliberately absent is listed in `docs/architecture/csharp.md`.
/// </para>
/// <para>
/// The order of the declarations is the completion order, so the abstract roots come first and the declarations a
/// user is most likely to want next. <c>Class.members</c> is one list rather than a <c>fields</c> slot beside a
/// <c>methods</c> slot, because C# keeps their order: a field declared after a method stays after it, and two slots
/// could not say that.
/// </para>
/// </remarks>
public static class CSharpLanguage
{
    /// <summary>The language's name, which is what a declared DSL writes in <c>depends on</c>.</summary>
    public const string Name = "CSharp";

    /// <summary>The property holding a type's name, a class's name, or a parameter's.</summary>
    public const string NameSlot = "name";

    /// <summary>The property holding a declaration's visibility.</summary>
    public const string VisibilitySlot = "visibility";

    /// <summary>The property holding whether a class or a method is static.</summary>
    public const string IsStaticSlot = "isStatic";

    /// <summary>The property holding whether a namespace is declared for the rest of the file.</summary>
    public const string IsFileScopedSlot = "isFileScoped";

    /// <summary>The property holding an operator symbol.</summary>
    public const string OperatorSlot = "operator";

    /// <summary>The property holding a literal's text.</summary>
    public const string ValueProperty = "value";

    /// <summary>The property holding the member a <c>MemberAccess</c> reads.</summary>
    public const string MemberSlot = "member";

    /// <summary>The visibility values, as an enumeration so the completion list offers them on arrival.</summary>
    private static PropertyType VisibilityType { get; } = PropertyType.Enum([.. CSharpVisibility.All]);

    /// <summary>The binary operators, as an enumeration for the same reason.</summary>
    private static PropertyType OperatorType { get; } = PropertyType.Enum([.. CSharpBinaryOperator.All]);
    /// <summary>The concepts of the language, in declaration order.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } = Declarations();

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    private static List<ConceptDeclaration> Declarations() =>
    [
        new ConceptDeclaration(
            CSharpConceptKeys.Expression,
            isAbstract: true,
            isRoot: false,
            description: "What a C# expression has in common: it produces a value."),

        new ConceptDeclaration(
            CSharpConceptKeys.Statement,
            isAbstract: true,
            isRoot: false,
            description: "What a C# statement has in common: it runs."),

        new ConceptDeclaration(
            CSharpConceptKeys.Member,
            isAbstract: true,
            isRoot: false,
            description: "What a class holds: a field or a method, in the order it was written."),

        new ConceptDeclaration(
            CSharpConceptKeys.TypeReference,
            [
                new PropertySlot(
                    NameSlot,
                    PropertyType.String,
                    "void",
                    "The type's name, as C# spells it: int, string, System.Date."),
            ],
            nameSlot: NameSlot,
            nameRule: NameRule.Identifier,
            isRoot: false,
            description: "A type, named - nothing more: no arity, no generic declaration."),

        new ConceptDeclaration(
            CSharpConceptKeys.IntLiteral,
            [new PropertySlot(ValueProperty, PropertyType.Integer, "0")],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "A whole number, as written."),

        new ConceptDeclaration(
            CSharpConceptKeys.StringLiteral,
            [new PropertySlot(ValueProperty, PropertyType.String, description: "The text inside the quotes.")],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "A string literal, without its quotes."),

        new ConceptDeclaration(
            CSharpConceptKeys.BoolLiteral,
            [new PropertySlot(ValueProperty, PropertyType.Boolean, "false")],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "true or false."),

        new ConceptDeclaration(
            CSharpConceptKeys.Name,
            [new PropertySlot(NameSlot, PropertyType.String, description: "The identifier, resolved by the compiler and never by us.")],
            superConcepts: [CSharpConceptKeys.Expression],
            nameSlot: NameSlot,
            nameRule: NameRule.Identifier,
            isRoot: false,
            description: "A name, as the compiler will read it in this scope."),

        new ConceptDeclaration(
            CSharpConceptKeys.MemberAccess,
            [
                new ChildSlot("target", CSharpConceptKeys.Expression, Cardinality.One),
                new PropertySlot(MemberSlot, PropertyType.String, description: "The member's name."),
            ],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "target.member"),

        new ConceptDeclaration(
            CSharpConceptKeys.Call,
            [
                new ChildSlot("callee", CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.ArgumentsSlot, CSharpConceptKeys.Expression, Cardinality.Many),
            ],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "A call: the callee, then its arguments."),

        new ConceptDeclaration(
            CSharpConceptKeys.Binary,
            [
                new ChildSlot("left", CSharpConceptKeys.Expression, Cardinality.One),
                new PropertySlot(OperatorSlot, OperatorType, CSharpBinaryOperator.Add),
                new ChildSlot("right", CSharpConceptKeys.Expression, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "left op right"),

        new ConceptDeclaration(
            CSharpConceptKeys.Parenthesized,
            [new ChildSlot("inner", CSharpConceptKeys.Expression, Cardinality.One)],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "An expression with its parentheses kept."),

        new ConceptDeclaration(
            CSharpConceptKeys.New,
            [
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.ArgumentsSlot, CSharpConceptKeys.Expression, Cardinality.Many),
            ],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "new Type(args)"),

        // C# 12's collection expression. It is a concept rather than a call to a list constructor because the target
        // type decides what it becomes, and a projection has no target type to ask - so the tree says "these
        // elements, in this order" and the compiler is the one that names the collection.
        new ConceptDeclaration(
            CSharpConceptKeys.CollectionExpression,
            [new ChildSlot(CSharpConceptKeys.ElementsSlot, CSharpConceptKeys.Expression, Cardinality.Many)],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "A collection expression: [1, 2, 3]."),

        new ConceptDeclaration(
            CSharpConceptKeys.File,
            [new ChildSlot(CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace, Cardinality.One)],
            description: "One document: a namespace and what it holds."),

        // C# 10's file-scoped form is the same declaration with one boolean: `namespace A;` puts everything after it
        // in `A`, and `namespace A { ... }` puts what is between the braces there. One concept rather than two, so
        // switching forms is a keystroke instead of a re-creation of the node.
        new ConceptDeclaration(
            CSharpConceptKeys.Namespace,
            [
                new PropertySlot(NameSlot, PropertyType.String, description: "The namespace, with dots if it is nested."),
                new PropertySlot(
                    IsFileScopedSlot,
                    PropertyType.Boolean,
                    "false",
                    "Whether the namespace is declared for the rest of the file rather than for a block."),
                new ChildSlot(CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class, Cardinality.Many),
            ],
            nameSlot: NameSlot,
            nameRule: NameRule.PascalDot,
            isRoot: false,
            description: "namespace Name { ... }"),

        new ConceptDeclaration(
            CSharpConceptKeys.Class,
            [
                new PropertySlot(VisibilitySlot, VisibilityType, CSharpVisibility.Internal),
                new PropertySlot(IsStaticSlot, PropertyType.Boolean, "false"),
                new PropertySlot(NameSlot, PropertyType.String, description: "The class's name."),
                new ChildSlot(CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Member, Cardinality.Many),
            ],
            nameSlot: NameSlot,
            nameRule: NameRule.Pascal,
            isRoot: false,
            description: "A class: its fields and its methods, in the order they were written."),

        new ConceptDeclaration(
            CSharpConceptKeys.Field,
            [
                new PropertySlot(VisibilitySlot, VisibilityType, CSharpVisibility.Private),
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One),
                new PropertySlot(NameSlot, PropertyType.String, description: "The field's name."),
                new ChildSlot(CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Expression, Cardinality.One, minimum: 0),
            ],
            superConcepts: [CSharpConceptKeys.Member],
            nameSlot: NameSlot,
            nameRule: NameRule.Camel,
            isRoot: false,
            description: "A field, with an initializer when it has one."),

        new ConceptDeclaration(
            CSharpConceptKeys.Method,
            [
                new PropertySlot(VisibilitySlot, VisibilityType, CSharpVisibility.Public),
                new PropertySlot(IsStaticSlot, PropertyType.Boolean, "false"),
                new ChildSlot("returnType", CSharpConceptKeys.TypeReference, Cardinality.One),
                new PropertySlot(
                    NameSlot,
                    PropertyType.String,
                    "todo",
                    "The method's name; the placeholder is what the hint rule talks about."),
                new ChildSlot(CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter, Cardinality.Many),
                new ChildSlot(CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Member],
            nameSlot: NameSlot,
            nameRule: NameRule.Pascal,
            isRoot: false,
            description: "A method: its signature and its body."),

        new ConceptDeclaration(
            CSharpConceptKeys.Parameter,
            [
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One),
                new PropertySlot(NameSlot, PropertyType.String, description: "The parameter's name."),
            ],
            nameSlot: NameSlot,
            nameRule: NameRule.Camel,
            isRoot: false,
            description: "One parameter: a type and a name."),

        new ConceptDeclaration(
            CSharpConceptKeys.Block,
            [new ChildSlot(CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Statement, Cardinality.Many)],
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "A block: the statements between two braces."),

        new ConceptDeclaration(
            CSharpConceptKeys.ExpressionStatement,
            [new ChildSlot("expression", CSharpConceptKeys.Expression, Cardinality.One)],
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "An expression run for its effect."),

        new ConceptDeclaration(
            CSharpConceptKeys.LocalDeclaration,
            [
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One),
                new PropertySlot(NameSlot, PropertyType.String, description: "The local's name."),
                new ChildSlot(CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Expression, Cardinality.One, minimum: 0),
            ],
            nameSlot: NameSlot,
            nameRule: NameRule.Camel,
            isRoot: false,
            description: "Type name = value;"),

        new ConceptDeclaration(
            CSharpConceptKeys.Assignment,
            [
                new ChildSlot("target", CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Expression, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "target = value;"),

        new ConceptDeclaration(
            CSharpConceptKeys.If,
            [
                new ChildSlot(CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.ThenSlot, CSharpConceptKeys.Block, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.OtherwiseSlot, CSharpConceptKeys.Block, Cardinality.One, minimum: 0),
            ],
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "if (condition) { ... } else { ... }"),

        new ConceptDeclaration(
            CSharpConceptKeys.Return,
            [new ChildSlot(CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Expression, Cardinality.One, minimum: 0)],
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "return value;"),

        new ConceptDeclaration(
            CSharpConceptKeys.Throw,
            [new ChildSlot(CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Expression, Cardinality.One)],
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "throw value;"),
    ];
}
