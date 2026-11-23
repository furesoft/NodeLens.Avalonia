using Projectional.Core;

using Projectional.CSharp;

namespace Projectional.Core.Tests;

/// <summary>The C# documents the golden corpus generates, one per half of the subset the plan names.</summary>
/// <remarks>
/// <para>
/// The models are code rather than <c>.mps</c> files: what is checked in is the *text* they generate, because that is
/// the artefact a reviewer reads and a byte that moves is a change to the generator. The persistence round trip is
/// already pinned by the <c>.mps</c> corpus, so writing these documents down twice would prove nothing new.
/// </para>
/// <para>
/// Every member is public or is read by one, and every name is a reference that resolves: the generated text has to
/// compile with warnings as errors, which is the same rule the repository's own code follows.
/// </para>
/// </remarks>
internal static class CSharpCorpus
{
    /// <summary>Every document, named as its golden file is.</summary>
    internal static IReadOnlyList<(string Name, Model Model)> Documents() =>
    [
        ("expressions", Expressions()),
        ("statements", Statements()),
        ("linq", Linq()),
    ];

    /// <summary>The expression form that makes LINQ writable: a lambda handed to a call.</summary>
    /// <remarks>
    /// The plan's row for LINQ says the query syntax is out because a call already expresses the same tree. That was
    /// true of the tree and not of the language: a call whose argument is *code* needs code to pass, and there was no
    /// lambda. This document is the check on the claim - two extension methods, one with a single parameter and one with
    /// two, compiled by the corpus test with warnings as errors.
    /// </remarks>
    private static Model Linq()
    {
        var (model, type) = NewFile("linq", "Linq");

        model.NewChild(model.Roots[0], CSharpConceptKeys.UsingsSlot, CSharpConceptKeys.Using)
            .Set(CSharpLanguage.NameSlot, "System.Collections.Generic");
        model.NewChild(model.Roots[0], CSharpConceptKeys.UsingsSlot, CSharpConceptKeys.Using)
            .Set(CSharpLanguage.NameSlot, "System.Linq");

        // public IEnumerable<int> Positive(IEnumerable<int> values) { return values.Where((item) => item > 0); }
        var positive = Method(model, type, "IEnumerable<int>", "Positive", ("IEnumerable<int>", "values"), out var positiveBody);
        var where = model.NewChild(Return(model, positiveBody), CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Call);
        var filtered = model.NewChild(where, "callee", CSharpConceptKeys.MemberAccess);
        NameOf(model, filtered, "target", Parameter(positive, 0));
        filtered.Set(CSharpLanguage.MemberSlot, "Where");

        var test = model.NewChild(where, CSharpConceptKeys.ArgumentsSlot, CSharpConceptKeys.Lambda);
        var item = model.NewChild(test, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.LambdaParameter);
        item.Set(CSharpLanguage.NameSlot, "item");
        var positive0 = Binary(model, test, CSharpConceptKeys.BodySlot, CSharpBinaryOperator.GreaterThan);
        NameOf(model, positive0, "left", item);
        Number(model, positive0, "right", "0");

        // public int Total(IEnumerable<int> values) { return values.Aggregate((left, right) => left + right); }
        var total = Method(model, type, "int", "Total", ("IEnumerable<int>", "values"), out var totalBody);
        var aggregate = model.NewChild(Return(model, totalBody), CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Call);
        var summed = model.NewChild(aggregate, "callee", CSharpConceptKeys.MemberAccess);
        NameOf(model, summed, "target", Parameter(total, 0));
        summed.Set(CSharpLanguage.MemberSlot, "Aggregate");

        var add = model.NewChild(aggregate, CSharpConceptKeys.ArgumentsSlot, CSharpConceptKeys.Lambda);
        var left = model.NewChild(add, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.LambdaParameter);
        var right = model.NewChild(add, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.LambdaParameter);
        left.Set(CSharpLanguage.NameSlot, "left");
        right.Set(CSharpLanguage.NameSlot, "right");
        var sum = Binary(model, add, CSharpConceptKeys.BodySlot, CSharpBinaryOperator.Add);
        NameOf(model, sum, "left", left);
        NameOf(model, sum, "right", right);

        return model;
    }

