using Projectional.Checks;
using Projectional.Core;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Samples.FeatureModel;
using Projectional.Samples.SqlSchema;
using Projectional.Samples.StateMachine;
using Projectional.TypeRules;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The spelling each shipped language declares for its names, and the repair <c>STD004</c> offers for a name written
/// in another convention.
/// </summary>
/// <remarks>
/// <see cref="NameRuleTests"/> proves the mechanism; this is the other half - that the languages this repository
/// ships actually use it, each with the convention its own names are written in, and that the repair writes the
/// spelling that convention asks for. The document is one root of the concept, because every one of them is
/// nameable wherever it stands.
/// </remarks>
public sealed class ShippedNameRulesTests
{
    [Theory]
    [InlineData("Rules.Ruleset", "payroll", "Payroll")]
    [InlineData("Rules.Rule", "approved rule", "ApprovedRule")]
    [InlineData("Expressions.Name", "Amount", "amount")]
    [InlineData("Sm.Machine", "release", "Release")]
    [InlineData("Sm.State", "idle", "Idle")]
    [InlineData("Fm.Model", "release", "Release")]
    [InlineData("Fm.Feature", "core feature", "CoreFeature")]
    [InlineData("Sql.Schema", "Shop", "shop")]
    [InlineData("Sql.Table", "Person", "person")]
    [InlineData("Sql.Column", "CityId", "city_id")]
    [InlineData("TypeRule.Named", "amount", "Amount")]
    [InlineData("TypeRule.Field", "CityId", "cityId")]
    public void ANameInAnotherConventionIsReportedWithTheRepairTheLanguageAsksFor(
        string conceptKey,
        string name,
        string proposal)
    {
        var model = new Project(RegistryOf(conceptKey)).NewModel("document");
        var node = model.NewNode(conceptKey);
        model.AddRoot(node);
        node.Set("name", name);

        var fix = Assert.Single(new IdentifierNameCheck().Find(model));

        Assert.Equal($"Use \"{proposal}\"", fix.Label);
    }

    private static ConceptRegistry RegistryOf(string conceptKey) => conceptKey switch
    {
        _ when conceptKey.StartsWith("Rules.", StringComparison.Ordinal) => RulesLanguage.Concepts,
        _ when conceptKey.StartsWith("Expressions.", StringComparison.Ordinal) => ExpressionsLanguage.Concepts,
        _ when conceptKey.StartsWith("Sm.", StringComparison.Ordinal) => StateMachineLanguage.Concepts,
        _ when conceptKey.StartsWith("Fm.", StringComparison.Ordinal) => FeatureModelLanguage.Concepts,
        _ when conceptKey.StartsWith("Sql.", StringComparison.Ordinal) => SqlSchemaLanguage.Concepts,
        _ => TypeRuleLanguage.Concepts,
    };
}