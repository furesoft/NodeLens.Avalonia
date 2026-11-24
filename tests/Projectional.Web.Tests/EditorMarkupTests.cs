using Xunit;

namespace Projectional.Web.Tests;

public sealed class EditorMarkupTests
{
    [Fact]
    public void TheCanvasCarriesTheStyledRunsAndTheCaret()
    {
        var language = WebLanguages.Rules;
        using var document = new EditorDocument("id", "payroll", language, language.NewSample());

        var html = EditorMarkup.Canvas(document, new CellMetrics(10, 20));

        Assert.Contains("class=\"pn-canvas\"", html, StringComparison.Ordinal);
        Assert.Contains("pn-keyword", html, StringComparison.Ordinal);
        Assert.Contains(">ruleset<", html, StringComparison.Ordinal);
        Assert.Contains("data-role=\"caret\"", html, StringComparison.Ordinal);
        Assert.Contains("left:", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TextFromTheModelIsEscaped()
    {
        var language = WebLanguages.Rules;
        var model = language.NewSample();
        model.Roots[0].Set("name", "<Payroll>");

        using var document = new EditorDocument("id", "payroll", language, model);
        var html = EditorMarkup.Canvas(document, new CellMetrics(10, 20));

        Assert.Contains("&lt;Payroll&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<Payroll>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ATabIsOneColumnBecauseTheMeasureSaidSo()
    {
        var language = WebLanguages.Rules;
        var model = language.NewSample();
        model.Roots[0].Set("name", "Pay\troll");

        using var document = new EditorDocument("id", "payroll", language, model);
        var html = EditorMarkup.Canvas(document, new CellMetrics(10, 20));

        Assert.Contains("Pay roll", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Pay\troll", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyMetricsIsReplacedRatherThanProducingAZeroSizedPage()
    {
        var metrics = new CellMetrics(0, 0);

        Assert.True(metrics.CellWidth > 0);
        Assert.True(metrics.CellHeight > 0);
        Assert.Equal(CellMetrics.Default, metrics);
    }
}