    /// <summary>The expression forms: precedence, grouping, logic, a choice, a cast, a test, a collection and an index.</summary>
    private static Model Expressions()
    {
        var (model, type) = NewFile("expressions", "Expressions");

        // public int Precedence(int amount) { return amount + 10 * 2; }
        var precedence = Method(model, type, "int", "Precedence", ("int", "amount"), out var precedenceBody);
        var amount = Parameter(precedence, 0);
        var sum = Binary(model, Return(model, precedenceBody), "value", CSharpBinaryOperator.Add);
        NameOf(model, sum, "left", amount);
        var product = Binary(model, sum, "right", CSharpBinaryOperator.Multiply);
        Number(model, product, "left", "10");
        Number(model, product, "right", "2");

        // public int Grouped(int amount) { return (amount + 10) * 2; } - the one place a reader's parentheses survive.
        var grouped = Method(model, type, "int", "Grouped", ("int", "amount"), out var groupedBody);
        var groupedAmount = Parameter(grouped, 0);
        var outer = Binary(model, Return(model, groupedBody), "value", CSharpBinaryOperator.Multiply);
        var parenthesised = model.NewChild(outer, "left", CSharpConceptKeys.Parenthesized);
        var inner = Binary(model, parenthesised, "inner", CSharpBinaryOperator.Add);
        NameOf(model, inner, "left", groupedAmount);
        Number(model, inner, "right", "10");
        Number(model, outer, "right", "2");

        // public bool Logic(bool flag, int amount) { return flag && amount > 10; }
        var logic = Method(model, type, "bool", "Logic", [("bool", "flag"), ("int", "amount")], out var logicBody);
        var either = Binary(model, Return(model, logicBody), "value", CSharpBinaryOperator.And);
        NameOf(model, either, "left", Parameter(logic, 0));
        var bound = Binary(model, either, "right", CSharpBinaryOperator.GreaterThan);
        NameOf(model, bound, "left", Parameter(logic, 1));
        Number(model, bound, "right", "10");

        // public int Choose(bool flag, int amount) { return flag ? amount : -amount; }
        var choose = Method(model, type, "int", "Choose", [("bool", "flag"), ("int", "amount")], out var chooseBody);
        var choice = model.NewChild(Return(model, chooseBody), "value", CSharpConceptKeys.Conditional);
        NameOf(model, choice, CSharpConceptKeys.ConditionSlot, Parameter(choose, 0));
        NameOf(model, choice, CSharpLanguage.WhenTrueSlot, Parameter(choose, 1));
        var negation = model.NewChild(choice, CSharpLanguage.WhenFalseSlot, CSharpConceptKeys.Unary);
        negation.Set(CSharpLanguage.UnaryOperatorSlot, CSharpUnaryOperator.Negate);
        NameOf(model, negation, CSharpConceptKeys.OperandSlot, Parameter(choose, 1));

        // public int Cast(double value) { return (int)value; }
        var cast = Method(model, type, "int", "Cast", ("double", "value"), out var castBody);
        var conversion = model.NewChild(Return(model, castBody), "value", CSharpConceptKeys.Cast);
        Type(model, conversion, "type", "int");
        NameOf(model, conversion, CSharpConceptKeys.OperandSlot, Parameter(cast, 0));

        // public bool Test(object value) { return value is string; }
        var test = Method(model, type, "bool", "Test", ("object", "value"), out var testBody);
        var typeTest = model.NewChild(Return(model, testBody), "value", CSharpConceptKeys.Is);
        NameOf(model, typeTest, CSharpConceptKeys.OperandSlot, Parameter(test, 0));
        Type(model, typeTest, "type", "string");

        // public int[] Build(int first) { return [first, 2]; }
        var build = Method(model, type, "int[]", "Build", ("int", "first"), out var buildBody);
        var collection = model.NewChild(Return(model, buildBody), "value", CSharpConceptKeys.CollectionExpression);
        NameOf(model, collection, CSharpConceptKeys.ElementsSlot, Parameter(build, 0));
        Number(model, collection, CSharpConceptKeys.ElementsSlot, "2");

        // public int First(int[] values) { return values[0]; }
        var first = Method(model, type, "int", "First", ("int[]", "values"), out var firstBody);
        var index = model.NewChild(Return(model, firstBody), "value", CSharpConceptKeys.Index);
        NameOf(model, index, CSharpConceptKeys.TargetSlot, Parameter(first, 0));
        Number(model, index, CSharpConceptKeys.IndexSlot, "0");

        return model;
    }

