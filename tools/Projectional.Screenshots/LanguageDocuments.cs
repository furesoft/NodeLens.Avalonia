using Projectional.Core;
using Projectional.CSharp;
using Projectional.Expressions;
using Projectional.ImportRules;
using Projectional.Languages;
using Projectional.Samples.FeatureModel;
using Projectional.Samples.SqlSchema;
using Projectional.Samples.StateMachine;
using Projectional.Testing;
using Projectional.Workbench;

namespace Projectional.Screenshots;

/// <summary>The documents the example-language screenshots are taken of.</summary>
/// <remarks>
/// They are the corpus documents of the three samples, built here through the public model API rather than loaded
/// from their files, for the same reason the feature shots build theirs: the tool must not depend on where the
/// sources sit. Each is a little longer than its corpus so the picture shows a language rather than a fragment.
/// The one exception is the declared expressions document, which is opened from the folder - see
/// <see cref="DeclaredExpressions"/> for why a model built by hand cannot stand in for it.
/// </remarks>
internal static class LanguageDocuments
{
    /// <summary>A machine with a loop, a guard and one state nothing reaches - so a rule has something to say.</summary>
    internal static Model StateMachine()
    {
        var model = StateMachineLanguage.NewProject().NewModel("Release");
        var machine = model.NewNode(SmConceptKeys.Machine);
        machine.Set("name", "Release");
        model.AddRoot(machine);

        var idle = State(model, machine, "Idle");
        var building = State(model, machine, "Building");
        var unreachable = State(model, machine, "Broken");
        var shipped = State(model, machine, "Shipped", isFinal: true);

        machine.SetReference(StateMachineLanguage.InitialSlot, idle);

        Transition(model, machine, "start", idle, building, guard: (">", 0));
        Transition(model, machine, "retry", building, building, guard: ("<", 3));
        Transition(model, machine, "ship", building, shipped);
        Transition(model, machine, "rescind", unreachable, idle);

        return model;
    }

    /// <summary>A feature model whose selection and rules disagree, so the interpreter has something to reject.</summary>
    internal static Model FeatureModel()
    {
        var model = FeatureModelLanguage.NewProject().NewModel("Release");
        var root = model.NewNode(FmConceptKeys.Model);
        root.Set("name", "Release");
        model.AddRoot(root);

        var core = Feature(model, root, "Core", selected: true);
        var ui = Feature(model, root, "Ui", selected: true);
        var safe = Feature(model, root, "Safe", selected: true);
        var experimental = Feature(model, root, "Experimental");

        Constraint(model, root, FmConceptKeys.Requires, core, safe);
        Constraint(model, root, FmConceptKeys.Requires, ui, core);
        Constraint(model, root, FmConceptKeys.Excludes, safe, experimental);

        return model;
    }

    /// <summary>Three tables and two keys, which is the cross-document axis in one picture.</summary>
    internal static Model SqlSchema()
    {
        var model = SqlSchemaLanguage.NewProject().NewModel("Shop");
        var schema = model.NewNode(SqlConceptKeys.Schema);
        schema.Set("name", "shop");
        model.AddRoot(schema);

        var person = Table(model, schema, "person");
        var personId = Column(model, person, "id", "int");
        Column(model, person, "name", "text");
        Column(model, person, "email", "text");

        var city = Table(model, schema, "city");
        var cityId = Column(model, city, "id", "int");
        Column(model, city, "name", "text");

        // Two keys out of one table, which is the shape a foreign key is for: an address belongs to a person and
        // sits in a city, and both are links - not a copy of the name.
        var address = Table(model, schema, "address");
        Column(model, address, "id", "int");
        var addressPerson = Column(model, address, "person_id", "int");
        var addressCity = Column(model, address, "city_id", "int");
        Key(model, address, addressPerson, personId);
        Key(model, address, addressCity, cityId);

        return model;
    }

