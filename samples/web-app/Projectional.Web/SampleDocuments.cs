using Projectional.Core;
using Projectional.Samples.FeatureModel;
using Projectional.Samples.SqlSchema;

namespace Projectional.Web;

/// <summary>The documents the SQL-schema and feature-model examples open, built through the public model API.</summary>
/// <remarks>
/// The desktop shell takes these documents from the screenshot corpus and from declared folders; a browser cannot
/// read either, so the sample builds the same shapes here. Building a document is what a host embedding the editor
/// does, and it keeps the sample's content independent of where the repository happens to put a file.
/// </remarks>
internal static class SampleDocuments
{
    /// <summary>A shop schema: three tables and two foreign keys, so references have something to show.</summary>
    internal static Model SqlSchema()
    {
        var model = SqlSchemaLanguage.NewProject().NewModel("shop");
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

        var address = Table(model, schema, "address");
        Column(model, address, "id", "int");
        var addressPerson = Column(model, address, "person_id", "int");
        var addressCity = Column(model, address, "city_id", "int");

        Key(model, address, addressPerson, personId);
        Key(model, address, addressCity, cityId);

        return model;
    }

    /// <summary>A feature model whose selection and rules disagree, so the problems panel has something to say.</summary>
    internal static Model FeatureModel()
    {
        var model = FeatureModelLanguage.NewProject().NewModel("release");
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
}
