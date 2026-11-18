using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The framework's catalogue: four checks a language includes by naming them, and no code of its own.
/// </summary>
/// <remarks>
/// The test that carries the slice is <see cref="ALanguageIncludingThemNeedsNoCodeOfItsOwn"/>: it builds a
/// document with one problem of each kind, asks the standard set, and expects the findings in catalogue
/// order with usable repairs - without a single line of check code written for the experiment.
/// </remarks>
public sealed class StandardChecksTests
{
    [Fact]
    public void TheCatalogueIsGroupedTheWayTheDesignGroupsIt()
    {
        Assert.Collection(
            StandardChecks.Names,
            check => Assert.IsType<PlaceholderNameCheck>(check),
            check => Assert.IsType<SurroundingWhitespaceCheck>(check));

        Assert.IsType<EmptyChildSlotCheck>(StandardChecks.Structure[0]);
        Assert.IsType<MinimumChildCountCheck>(StandardChecks.Structure[1]);
        Assert.IsType<BinaryGroupingCheck>(Assert.Single(StandardChecks.Expressions));
        Assert.Equal(5, StandardChecks.All.Count);
    }

    [Fact]
    public void ALanguageIncludingThemNeedsNoCodeOfItsOwn()
    {
        var model = Document();

        var fixes = StandardChecks.All.Find(model);

        Assert.Equal(
            [PlaceholderNameCheck.Code, SurroundingWhitespaceCheck.Code, EmptyChildSlotCheck.Code, BinaryGroupingCheck.Code],
            fixes.Select(fix => fix.Diagnostic.Code));

        foreach (var fix in fixes)
        {
            Assert.False(string.IsNullOrWhiteSpace(fix.Label));
            Assert.False(string.IsNullOrWhiteSpace(fix.Description));
            Assert.Equal(EditSafety.Plain, fix.Safety);
            Assert.False(fix.IsDisabled);
        }
    }

    [Fact]
    public void AGroupCanBeTakenWithoutTheRest()
    {
        // A language that declares no names takes the structure check and pays nothing for the name rules.
        var structure = new CheckSet(StandardChecks.Structure);

        var fixes = structure.Find(Document());

        Assert.Equal(EmptyChildSlotCheck.Code, Assert.Single(fixes).Diagnostic.Code);
    }

    [Fact]
    public void ASetCanBeExtendedWithALanguagesOwnCheck()
    {
        var set = StandardChecks.All.With(new StubCheck("mine"));

        var fixes = set.Find(Document());

        Assert.Equal(5, fixes.Count);
        Assert.Equal("mine", fixes[^1].Label);
    }

    [Fact]
    public void TheSampleDocumentIsClean()
    {
        // The sample is the document the design shows, so it must not need a repair of its own.
        Assert.Empty(StandardChecks.All.Find(PayrollSample.NewSampleModel()));
    }

    /// <summary>A document with one problem per standard check that inspects structure.</summary>
    /// <remarks>
    /// The ruleset keeps its placeholder name (<c>STD001</c>), the first rule's name has a trailing space
    /// (<c>STD003</c>) and no condition (<c>STD010</c>), and the second rule's condition multiplies a sum
    /// (<c>STD040</c>).
    /// </remarks>
    private static Model Document()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];

        var spaced = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        spaced.Set("name", "Spaced ");

        var grouped = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        grouped.Set("name", "Grouped");

        var product = model.NewChild(grouped, "condition", ExpressionsConceptKeys.Binary);
        product.Set("operator", "*");

        var sum = model.NewChild(product, "left", ExpressionsConceptKeys.Binary);
        sum.Set("operator", "+");
        Int(model, sum, "left", 1);
        Int(model, sum, "right", 2);
        Int(model, product, "right", 3);

        return model;
    }

    private static void Int(Model model, Node parent, string slot, int value) =>
        model.NewChild(parent, slot, ExpressionsConceptKeys.IntLiteral).Set("value", value);
}
