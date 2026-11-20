using Projectional.Core;
using Projectional.Expressions;
using Projectional.ImportRules;
using Projectional.Samples.FeatureModel;
using Projectional.Samples.SqlSchema;
using Projectional.Samples.StateMachine;
using Projectional.Testing;

namespace Projectional.Screenshots;

/// <summary>The documents the example-language screenshots are taken of.</summary>
/// <remarks>
/// They are the corpus documents of the three samples, built here through the public model API rather than loaded
/// from their files, for the same reason the feature shots build theirs: the tool must not depend on where the
/// sources sit. Each is a little longer than its corpus so the picture shows a language rather than a fragment.
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
}