    /// <summary>The statements and declarations: a field, a constructor, a property, a branch, two loops and a switch.</summary>
    private static Model Statements()
    {
        var (model, type) = NewFile("statements", "Statements");

        // private int seen; - read by the constructor and by the property below.
        var field = model.NewChild(type, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
        field.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Private);
        field.Set(CSharpLanguage.NameSlot, "seen");
        Type(model, field, "type", "int");

        // public Statements(int seen) { this.seen = seen; }
        var constructor = model.NewChild(type, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Constructor);
        constructor.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        constructor.Set(CSharpLanguage.NameSlot, "Statements");
        var parameter = model.NewChild(constructor, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
        Type(model, parameter, "type", "int");
        parameter.Set(CSharpLanguage.NameSlot, "seen");
        var constructorBody = model.NewChild(constructor, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var assign = model.NewChild(constructorBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Assignment);
        var target = model.NewChild(assign, "target", CSharpConceptKeys.MemberAccess);
        model.NewChild(target, "target", CSharpConceptKeys.This);
        target.Set(CSharpLanguage.MemberSlot, "seen");
        NameOf(model, assign, CSharpConceptKeys.ValueSlot, parameter);

        // public int Seen { get { return seen; } }
        var property = model.NewChild(type, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Property);
        property.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        property.Set(CSharpLanguage.NameSlot, "Seen");
        Type(model, property, "type", "int");
        property.Set(CSharpConceptKeys.HasSetterSlot, "false");
        property.Set(CSharpConceptKeys.HasBodiesSlot, "true");
        var getter = model.NewChild(property, CSharpConceptKeys.GetterSlot, CSharpConceptKeys.Block);
        NameOf(model, Return(model, getter), "value", field);

        // public int Decide(int amount) { if (...) { return 1; } else if (...) { return 2; } else { return 0; } }
        var decide = Method(model, type, "int", "Decide", ("int", "amount"), out var decideBody);
        var decideAmount = Parameter(decide, 0);
        var branch = If(model, decideBody);
        var high = Binary(model, branch, CSharpConceptKeys.ConditionSlot, CSharpBinaryOperator.GreaterThan);
        NameOf(model, high, "left", decideAmount);
        Number(model, high, "right", "100");
        Number(model, Return(model, Block(model, branch, CSharpConceptKeys.ThenSlot)), "value", "1");

        var otherwise = Block(model, branch, CSharpConceptKeys.OtherwiseSlot);
        var second = If(model, otherwise);
        var medium = Binary(model, second, CSharpConceptKeys.ConditionSlot, CSharpBinaryOperator.GreaterThan);
        NameOf(model, medium, "left", decideAmount);
        Number(model, medium, "right", "10");
        Number(model, Return(model, Block(model, second, CSharpConceptKeys.ThenSlot)), "value", "2");
        Number(model, Return(model, Block(model, second, CSharpConceptKeys.OtherwiseSlot)), "value", "0");

        // public void Count(int count) { for (int i = 0; i < count; i++) { if (i == 2) { continue; } } }
        var count = Method(model, type, "void", "Count", ("int", "count"), out var countBody);
        var loop = model.NewChild(countBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.For);
        var initializer = model.NewChild(loop, CSharpConceptKeys.InitializerSlot, CSharpConceptKeys.ForInitializer);
        initializer.Set(CSharpLanguage.NameSlot, "i");
        Type(model, initializer, "type", "int");
        Number(model, initializer, CSharpConceptKeys.ValueSlot, "0");
        var bound = Binary(model, loop, CSharpConceptKeys.ConditionSlot, CSharpBinaryOperator.LessThan);
        NameOf(model, bound, "left", initializer);
        NameOf(model, bound, "right", Parameter(count, 0));
        var step = model.NewChild(loop, CSharpConceptKeys.IncrementSlot, CSharpConceptKeys.Unary);
        step.Set(CSharpLanguage.UnaryOperatorSlot, CSharpUnaryOperator.Increment);
        step.Set(CSharpLanguage.IsPostfixSlot, "true");
        NameOf(model, step, CSharpConceptKeys.OperandSlot, initializer);

        var loopBranch = If(model, Block(model, loop, CSharpConceptKeys.BodySlot));
        var reached = Binary(model, loopBranch, CSharpConceptKeys.ConditionSlot, CSharpBinaryOperator.Equal);
        NameOf(model, reached, "left", initializer);
        Number(model, reached, "right", "2");
        model.NewChild(Block(model, loopBranch, CSharpConceptKeys.ThenSlot), CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Continue);

        // public void Walk(int[] values) { int index = 0; while (index < values.Length) { index++; } }
        var walk = Method(model, type, "void", "Walk", ("int[]", "values"), out var walkBody);
        var local = model.NewChild(walkBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.LocalDeclaration);
        local.Set(CSharpLanguage.NameSlot, "index");
        Type(model, local, "type", "int");
        Number(model, local, CSharpConceptKeys.ValueSlot, "0");

        var repeat = model.NewChild(walkBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.While);
        var comparison = Binary(model, repeat, CSharpConceptKeys.ConditionSlot, CSharpBinaryOperator.LessThan);
        NameOf(model, comparison, "left", local);
        var length = model.NewChild(comparison, "right", CSharpConceptKeys.MemberAccess);
        NameOf(model, length, "target", Parameter(walk, 0));
        length.Set(CSharpLanguage.MemberSlot, "Length");

        var bump = model.NewChild(Block(model, repeat, CSharpConceptKeys.BodySlot), CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var increment = model.NewChild(bump, "expression", CSharpConceptKeys.Unary);
        increment.Set(CSharpLanguage.UnaryOperatorSlot, CSharpUnaryOperator.Increment);
        increment.Set(CSharpLanguage.IsPostfixSlot, "true");
        NameOf(model, increment, CSharpConceptKeys.OperandSlot, local);

        // public string Classify(int value) { switch (value) { case 1: return "one"; default: return "many"; } }
        var classify = Method(model, type, "string", "Classify", ("int", "value"), out var classifyBody);
        var selection = model.NewChild(classifyBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Switch);
        NameOf(model, selection, CSharpConceptKeys.SubjectSlot, Parameter(classify, 0));

        var one = model.NewChild(selection, CSharpConceptKeys.SectionsSlot, CSharpConceptKeys.SwitchSection);
        Number(model, one, CSharpConceptKeys.LabelSlot, "1");
        Text(model, Return(model, one), "value", "one");

        var many = model.NewChild(selection, CSharpConceptKeys.SectionsSlot, CSharpConceptKeys.SwitchSection);
        many.Set(CSharpConceptKeys.IsDefaultSlot, "true");
        Text(model, Return(model, many), "value", "many");

        return model;
    }

    /// <summary>A model holding one document: a namespace and one public class in it.</summary>
    private static (Model Model, Node Type) NewFile(string name, string className)
    {
        var model = new Project(CSharpLanguage.Concepts).NewModel(name);
        var file = model.NewNode(CSharpConceptKeys.File);
        var space = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var type = model.NewChild(space, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);

        space.Set(CSharpLanguage.NameSlot, "Sample");
        type.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        type.Set(CSharpLanguage.NameSlot, className);
        model.AddRoot(file);

        return (model, type);
    }

    /// <summary>A public method, its parameters in order, and the block its statements go into.</summary>
    private static Node Method(
        Model model,
        Node type,
        string returnType,
        string name,
        (string Type, string Name)[] parameters,
        out Node body)
    {
        var method = model.NewChild(type, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);

        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.NameSlot, name);
        Type(model, method, "returnType", returnType);

        foreach (var (parameterType, parameterName) in parameters)
        {
            var parameter = model.NewChild(method, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
            Type(model, parameter, "type", parameterType);
            parameter.Set(CSharpLanguage.NameSlot, parameterName);
        }

        body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);

        return method;
    }

    /// <summary>The same method for one parameter, so a test does not spell the array out.</summary>
    private static Node Method(Model model, Node type, string returnType, string name, (string Type, string Name) parameter, out Node body) =>
        Method(model, type, returnType, name, [parameter], out body);

    private static Node Parameter(Node method, int index) =>
        method.GetChildren(CSharpConceptKeys.ParametersSlot)[index];

    private static Node Type(Model model, Node owner, string slot, string name)
    {
        var type = model.NewChild(owner, slot, CSharpConceptKeys.TypeReference);
        type.Set(CSharpLanguage.NameSlot, name);

        return type;
    }

    private static Node NameOf(Model model, Node owner, string slot, Node declaration)
    {
        var name = model.NewChild(owner, slot, CSharpConceptKeys.Name);
        name.SetReference(CSharpConceptKeys.TargetSlot, declaration);

        return name;
    }

    private static Node Number(Model model, Node owner, string slot, string value)
    {
        var number = model.NewChild(owner, slot, CSharpConceptKeys.IntLiteral);
        number.Set(CSharpLanguage.ValueProperty, value);

        return number;
    }

    private static Node Text(Model model, Node owner, string slot, string value)
    {
        var text = model.NewChild(owner, slot, CSharpConceptKeys.StringLiteral);
        text.Set(CSharpLanguage.ValueProperty, value);

        return text;
    }

    private static Node Binary(Model model, Node owner, string slot, string symbol)
    {
        var binary = model.NewChild(owner, slot, CSharpConceptKeys.Binary);
        binary.Set(CSharpLanguage.OperatorSlot, symbol);

        return binary;
    }

    private static Node Return(Model model, Node block) =>
        model.NewChild(block, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);

    private static Node If(Model model, Node block) =>
        model.NewChild(block, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.If);

    private static Node Block(Model model, Node owner, string slot) =>
        model.NewChild(owner, slot, CSharpConceptKeys.Block);
}
