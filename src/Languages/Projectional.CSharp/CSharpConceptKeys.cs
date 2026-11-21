namespace Projectional.CSharp;

/// <summary>The keys of the C# base language, one per concept it declares.</summary>
/// <remarks>
/// The keys are the vocabulary a DSL writes: a slot declared over <see cref="Expression"/> accepts anything that
/// produces a value, which is what makes "put some C# here" a slot declaration rather than a second language.
/// </remarks>
public static class CSharpConceptKeys
{
    /// <summary>What every expression has in common: it produces a value. Abstract.</summary>
    public const string Expression = "CSharp.Expression";

    /// <summary>What every statement has in common: it runs. Abstract.</summary>
    public const string Statement = "CSharp.Statement";

    /// <summary>What a class holds. Abstract, because a member is a field or a method.</summary>
    public const string Member = "CSharp.Member";

    /// <summary>What a name can refer to: a field, a parameter or a local. An interface, because the three share no
    /// slots and only the fact that they are referencable.</summary>
    public const string NamedDeclaration = "CSharp.NamedDeclaration";

    /// <summary>A type, named.</summary>
    public const string TypeReference = "CSharp.TypeReference";

    /// <summary>A whole number, as written.</summary>
    public const string IntLiteral = "CSharp.IntLiteral";

    /// <summary>A string literal, without its quotes.</summary>
    public const string StringLiteral = "CSharp.StringLiteral";

    /// <summary><c>true</c> or <c>false</c>.</summary>
    public const string BoolLiteral = "CSharp.BoolLiteral";

    /// <summary>A name, resolved by the compiler and never by us.</summary>
    public const string Name = "CSharp.Name";

    /// <summary><c>target.member</c>.</summary>
    public const string MemberAccess = "CSharp.MemberAccess";

    /// <summary>A call: the callee, then its arguments.</summary>
    public const string Call = "CSharp.Call";

    /// <summary><c>left op right</c>.</summary>
    public const string Binary = "CSharp.Binary";

    /// <summary>An expression with its parentheses kept.</summary>
    public const string Parenthesized = "CSharp.Parenthesized";

    /// <summary><c>new Type(args)</c>.</summary>
    public const string New = "CSharp.New";

    /// <summary>One operator in front of one operand: <c>!ready</c>.</summary>
    public const string Unary = "CSharp.Unary";

    /// <summary><c>condition ? whenTrue : whenFalse</c>.</summary>
    public const string Conditional = "CSharp.Conditional";

    /// <summary>A collection expression: the elements between two brackets.</summary>
    public const string CollectionExpression = "CSharp.CollectionExpression";

    /// <summary><c>(Type)operand</c>: the same value, read as another type.</summary>
    public const string Cast = "CSharp.Cast";

    /// <summary><c>operand is Type</c>: whether a value is of a type.</summary>
    public const string Is = "CSharp.Is";

    /// <summary><c>target[index]</c>: the element at a position.</summary>
    public const string Index = "CSharp.Index";

    /// <summary>One document: a namespace and what it holds. The root.</summary>
    public const string File = "CSharp.File";

    /// <summary><c>namespace Name { ... }</c>.</summary>
    public const string Namespace = "CSharp.Namespace";

    /// <summary>A class: its fields and its methods, in the order they were written.</summary>
    public const string Class = "CSharp.Class";

    /// <summary>A type a namespace holds: a class or an interface. Abstract.</summary>
    public const string TypeDeclaration = "CSharp.TypeDeclaration";

    /// <summary>The base a class names, and the interfaces it implements: the list after the colon.</summary>
    public const string Inheritance = "CSharp.Inheritance";

    /// <summary>A member of an interface: a signature with no body.</summary>
    public const string Signature = "CSharp.Signature";

    /// <summary>One entry of a class's inheritance list: a node so the list can be drawn, a reference so it resolves.</summary>
    public const string InheritanceEntry = "CSharp.InheritanceEntry";

    /// <summary>An interface: the members a class promises to provide.</summary>
    public const string Interface = "CSharp.Interface";

    /// <summary>A constructor: the parameters and the body that make an instance.</summary>
    public const string Constructor = "CSharp.Constructor";

    /// <summary>An auto-property: a getter, and a setter when it has one.</summary>
    public const string Property = "CSharp.Property";