    /// <summary>
    /// A language's own test, written in the test language: one document asserting that the pipeline draws as the
    /// table it should. The picture is the point of the slice - a language author writes a test as data, not in C#.
    /// </summary>
    internal static Model Testing()
    {
        var model = TestingLanguage.NewModel();
        var test = model.Roots[0];

        test.Set(TestingLanguage.DocumentSlot, "pipeline.mps");

        var draws = model.NewChild(test, TestingLanguage.AssertionSlot, TestingConceptKeys.Draws);

        draws.Set(TestingLanguage.WidthSlot, "60");

        foreach (var line in new[]
        {
            "pipeline Release",
            "  stage run          retries",
            "  build dotnet build 1",
            "  test  dotnet test  0",
        })
        {
            model.NewChild(draws, TestingLanguage.LinesSlot, TestingConceptKeys.Line)
                .Set(TestingLanguage.TextSlot, line);
        }

        return model;
    }

    private static Node State(Model model, Node machine, string name, bool isFinal = false)
    {
        var state = model.NewChild(machine, StateMachineLanguage.StatesSlot, SmConceptKeys.State);
        state.Set("name", name);
        state.Set("isFinal", isFinal);

        return state;
    }

    private static void Transition(
        Model model,
        Node machine,
        string @event,
        Node source,
        Node target,
        (string Operator, int Value)? guard = null)
    {
        var transition = model.NewChild(machine, StateMachineLanguage.TransitionsSlot, SmConceptKeys.Transition);
        transition.Set("event", @event);
        transition.SetReference("source", source);
        transition.SetReference("target", target);

        if (guard is not { } comparison)
        {
            return;
        }

        // `attempts <op> <value>`: the shared expression language, which is what a guard is.
        var guardNode = model.NewChild(transition, "guard", ExpressionsConceptKeys.Binary);
        guardNode.Set("operator", comparison.Operator);
        model.NewChild(guardNode, "left", ExpressionsConceptKeys.Name).Set("name", "attempts");
        model.NewChild(guardNode, "right", ExpressionsConceptKeys.IntLiteral).Set("value", comparison.Value);
    }

    private static Node Feature(Model model, Node root, string name, bool selected = false)
    {
        var feature = model.NewChild(root, FeatureModelLanguage.FeaturesSlot, FmConceptKeys.Feature);
        feature.Set("name", name);
        feature.Set(FeatureModelLanguage.SelectedSlot, selected);

        return feature;
    }

    private static void Constraint(Model model, Node root, string concept, Node subject, Node other)
    {
        var constraint = model.NewChild(root, FeatureModelLanguage.ConstraintsSlot, concept);
        constraint.SetReference(FeatureModelLanguage.SubjectSlot, subject);
        constraint.SetReference(FeatureModelLanguage.OtherSlot, other);
    }

    private static Node Table(Model model, Node schema, string name)
    {
        var table = model.NewChild(schema, SqlSchemaLanguage.TablesSlot, SqlConceptKeys.Table);
        table.Set("name", name);

        return table;
    }

    private static Node Column(Model model, Node table, string name, string type)
    {
        var column = model.NewChild(table, SqlSchemaLanguage.ColumnsSlot, SqlConceptKeys.Column);
        column.Set("name", name);
        column.Set("type", type);

        return column;
    }

    private static void Key(Model model, Node table, Node local, Node target)
    {
        var key = model.NewChild(table, SqlSchemaLanguage.KeysSlot, SqlConceptKeys.ForeignKey);
        key.SetReference(SqlSchemaLanguage.LocalSlot, local);
        key.SetReference(SqlSchemaLanguage.TargetSlot, target);
    }

    /// <summary>
    /// The C# base language, in one picture: a file-scoped namespace, two fields, a method with an <c>if</c>/<c>else</c>,
    /// a collection expression and a counted <c>for</c> loop.
    /// </summary>
    /// <remarks>
    /// Built through the model API like the sample languages above, and every name in it is a reference that resolves:
    /// the arguments point at the fields, so the picture shows a language that knows what its names mean rather than
    /// one that only draws them. The namespace is file-scoped because that is the form this language exists to show -
    /// one concept, two shapes, and this is the modern one.
    /// </remarks>
    internal static Model CSharp()
    {
        var model = new Project(CSharpLanguage.Concepts).NewModel("Greeter");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);

