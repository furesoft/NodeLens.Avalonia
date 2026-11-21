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

    /// <summary>The property holding whether a local declaration lets the initializer decide its type.</summary>
    public const string IsVarSlot = "isVar";

    /// <summary>The property holding an operator symbol.</summary>
    public const string OperatorSlot = "operator";

    /// <summary>The property holding a literal's text.</summary>
    public const string ValueProperty = "value";

    /// <summary>The property holding the member a <c>MemberAccess</c> reads.</summary>
    public const string MemberSlot = "member";

    /// <summary>The property holding whether a unary operator follows its operand.</summary>
    public const string IsPostfixSlot = "isPostfix";

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

        // The suffix is part of the notation rather than part of the property, so the value stays a number the model
        // can reason about and the `L` is what says which spelling of it this node is. `10` and `10L` are one number
        // and two literals in C#, and only the second is this concept.
        new ConceptDeclaration(
            CSharpConceptKeys.LongLiteral,
            [new PropertySlot(ValueProperty, PropertyType.Integer, "0")],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "A whole number too wide for an int, written with its L suffix."),

        // A number, not text: the property type is what keeps `3.1.4` from being stored at all. An integral value is
        // allowed and is not a mistake - `double ratio = 3;` is legal C#, because the int widens on assignment - so
        // the notation adds no suffix and the generator writes what the author wrote.
        new ConceptDeclaration(
            CSharpConceptKeys.DoubleLiteral,
            [new PropertySlot(ValueProperty, PropertyType.Float, "0")],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "A number with a fractional part, as written."),

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

        // `null` is the one literal with no slot: it is a word and nothing else. Its type is deliberately not claimed,
        // because in C# `null` has no type of its own - it takes the type of whatever it is assigned to - so a rule
        // that named one would be wrong wherever the target was something else.
        new ConceptDeclaration(
            CSharpConceptKeys.NullLiteral,
            [],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "null"),

        // `this` is a word and nothing else, like `null`: it names the instance the code is running in, which is not a
        // declaration anything could resolve to, so it is a concept rather than a reference. It is what makes a
        // constructor able to store a parameter into the field of the same name - `this.message = message` is the only
        // spelling of that, and without this node the language could not write it at all.
        //
        // Its type is deliberately not claimed. `this` is the enclosing class, and a class is not a type this language
        // can describe: a `TypeReference` naming it is unknown too, because there is no declaration to read. Answering
        // something here would be inventing a type system the rest of the layer does not have.
        new ConceptDeclaration(
            CSharpConceptKeys.This,
            [],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "this"),

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
                new PropertySlot(IsPostfixSlot, PropertyType.Boolean, "false", "Whether the operator follows its operand."),
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

        // Three expressions whose subject is a type. A cast and an `is` both name one in a child slot rather than in a
        // property, so the type is a node the caret can enter and the type system can read - the same reason a field
        // holds its type as a child. An index names no type at all: it takes a position, and what an array or a string
        // returns there is a fact about the type of the target, which this language does not model.
        new ConceptDeclaration(
            CSharpConceptKeys.Cast,
            [
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Expression, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "(Type)operand"),

        new ConceptDeclaration(
            CSharpConceptKeys.Is,
            [
                new ChildSlot(CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "operand is Type"),

        // `operand as Type` is `is` and a cast in one word: it asks the same question and yields the value when the
        // answer is yes. Its type is the type it names, the same claim a cast makes, because a language without
        // nullability cannot say "this type or nothing" in its type system.
        new ConceptDeclaration(
            CSharpConceptKeys.As,
            [
                new ChildSlot(CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "operand as Type"),

        new ConceptDeclaration(
            CSharpConceptKeys.Index,
            [
                new ChildSlot(CSharpConceptKeys.TargetSlot, CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.IndexSlot, CSharpConceptKeys.Expression, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Expression],
            isRoot: false,
            description: "target[index]"),

        // A file is the namespaces it imports and the namespace it declares. The imports come first because that is
        // where C# 10's file-scoped form puts them, and they are a list because a file usually needs more than one.
        // A using names a namespace, not a type, so its name is a property rather than a type reference: there is no
        // declaration for it to resolve to, and pretending otherwise would offer a scope that does not exist.
        new ConceptDeclaration(
            CSharpConceptKeys.File,
            [
                new ChildSlot(CSharpConceptKeys.UsingsSlot, CSharpConceptKeys.Using, Cardinality.Many),
                new ChildSlot(CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace, Cardinality.One),
            ],
            description: "One document: its imports and the namespace it declares."),

        new ConceptDeclaration(
            CSharpConceptKeys.Using,
            [new PropertySlot(NameSlot, PropertyType.String, "System", "The namespace, with dots if it is nested.")],
            nameSlot: NameSlot,
            nameRule: NameRule.PascalDot,
            isRoot: false,
            description: "using Name;"),

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
                new ChildSlot(CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.TypeDeclaration, Cardinality.Many),
            ],
            nameSlot: NameSlot,
            nameRule: NameRule.PascalDot,
            isRoot: false,
            description: "namespace Name { ... }"),

        // A namespace holds types, and a class and an interface are both types - so the slot is declared over this and
        // the two do not need a second list each.
        new ConceptDeclaration(
            CSharpConceptKeys.TypeDeclaration,
            isAbstract: true,
            isRoot: false,
            description: "A type a namespace holds: a class or an interface."),

        // The list after the colon, as a node rather than two references on the class. A reference cannot be hidden by
        // the notation - there is no "while this reference is set" cell - while a child slot can, and a class that
        // inherits nothing should draw no colon at all.
        // One list rather than a base beside a list of interfaces, because C# separates the entries with commas and a
        // list cell is what knows how to do that: `: Shape, INamed` and `: INamed` are then the same shape, and the
        // language needs no rule for "a colon with no base but with interfaces".
        new ConceptDeclaration(
            CSharpConceptKeys.Inheritance,
            [new ChildSlot(CSharpConceptKeys.EntriesSlot, CSharpConceptKeys.InheritanceEntry, Cardinality.Many)],
            isRoot: false,
            description: "The types a class inherits from, in the order they are written."),

        // One entry. A node rather than a repeatable reference, because a reference has no list cell in the notation
        // layer: `InlineCollection` reads child slots, so the entry is what the list holds and the reference inside it
        // is what resolves - rename and go-to-definition work through it unchanged.
        new ConceptDeclaration(
            CSharpConceptKeys.InheritanceEntry,
            [new ReferenceSlot(CSharpConceptKeys.TargetSlot, CSharpConceptKeys.TypeDeclaration)],
            isRoot: false,
            description: "One type in a class's inheritance list."),

        new ConceptDeclaration(
            CSharpConceptKeys.Class,
            [
                new PropertySlot(VisibilitySlot, VisibilityType, CSharpVisibility.Internal),
                new PropertySlot(IsStaticSlot, PropertyType.Boolean, "false"),
                new PropertySlot(NameSlot, PropertyType.String, description: "The class's name."),
                new ChildSlot(CSharpConceptKeys.InheritanceSlot, CSharpConceptKeys.Inheritance, Cardinality.One, minimum: 0),
                new ChildSlot(CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Member, Cardinality.Many),
            ],
            superConcepts: [CSharpConceptKeys.TypeDeclaration],
            nameSlot: NameSlot,
            nameRule: NameRule.Pascal,
            isRoot: false,
            description: "A class: what it inherits and its members, in the order they were written."),

        // An interface holds signatures, which is why the method's body is optional: the same concept serves a class and
        // an interface, and the rule that reports a bodyless method skips the one place it is the point.
        new ConceptDeclaration(
            CSharpConceptKeys.Interface,
            [
                new PropertySlot(VisibilitySlot, PropertyType.String, CSharpVisibility.Public),
                new PropertySlot(NameSlot, PropertyType.String, description: "The interface's name."),
                new ChildSlot(CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Signature, Cardinality.Many),
            ],
            superConcepts: [CSharpConceptKeys.TypeDeclaration],
            nameSlot: NameSlot,
            nameRule: NameRule.Pascal,
            isRoot: false,
            description: "An interface: the signatures a class promises to provide."),

        // An enum is a type whose values are a closed set of names, so it is a type declaration like a class and an
        // interface - and its members are names, not fields: an enum value has no type of its own and no initializer
        // to write. Its members are a concept of their own so a name in the model can refer to one, which is what
        // `Color.Red` would need to resolve.
        new ConceptDeclaration(
            CSharpConceptKeys.Enum,
            [
                new PropertySlot(VisibilitySlot, PropertyType.String, CSharpVisibility.Public),
                new PropertySlot(NameSlot, PropertyType.String, description: "The enum's name."),
                new ChildSlot(CSharpConceptKeys.MembersSlot, CSharpConceptKeys.EnumMember, Cardinality.Many),
            ],
            superConcepts: [CSharpConceptKeys.TypeDeclaration],
            nameSlot: NameSlot,
            nameRule: NameRule.Pascal,
            isRoot: false,
            description: "An enum: a type whose values are a closed set of names."),

        // The MPS behaviour aspect in the only form this framework can honour: the model declares the methods that
        // belong to one concept, the generator emits them into a partial class, and the host writes the other half of
        // that partial by hand when it needs something a notation cannot express - an attribute, an `unsafe` block, a
        // method tuned by hand. That is what keeps "attributes are metadata, not logic" honest: the escape hatch is a
        // partial class rather than a concept per attribute.
        //
        // It is a TypeDeclaration rather than a second root, and that is the load-bearing decision. The language has
        // exactly one root - the document - which is what makes "new document" one question, and a behaviour class is
        // not a document: it is a type in a namespace, which is also what makes its `partial` half merge with the one
        // the host writes. Declared after Class, Interface and Enum so that it is never the concept a fresh slot is
        // filled with, since `DefaultInstantiableFor` answers with the first concrete concept below the slot's.
        //
        // The concept key is a property because C# has nothing a concept could be referenced by, and the class name is
        // a property because the notation may only draw properties and keywords - it cannot derive `RuleBehaviour` from
        // `Rules.Rule`. The key is also generated, as a comment: the host's hand-written half says nothing about the
        // model, so the generated half is where "which concept is this for" survives.
        new ConceptDeclaration(
            CSharpConceptKeys.Behaviour,
            [
                new PropertySlot(
                    CSharpConceptKeys.ConceptKeySlot,
                    PropertyType.String,
                    "Concept.Key",
                    "The concept this class is the behaviour of."),
                new PropertySlot(
                    NameSlot,
                    PropertyType.String,
                    "ConceptBehaviour",
                    "The class name, which the hand-written half has to match."),
                new ChildSlot(CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method, Cardinality.Many),
            ],
            superConcepts: [CSharpConceptKeys.TypeDeclaration],
            nameSlot: NameSlot,
            nameRule: NameRule.Pascal,
            isRoot: false,
            description: "A behaviour class: the methods one concept's nodes answer with."),

        new ConceptDeclaration(
            CSharpConceptKeys.EnumMember,
            [new PropertySlot(NameSlot, PropertyType.String, description: "The value's name.")],
            interfaces: [CSharpConceptKeys.NamedDeclaration],
            nameSlot: NameSlot,
            nameRule: NameRule.Pascal,
            isRoot: false,
            description: "One value of an enum."),

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

        // A property in its two shapes, chosen by two booleans rather than by a second concept: an auto-property
        // (`{ get; set; }`, or `{ get; }` when the setter is withheld) and a property whose accessors have bodies
        // (`{ get { ... } set { ... } }`). `hasSetter` says whether there is a setter at all; `hasBodies` says whether
        // the accessors are written out. The four combinations are the four forms C# allows, so the pair is honest
        // rather than a shortcut - and a second concept would have meant two names for one declaration.
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
                new PropertySlot(
                    CSharpConceptKeys.HasBodiesSlot,
                    PropertyType.Boolean,
                    "false",
                    "Whether the accessors have bodies; without them it is an auto-property."),
                new ChildSlot(CSharpConceptKeys.GetterSlot, CSharpConceptKeys.Block, Cardinality.One, minimum: 0),
                new ChildSlot(CSharpConceptKeys.SetterSlot, CSharpConceptKeys.Block, Cardinality.One, minimum: 0),
            ],
            superConcepts: [CSharpConceptKeys.Member],
            interfaces: [CSharpConceptKeys.NamedDeclaration],
            nameSlot: NameSlot,
            nameRule: NameRule.Pascal,
            isRoot: false,
            description: "A property: { get; set; }, { get; }, or accessors with bodies."),

        // An indexer is a member whose name is the keyword `this`: the type, then the parameters in brackets, then
        // the accessors. It is its own concept rather than a property with a parameter list, because it has no name
        // slot at all - and an empty slot is a row the layout keeps, which would draw a placeholder where a name
        // belongs. It always has bodies, so it needs no `hasBodies` boolean either.
        new ConceptDeclaration(
            CSharpConceptKeys.Indexer,
            [
                new PropertySlot(VisibilitySlot, PropertyType.String, CSharpVisibility.Public),
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter, Cardinality.Many),
                new ChildSlot(CSharpConceptKeys.GetterSlot, CSharpConceptKeys.Block, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.SetterSlot, CSharpConceptKeys.Block, Cardinality.One, minimum: 0),
            ],
            superConcepts: [CSharpConceptKeys.Member],
            isRoot: false,
            description: "An indexer: this[parameters] with its accessors."),

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

        // A member of an interface: the same four things a method declares before its body, and no body slot at all -
        // which is why it is its own concept rather than a method with an empty one. An empty slot is a row the layout
        // keeps for it, and an interface would then draw a placeholder where a signature belongs.
        new ConceptDeclaration(
            CSharpConceptKeys.Signature,
            [
                new PropertySlot(VisibilitySlot, PropertyType.String, CSharpVisibility.Public),
                new ChildSlot("returnType", CSharpConceptKeys.TypeReference, Cardinality.One),
                new PropertySlot(NameSlot, PropertyType.String, description: "The member's name."),
                new ChildSlot(CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter, Cardinality.Many),
            ],
            superConcepts: [CSharpConceptKeys.Member],
            nameSlot: NameSlot,
            nameRule: NameRule.Pascal,
            isRoot: false,
            description: "A member of an interface: a signature with no body."),

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
                // The flag first, because it is what decides how the slot below it is read: `var` is not a type, it is
                // the decision not to write one. The type slot is therefore optional - a local that says `var` holds no
                // type node at all - and a type left behind by switching to `var` is kept rather than deleted, so
                // switching back shows the author the type they wrote instead of an empty slot.
                new PropertySlot(IsVarSlot, PropertyType.Boolean, "false"),
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One, minimum: 0),
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

        // `for` has three parts and two separators, and the initializer is a *statement* - which is what makes the
        // notation work: a local declaration draws its own `;`, so that semicolon is the first separator rather than a
        // terminator. The increment is an expression for the same reason: `i++` has no `;`, so it cannot close the
        // header early.
        new ConceptDeclaration(
            CSharpConceptKeys.For,
            [
                new ChildSlot(CSharpConceptKeys.InitializerSlot, CSharpConceptKeys.Statement, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.IncrementSlot, CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block, Cardinality.One),
            ],
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "for (init; condition; increment) { ... }"),
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

        // A try is one statement with a block, the handlers that follow it and a block that runs whatever happened.
        // The handler is a concept of its own rather than a second block slot, because a try may have several and each
        // declares the exception it catches - which is a declaration a name in its body can point at, so it carries
        // the interface the scope provider resolves against.
        new ConceptDeclaration(
            CSharpConceptKeys.Try,
            [
                new ChildSlot(CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.CatchesSlot, CSharpConceptKeys.Catch, Cardinality.Many),
                new ChildSlot(CSharpConceptKeys.FinallySlot, CSharpConceptKeys.Block, Cardinality.One, minimum: 0),
            ],
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "try { ... } catch (Type name) { ... } finally { ... }"),

        // `catch (Exception e) { ... }`: the type it catches, the name it binds, and what it does. The name is a
        // property on the declaration, so the catch is what a name in its body resolves to - the same arrangement a
        // parameter has, and the reason a handler body can refer to the exception it caught.
        new ConceptDeclaration(
            CSharpConceptKeys.Catch,
            [
                new ChildSlot("type", CSharpConceptKeys.TypeReference, Cardinality.One),
                new PropertySlot(NameSlot, PropertyType.String, "e", "The name the caught exception is bound to."),
                new ChildSlot(CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block, Cardinality.One),
            ],
            interfaces: [CSharpConceptKeys.NamedDeclaration],
            nameSlot: NameSlot,
            nameRule: NameRule.Camel,
            isRoot: false,
            description: "catch (Type name) { ... }"),

        // `switch (value) { ... }`: the value it chooses on and the sections that answer it. The sections are a
        // concept of their own rather than a statement, because a section is not a statement the language can put
        // anywhere - it only exists inside a switch, and what it holds is what it runs.
        new ConceptDeclaration(
            CSharpConceptKeys.Switch,
            [
                new ChildSlot(CSharpConceptKeys.SubjectSlot, CSharpConceptKeys.Expression, Cardinality.One),
                new ChildSlot(CSharpConceptKeys.SectionsSlot, CSharpConceptKeys.SwitchSection, Cardinality.Many),
            ],
            superConcepts: [CSharpConceptKeys.Statement],
            isRoot: false,
            description: "switch (value) { case ...: ... }"),

        // One section: a case label or `default`, and what it does. The label is an optional child slot so the
        // `default` section has none to draw - and `isDefault` says which of the two the section is, because a
        // notation cannot ask "is this slot empty" and choose a keyword from the answer.
        new ConceptDeclaration(
            CSharpConceptKeys.SwitchSection,
            [
                new PropertySlot(CSharpConceptKeys.IsDefaultSlot, PropertyType.Boolean, "false", "Whether this is the default section."),
                new ChildSlot(CSharpConceptKeys.LabelSlot, CSharpConceptKeys.Expression, Cardinality.One, minimum: 0),
                new ChildSlot(CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Statement, Cardinality.Many),
            ],
            isRoot: false,
            description: "case value: ... or default: ..."),
    ];
}
