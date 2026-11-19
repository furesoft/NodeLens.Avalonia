using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// <c>STD004</c>: a name a concept requires to be an identifier, spelled as something else.
/// </summary>
/// <remarks>
/// The rule is only offered when a concept declares it, so the tests build a two-concept language where one
/// concept asks for identifiers and one does not - the two halves of "the catalogue reports what the language
/// asked for, and nothing else".
/// </remarks>
public sealed class IdentifierNameCheckTests
{
    private const string IdentifierKey = "Test.Identifier";
    private const string ProseKey = "Test.Prose";
    private const string SnakesKey = "Test.Snakes";
    private const string PlaceholderKey = "Test.Placeholder";

    private static IdentifierNameCheck Check => new();

    [Fact]
    public void ANameThatIsNoIdentifierIsReported()
    {
        var (model, node) = Document("my rule");

        var fix = Assert.Single(Check.Find(model));
        var finding = fix.Diagnostic;

        Assert.Equal(IdentifierNameCheck.Code, finding.Code);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
        Assert.Equal("name", finding.Role);
        Assert.Equal(node.Id, finding.Target);
        Assert.Equal("Use \"my_rule\"", fix.Label);
        Assert.Equal(EditSafety.Plain, fix.Safety);
    }

    [Fact]
    public void ANameInTheWrongConventionIsReportedWithTheRuleItBreaks()
    {
        // The finding quotes the convention, because "not an identifier" would be wrong about `myRule` when the
        // language asked for snake_case.
        var (model, _) = Document("myRule", SnakesKey);

        var fix = Assert.Single(Check.Find(model));

        Assert.Contains("snake_case", fix.Diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal("Use \"my_rule\"", fix.Label);
    }

    [Fact]
    public void ANameThatIsItsConceptPlaceholderIsNotReported()
    {
        // `unnamed` is not PascalCase, but nobody typed it: STD001 is the finding for an unnamed node, and a
        // spelling warning about a name that is about to be replaced would be noise.
        var (model, _) = Document("unnamed", PlaceholderKey);

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void ANameWithWhitespaceAroundItIsLeftToTheWhitespaceCheck()
    {
        // `STD003` repairs the space; the spelling of what is left is the next finding, not a second one now.
        var (model, _) = Document("my rule ");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void ANameThatIsAnIdentifierIsNotReported()
    {
        var (model, _) = Document("myRule");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void AConceptThatDeclaresNoRuleIsNotReported()
    {
        var (model, _) = Document("my rule", ProseKey);

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void AnEmptyNameIsNotReported()
    {
        // "Nobody has named this yet" is STD001's finding; a second one here would be two reports about one
        // absence.
        var (model, _) = Document(string.Empty);

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void TheFixNamesThePropertyAndTheRepairedValue()
    {
        var (model, node) = Document("my rule");
        var fix = Assert.Single(Check.Find(model));

        var action = Assert.IsType<SetPropertyAction>(fix.Create(new FixContext(model, fix.Diagnostic, node)));

        Assert.Equal(CellAddress.OfSlot(node.Id, "name"), action.Address);
        Assert.Equal("my_rule", action.Text);
    }

    [Fact]
    public void TheFixDeclinesWhenTheTargetIsGone()
    {
        var (model, node) = Document("my rule");
        var fix = Assert.Single(Check.Find(model));

        Assert.Null(fix.Create(new FixContext(model, fix.Diagnostic, null)));
    }

    /// <summary>One document holding one node whose name is <paramref name="name"/>.</summary>
    private static (Model Model, Node Node) Document(string name, string conceptKey = IdentifierKey)
    {
        var registry = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                IdentifierKey,
                [new PropertySlot("name", PropertyType.String)],
                nameSlot: "name",
                nameRule: NameRule.Identifier),
            new ConceptDeclaration(
                ProseKey,
                [new PropertySlot("name", PropertyType.String)],
                nameSlot: "name"),
            new ConceptDeclaration(
                SnakesKey,
                [new PropertySlot("name", PropertyType.String)],
                nameSlot: "name",
                nameRule: NameRule.Snake),
            new ConceptDeclaration(
                PlaceholderKey,
                [new PropertySlot("name", PropertyType.String, "unnamed")],
                nameSlot: "name",
                nameRule: NameRule.Pascal),
        ]);

        var model = new Project(registry).NewModel("document");
        var node = model.NewNode(conceptKey);
        model.AddRoot(node);

        if (name.Length > 0)
        {
            node.Set("name", name);
        }

        return (model, node);
    }
}