        model.AddRoot(file);

        ns.Set(CSharpLanguage.NameSlot, "Sample");
        ns.Set(CSharpLanguage.IsFileScopedSlot, "true");
        cls.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        cls.Set(CSharpLanguage.NameSlot, "Greeter");

        var message = Field(model, cls, "private", "string", "message", CSharpConceptKeys.StringLiteral, "Hello");
        var count = Field(model, cls, "private", "int", "count", CSharpConceptKeys.IntLiteral, "0");

        // public static void Greet() { if (count == 0) { ... } else { ... } }
        var greet = Method(model, cls, "public", isStatic: true, "void", "Greet", out var body);
        var branch = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.If);
        var condition = model.NewChild(branch, CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.Binary);
        var guard = model.NewChild(condition, "left", CSharpConceptKeys.Name);
        var zero = model.NewChild(condition, "right", CSharpConceptKeys.IntLiteral);
        var then = model.NewChild(branch, CSharpConceptKeys.ThenSlot, CSharpConceptKeys.Block);
        var otherwise = model.NewChild(branch, CSharpConceptKeys.OtherwiseSlot, CSharpConceptKeys.Block);

        condition.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.Equal);
        guard.SetReference(CSharpConceptKeys.TargetSlot, count);
        zero.Set(CSharpLanguage.ValueProperty, "0");

        var write = model.NewChild(then, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var call = model.NewChild(write, "expression", CSharpConceptKeys.Call);
        var callee = model.NewChild(call, "callee", CSharpConceptKeys.MemberAccess);
        var console = model.NewChild(callee, "target", CSharpConceptKeys.Name);
        var argument = model.NewChild(call, CSharpConceptKeys.ArgumentsSlot, CSharpConceptKeys.Name);

        callee.Set(CSharpLanguage.MemberSlot, "WriteLine");
        console.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "Console");
        argument.SetReference(CSharpConceptKeys.TargetSlot, message);

        var bump = model.NewChild(otherwise, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Assignment);
        var target = model.NewChild(bump, "target", CSharpConceptKeys.Name);
        var increment = model.NewChild(bump, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.Binary);
        var incremented = model.NewChild(increment, "left", CSharpConceptKeys.Name);
        var one = model.NewChild(increment, "right", CSharpConceptKeys.IntLiteral);

        target.SetReference(CSharpConceptKeys.TargetSlot, count);
        increment.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.Add);
        incremented.SetReference(CSharpConceptKeys.TargetSlot, count);
        one.Set(CSharpLanguage.ValueProperty, "1");

        // public int[] Levels() { return [0, 1, 2]; } - the collection expression, which is the newer syntax in one line.
        var levels = Method(model, cls, "public", isStatic: false, "int[]", "Levels", out var levelsBody);
        var ret = model.NewChild(levelsBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);
        var collection = model.NewChild(ret, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.CollectionExpression);

        foreach (var value in new[] { "0", "1", "2" })
        {
            model.NewChild(collection, CSharpConceptKeys.ElementsSlot, CSharpConceptKeys.IntLiteral)
                .Set(CSharpLanguage.ValueProperty, value);
        }

        // public void Repeat() { for (int i = 0; i < 3; i++) { Console.WriteLine(message); } } - the counted loop, whose
        // header is only writable because the initializer is a statement (it draws the first `;`) and the increment is
        // an expression (it draws none).
        var repeat = Method(model, cls, "public", isStatic: false, "void", "Repeat", out var repeatBody);
        var loop = model.NewChild(repeatBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.For);
        var local = model.NewChild(loop, CSharpConceptKeys.InitializerSlot, CSharpConceptKeys.LocalDeclaration);
        var localType = model.NewChild(local, "type", CSharpConceptKeys.TypeReference);
        var start = model.NewChild(local, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.IntLiteral);
        var bound = model.NewChild(loop, CSharpConceptKeys.ConditionSlot, CSharpConceptKeys.Binary);
        var beforeBound = model.NewChild(bound, "left", CSharpConceptKeys.Name);
        var limit = model.NewChild(bound, "right", CSharpConceptKeys.IntLiteral);
        var step = model.NewChild(loop, CSharpConceptKeys.IncrementSlot, CSharpConceptKeys.Unary);
        var stepped = model.NewChild(step, CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Name);
        var loopBody = model.NewChild(loop, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var repeated = model.NewChild(loopBody, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.ExpressionStatement);
        var repeatedCall = model.NewChild(repeated, "expression", CSharpConceptKeys.Call);
        var repeatedCallee = model.NewChild(repeatedCall, "callee", CSharpConceptKeys.MemberAccess);
        var repeatedConsole = model.NewChild(repeatedCallee, "target", CSharpConceptKeys.Name);
        var repeatedArgument = model.NewChild(repeatedCall, CSharpConceptKeys.ArgumentsSlot, CSharpConceptKeys.Name);

        local.Set(CSharpLanguage.NameSlot, "i");
        localType.Set(CSharpLanguage.NameSlot, "int");
        start.Set(CSharpLanguage.ValueProperty, "0");
        bound.Set(CSharpLanguage.OperatorSlot, CSharpBinaryOperator.LessThan);
        beforeBound.SetReference(CSharpConceptKeys.TargetSlot, local);
        limit.Set(CSharpLanguage.ValueProperty, "3");
        step.Set(CSharpLanguage.UnaryOperatorSlot, CSharpUnaryOperator.Increment);
        step.Set(CSharpLanguage.IsPostfixSlot, "true");
        stepped.SetReference(CSharpConceptKeys.TargetSlot, local);
        repeatedCallee.Set(CSharpLanguage.MemberSlot, "WriteLine");
        repeatedConsole.SetReference(CSharpConceptKeys.TargetSlot, NodeId.New(), "Console");
        repeatedArgument.SetReference(CSharpConceptKeys.TargetSlot, message);

        _ = greet;
        _ = levels;
        _ = repeat;

        return model;

        static Node Field(Model model, Node cls, string visibility, string type, string name, string literal, string value)
        {
            var field = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Field);
            var fieldType = model.NewChild(field, "type", CSharpConceptKeys.TypeReference);
            var initializer = model.NewChild(field, CSharpConceptKeys.ValueSlot, literal);

            field.Set(CSharpLanguage.VisibilitySlot, visibility);
            field.Set(CSharpLanguage.NameSlot, name);
            fieldType.Set(CSharpLanguage.NameSlot, type);
            initializer.Set(CSharpLanguage.ValueProperty, value);

            return field;
        }

        static Node Method(Model model, Node cls, string visibility, bool isStatic, string type, string name, out Node body)
        {
            var method = model.NewChild(cls, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
            var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);

            method.Set(CSharpLanguage.VisibilitySlot, visibility);
            method.Set(CSharpLanguage.NameSlot, name);

            if (isStatic)
            {
                method.Set(CSharpLanguage.IsStaticSlot, "true");
            }

            returnType.Set(CSharpLanguage.NameSlot, type);
            body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);

            return method;
        }
    }
    /// <summary>A declared importer, which is how a language written as a project reads a file back.</summary>
    /// <remarks>
    /// Built through the import language's own API rather than loaded from the sample folder, for the reason this class
    /// gives: the picture must not depend on where the sources sit. It is the <c>relaxed</c> importer's shape - a quoted
    /// value, a comment stepped over, and a rule per concept - because that is the one that shows a token class and a
    /// rule in one drawing.
    /// </remarks>
    internal static Model DeclaredImporter()
    {
        var model = ImportLanguage.NewModel();
        var importer = model.Roots[0];

        importer.Set(ImportLanguage.NameSlot, "relaxed");

        var text = model.NewChild(importer, ImportLanguage.TokensSlot, ImportConceptKeys.Quoted);
        text.Set(ImportLanguage.TokenNameSlot, "Text");

        var comment = model.NewChild(importer, ImportLanguage.TokensSlot, ImportConceptKeys.Skipped);
        comment.Set(ImportLanguage.TokenNameSlot, "Comment");
        comment.Set(ImportLanguage.PrefixSlot, "#");

        var pipeline = model.NewChild(importer, ImportLanguage.RulesSlot, ImportConceptKeys.Rule);
        pipeline.Set(ImportLanguage.ConceptKeySlot, "Pipeline.Pipeline");

        model.NewChild(pipeline, ImportLanguage.PartsSlot, ImportConceptKeys.Literal).Set(ImportLanguage.TextSlot, "pipeline");
        model.NewChild(pipeline, ImportLanguage.PartsSlot, ImportConceptKeys.Literal).Set(ImportLanguage.TextSlot, "=");

        var name = model.NewChild(pipeline, ImportLanguage.PartsSlot, ImportConceptKeys.Capture);
        name.Set(ImportLanguage.SlotSlot, "name");
        name.Set(ImportLanguage.TokenSlot, "Text");

        var stages = model.NewChild(pipeline, ImportLanguage.PartsSlot, ImportConceptKeys.Repeat);
        stages.Set(ImportLanguage.SlotSlot, "stages");
        stages.Set(ImportLanguage.SeparatorSlot, "");
        stages.Set(ImportLanguage.MinimumSlot, "0");

        var stage = model.NewChild(importer, ImportLanguage.RulesSlot, ImportConceptKeys.Rule);
        stage.Set(ImportLanguage.ConceptKeySlot, "Pipeline.Stage");

        model.NewChild(stage, ImportLanguage.PartsSlot, ImportConceptKeys.Literal).Set(ImportLanguage.TextSlot, "stage");
        model.NewChild(stage, ImportLanguage.PartsSlot, ImportConceptKeys.Literal).Set(ImportLanguage.TextSlot, "=");

        var command = model.NewChild(stage, ImportLanguage.PartsSlot, ImportConceptKeys.Capture);
        command.Set(ImportLanguage.SlotSlot, "command");
        command.Set(ImportLanguage.TokenSlot, "Text");

        return model;
    }

    /// <summary>The declared expressions language, opened once so its document and its surface cannot disagree.</summary>
    /// <remarks>
    /// This is the one document of the gallery read from a folder rather than built through the model API, and it has
    /// to be: the picture is of the <c>typeof</c> annotation, and the type of <c>1 &lt; 2</c> exists only once the
    /// project's own type rules are lowered - a model built by hand would have to restate them. The folder is the copy
    /// this tool's build puts in its output, exactly as the shell shots open the pipeline sample, so the picture is of
    /// the documents a user opens.
    /// </remarks>
    internal static LanguageProject DeclaredExpressions { get; } = Open("declared-expressions");

    /// <summary>The sample document of the declared expressions language: an <c>if</c> over a comparison.</summary>
    /// <remarks>
    /// Every literal in it is drawn with its type beside it, which is what the concept's own editor decides - the
    /// notation says <c>@type@</c> after the value, and the annotation the projection computes says <c>int</c>.
    /// </remarks>
    internal static Model DeclaredExpression()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "declared-expressions", "expressions.mps");

        return DocumentFile.TryLoad(path, DeclaredExpressions.NewProject(), out var error)
            ?? throw new InvalidOperationException(error);
    }

    /// <summary>Opens one of the declaration samples from the copy this tool's build made of it.</summary>
    private static LanguageProject Open(string name) =>
        LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, name), out var error)
            ?? throw new InvalidOperationException(error);
}