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

    /// <summary>The property holding a unary operator.</summary>
    public const string UnaryOperatorSlot = "operator";

    /// <summary>The property holding which assignment an <c>Assignment</c> is.</summary>
    public const string AssignmentOperatorSlot = "operator";

    /// <summary>The slot holding what a ternary takes when its condition holds.</summary>
    public const string WhenTrueSlot = "whenTrue";

    /// <summary>The slot holding what a ternary takes when it does not.</summary>
    public const string WhenFalseSlot = "whenFalse";

    /// <summary>The unary operators, as an enumeration for the same reason the binary ones are.</summary>
    private static PropertyType UnaryType { get; } = PropertyType.Enum([.. CSharpUnaryOperator.All]);

    /// <summary>The assignment forms, as an enumeration.</summary>
    private static PropertyType AssignmentType { get; } = PropertyType.Enum([.. CSharpAssignmentOperator.All]);

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

        // An interface and not an abstract concept, because a field, a parameter and a local share no slots - only the
        // fact that a name may point at them. `Name.target` is declared over this, which is what makes "which
        // declaration does this name mean" a question the model asks and a scope provider answers.
        new ConceptDeclaration(
            CSharpConceptKeys.NamedDeclaration,
            isAbstract: true,
            isInterface: true,
            isRoot: false,
            description: "Something a name can refer to: a field, a parameter or a local."),

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

        // A reference, not a property: the name *is* the declaration it points at, which is what lets a rename follow
        // it, go-to-definition find it, and a scope provider answer which declarations are visible here. The text a
        // user typed while nothing resolved is the reference's own resolveInfo, so there is one truth and not two.
        new ConceptDeclaration(
            CSharpConceptKeys.Name,
            [new ReferenceSlot(CSharpConceptKeys.TargetSlot, CSharpConceptKeys.NamedDeclaration)],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "A name, referring to the declaration it means."),

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

        // One operator in front of one operand. The operator is an enumeration so the completion list offers the four
        // the language knows, and so a symbol the language cannot generate is not a value the property can hold.
        new ConceptDeclaration(
            CSharpConceptKeys.Unary,
            [
                new PropertySlot(UnaryOperatorSlot, UnaryType, CSharpUnaryOperator.Not),
                new ChildSlot(CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Expression, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "One operator in front of one operand: !ready."),

        // A ternary is three expressions and two symbols, which is why it is a concept and not a property on Binary.
        new ConceptDeclaration(
            CSharpConceptKeys.Conditional,
            [
                new ChildSlot(CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot(WhenTrueSlot, CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot(WhenFalseSlot, CSharpConceptKeys.Expression, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "condition ? whenTrue : whenFalse"),
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
            interfaces: [CSharpConceptKeys.NamedDeclaration],
            nameSlot: NameSlot,
            nameRule: NameRule.Camel,
            isRoot: false,
            description: "A field, with an initializer when it has one."),

        // A constructor is a member without a return type, and its name is the class's - the language does not enforce
        // that, because a name is the author's and a check that refused a mismatch would report a half-typed class.
        new ConceptDeclaration(
            CSharpConceptKeys.Constructor,
            [
                new PropertySlot(VisibilitySlot, PropertyType.String, CSharpVisibility.Public),
                new PropertySlot(NameSlot, PropertyType.String, description: "The name, which a constructor shares with its class."),
                new ChildSlot(CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter, Cardinality.Many),
                new ChildSlot(CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Member],
            nameSlot: NameSlot,
            nameRule: NameRule.Pascal,
            isRoot: false,
            description: "A constructor: the parameters and the body that make an instance."),

        // An auto-property, which is the one form that needs no accessor body: `{ get; set; }`, or `{ get; }` when the
        // setter is withheld. A property with written accessors is a later decision - it needs a third shape and the
        // boolean that chooses between the three, and that is a design of its own.
        new ConceptDeclaration(
            CSharpConceptKeys.Property,
            [
                new PropertySlot(VisibilitySlot, PropertyType.String, CSharpVisibility.Public),
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One),
                new PropertySlot(NameSlot, PropertyType.String, description: "The property's name."),
                new PropertySlot(
                    CSharpConceptKeys.HasSetterSlot,
                    PropertyType.Boolean,
                    "true",
                    "Whether the property offers a setter; without one it is `{ get; }`."),
            ],
            superConcepts: [CSharpConceptKeys.Member],
            interfaces: [CSharpConceptKeys.NamedDeclaration],
            nameSlot: NameSlot,
            nameRule: NameRule.Pascal,
            isRoot: false,
            description: "An auto-property: { get; set; }"),
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
            interfaces: [CSharpConceptKeys.NamedDeclaration],
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
            // A local declaration is a statement: without this it could not stand in a block, which is the only place a
            // local is ever written.
            superConcepts: [CSharpConceptKeys.Statement],
            interfaces: [CSharpConceptKeys.NamedDeclaration],
            nameSlot: NameSlot,
            nameRule: NameRule.Camel,
            isRoot: false,
            description: "Type name = value;"),

        new ConceptDeclaration(
            CSharpConceptKeys.Assignment,
            [
                new ChildSlot("target", CSharpConceptKeys.Expression, Cardinality.One),
                new PropertySlot(AssignmentOperatorSlot, AssignmentType, CSharpAssignmentOperator.Assign),
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

        // A loop's body is a Block and its condition an Expression, so `while` needs no concept of its own for either.
        new ConceptDeclaration(
            CSharpConceptKeys.While,
            [
                new ChildSlot(CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "while (condition) { ... }"),

        // `foreach` declares its element where it is used, which is why the type and the name are slots of the loop
        // rather than a local the body could see: the language has no scope for a declaration that is not a node.
        new ConceptDeclaration(
            CSharpConceptKeys.ForEach,
            [
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One),
                new PropertySlot(NameSlot, PropertyType.String, description: "The name each element is bound to."),
                new ChildSlot(CSharpConceptKeys.SourceSlot, CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Statement],
            nameSlot: NameSlot,
            nameRule: NameRule.Camel,
            isRoot: false,
            description: "foreach (Type name in source) { ... }"),

        new ConceptDeclaration(
            CSharpConceptKeys.Break,
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "break;"),

        new ConceptDeclaration(
            CSharpConceptKeys.Continue,
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "continue;"),
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