    /// <summary>A field, with an initializer when it has one.</summary>
    public const string Field = "CSharp.Field";

    /// <summary>A method: its signature and its body.</summary>
    public const string Method = "CSharp.Method";

    /// <summary>One parameter: a type and a name.</summary>
    public const string Parameter = "CSharp.Parameter";

    /// <summary>A block: the statements between two braces.</summary>
    public const string Block = "CSharp.Block";

    /// <summary>An expression run for its effect.</summary>
    public const string ExpressionStatement = "CSharp.ExpressionStatement";

    /// <summary><c>Type name = value;</c></summary>
    public const string LocalDeclaration = "CSharp.LocalDeclaration";

    /// <summary><c>target = value;</c></summary>
    public const string Assignment = "CSharp.Assignment";

    /// <summary><c>if (condition) { ... } else { ... }</c>.</summary>
    public const string If = "CSharp.If";

    /// <summary><c>for (init; condition; increment) { ... }</c>.</summary>
    public const string For = "CSharp.For";

    /// <summary><c>while (condition) { ... }</c>.</summary>
    public const string While = "CSharp.While";

    /// <summary><c>foreach (Type name in source) { ... }</c>.</summary>
    public const string ForEach = "CSharp.ForEach";

    /// <summary>Leaves the enclosing loop.</summary>
    public const string Break = "CSharp.Break";

    /// <summary>Skips to the next iteration of the enclosing loop.</summary>
    public const string Continue = "CSharp.Continue";

    /// <summary><c>return value;</c></summary>
    public const string Return = "CSharp.Return";

    /// <summary><c>throw value;</c></summary>
    public const string Throw = "CSharp.Throw";

    /// <summary>The slot holding the namespace a file declares.</summary>
    public const string NamespaceSlot = "namespace";

    /// <summary>The slot holding a namespace's classes.</summary>
    public const string ClassesSlot = "classes";

    /// <summary>The slot holding a class's members.</summary>
    public const string MembersSlot = "members";

    /// <summary>The slot holding a block's statements.</summary>
    public const string StatementsSlot = "statements";

    /// <summary>The slot holding a method's parameters.</summary>
    public const string ParametersSlot = "parameters";

    /// <summary>The slot holding a collection expression's elements.</summary>
    public const string ElementsSlot = "elements";

    /// <summary>The slot holding a call's arguments.</summary>
    public const string ArgumentsSlot = "arguments";

    /// <summary>The slot holding a method's or a block's body.</summary>
    public const string BodySlot = "body";

    /// <summary>The slot holding an optional initializer or return value.</summary>
    public const string ValueSlot = "value";

    /// <summary>The property holding whether a property has a setter.</summary>
    public const string HasSetterSlot = "hasSetter";

    /// <summary>The slot holding the value an operator is applied to, or the one a ternary chooses.</summary>
    public const string OperandSlot = "operand";

    /// <summary>The slot holding the position an index reads, or a cast's or an <c>is</c>'s type.</summary>
    public const string IndexSlot = "index";

    /// <summary>The slot holding a for's initializer.</summary>
    public const string InitializerSlot = "initializer";

    /// <summary>The slot holding a for's increment.</summary>
    public const string IncrementSlot = "increment";

    /// <summary>The slot holding what a foreach iterates.</summary>
    public const string SourceSlot = "source";

    /// <summary>The slot holding the class a class extends.</summary>
    public const string BaseSlot = "base";

    /// <summary>The slot holding the interfaces a class implements.</summary>
    public const string InterfacesSlot = "interfaces";

    /// <summary>The slot holding the list a class inherits from.</summary>
    public const string InheritanceSlot = "inheritance";

    /// <summary>The slot holding the entries of that list.</summary>
    public const string EntriesSlot = "entries";

    /// <summary>The slot holding the declaration a name refers to.</summary>
    public const string TargetSlot = "target";

    /// <summary>The slot holding an if's condition.</summary>
    public const string ConditionSlot = "condition";

    /// <summary>The slot holding the block an if runs.</summary>
    public const string ThenSlot = "then";

    /// <summary>The slot holding the block an if runs otherwise.</summary>
    public const string OtherwiseSlot = "otherwise";
}
