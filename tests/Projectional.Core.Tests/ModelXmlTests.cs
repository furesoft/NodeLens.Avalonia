using System.Text;
using Projectional.Persistence;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The stored form of a model: the document layout itself, and the promise that reading and
/// writing it again changes nothing.
/// </summary>
public sealed class ModelXmlTests
{
    /// <summary>A document written by hand, so that the tests pin the layout and not just a round trip.</summary>
    private const string Golden = """
        <?xml version="1.0" encoding="utf-8"?>
        <model formatVersion="1" id="11111111-1111-1111-1111-111111111111" name="Payroll">
          <nodes>
            <node id="22222222-2222-2222-2222-222222222222" concept="Test.Rule" name="Payroll">
              <properties>
                <name>Payroll</name>
                <enabled>true</enabled>
              </properties>
              <references role="tag" target="33333333-3333-3333-3333-333333333333" resolveInfo="Salary" />
              <children role="body">
                <node id="44444444-4444-4444-4444-444444444444" concept="Test.Body">
                  <children role="steps">
                    <node id="55555555-5555-5555-5555-555555555555" concept="Test.Step">
                      <properties>
                        <text>Pay</text>
                      </properties>
                    </node>
                  </children>
                </node>
              </children>
            </node>
          </nodes>
        </model>
        """;

    private static readonly NodeId RuleId = NodeId.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly NodeId TagTargetId = NodeId.Parse("33333333-3333-3333-3333-333333333333");

    private static string Text => Golden.Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void ReadingAndWritingAgainKeepsTheDocumentByteForByte()
    {
        var model = ModelXml.Deserialize(TestLanguage.NewProject(), Text);

        Assert.Equal(Text, ModelXml.Serialize(model));
    }

    [Fact]
    public void AFloatPropertyRoundTripsInItsCanonicalSpelling()
    {
        // A property type is stored as text, so what the file holds is what Format wrote. Reading it back and
        // writing again must not re-spell it - that is the promise every other property type keeps too.
        var model = TestLanguage.NewProject().NewModel("steps");
        var step = model.NewNode(TestLanguage.Step);

        model.AddRoot(step);

        step.Set("weight", 0.1);
        step.Set("index", 3);

        var written = ModelXml.Serialize(model);
        var reloaded = ModelXml.Deserialize(TestLanguage.NewProject(), written);

        Assert.Contains("<weight>0.1</weight>", written, StringComparison.Ordinal);
        Assert.Equal(written, ModelXml.Serialize(reloaded));
        Assert.Equal(0.1, reloaded.Roots[0].GetFloat("weight"));
    }

    [Fact]
    public void ReadingKeepsIdentityValuesAndReferences()
    {
        var model = ModelXml.Deserialize(TestLanguage.NewProject(), Text);

        var rule = Assert.Single(model.Roots);
        Assert.Equal("Payroll", model.Name);
        Assert.Equal(RuleId, rule.Id);
        Assert.Equal(TestLanguage.Rule, rule.Concept.Key);
        Assert.Equal("Payroll", rule.Name);
        Assert.Equal("true", rule.Get("enabled"));

        var tag = rule.GetReference("tag");
        Assert.Equal(TagTargetId, tag!.Value.Target);
        Assert.Equal("Salary", tag.Value.ResolveInfo);
        Assert.Null(rule.GetReferenceTarget("tag"));
    }

    [Fact]
    public void ReadingAndWritingAgainKeepsAWholeTreeByteForByte()
    {
        var model = BuildModel(TestLanguage.NewProject());

        var first = ModelXml.Serialize(model);
        var again = ModelXml.Serialize(ModelXml.Deserialize(TestLanguage.NewProject(), first));

        Assert.Equal(first, again);
    }

    [Fact]
    public void PropertiesAreWrittenInConceptDeclarationOrder()
    {
        var project = TestLanguage.NewProject();
        var model = project.NewModel();
        var step = model.NewNode(TestLanguage.Step);

        // Set in the opposite order of the declaration to prove the writer does not follow it.
        step.Set("status", "draft");
        step.Set("index", 2);
        step.Set("text", "Pay");

        var document = ModelXml.Serialize(model);

        Assert.True(
            document.IndexOf("<text>", StringComparison.Ordinal) < document.IndexOf("<index>", StringComparison.Ordinal),
            document);
        Assert.True(
            document.IndexOf("<index>", StringComparison.Ordinal) < document.IndexOf("<status>", StringComparison.Ordinal),
            document);
    }

    [Fact]
    public void ChildSlotsAreWrittenInConceptDeclarationOrder()
    {
        var project = TestLanguage.NewProject();
        var model = project.NewModel();
        var body = model.NewNode(TestLanguage.Body);

        // Filled in the opposite order of the declaration again.
        model.NewChild(body, "notes", TestLanguage.Note).Set("text", "first");
        model.NewChild(body, "steps", TestLanguage.Step).Set("text", "second");

        var document = ModelXml.Serialize(model);

        Assert.True(
            document.IndexOf("role=\"steps\"", StringComparison.Ordinal)
                < document.IndexOf("role=\"notes\"", StringComparison.Ordinal),
            document);
    }

    [Fact]
    public void UnsetSlotsAndEmptyChildSlotsAreNotWritten()
    {
        var project = TestLanguage.NewProject();
        var model = project.NewModel();
        model.NewNode(TestLanguage.Tag);

        var document = ModelXml.Serialize(model);

        Assert.DoesNotContain("<children", document, StringComparison.Ordinal);
        Assert.DoesNotContain("<properties", document, StringComparison.Ordinal);
        Assert.DoesNotContain("<references", document, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyModelStillHasANodesElement()
    {
        var project = TestLanguage.NewProject();
        var model = project.NewModel();

        var document = ModelXml.Serialize(model);

        Assert.Contains("<nodes />", document, StringComparison.Ordinal);
        Assert.Equal(document, ModelXml.Serialize(ModelXml.Deserialize(TestLanguage.NewProject(), document)));
    }

    [Fact]
    public void AModelWithoutANameHasNoNameAttribute()
    {
        var project = TestLanguage.NewProject();
        var model = project.NewModel();

        var document = ModelXml.Serialize(model);

        Assert.Contains($"<model formatVersion=\"1\" id=\"{model.Id}\">", document, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeWhoseNameIsComputedHasNoNameAttribute()
    {
        // The attribute exists so a file is greppable and a reference readable without chasing an id - it is not stored
        // data. A name a concept computes is derived from the language that computes it, so writing it would put a
        // stale answer in the file and make a document's bytes change when a name function does.
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "Computed.Item",
                [new PropertySlot("key", PropertyType.String)],
                nameOf: node => $"computed-{node.GetOrNull("key")}"),
        ]);

        var model = new Project(concepts).NewModel();
        var item = model.NewNode("Computed.Item");
        item.Set("key", "seven");
        model.AddRoot(item);

        var document = ModelXml.Serialize(model);

        Assert.Equal("computed-seven", item.Name);
        Assert.Contains($"<node id=\"{item.Id}\" concept=\"Computed.Item\">", document, StringComparison.Ordinal);

        // And the round trip is stable in both directions: nothing was dropped that a second save would invent.
        Assert.Equal(document, ModelXml.Serialize(ModelXml.Deserialize(new Project(concepts), document)));
    }

    [Fact]
    public void OutputIsUtf8WithoutABomWithLineFeedsAndTwoSpaceIndent()
    {
        var model = BuildModel(TestLanguage.NewProject());

        using var buffer = new MemoryStream();
        ModelXml.WriteTo(model, buffer);

        var bytes = buffer.ToArray();
        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "the document must not start with a byte order mark");

        var document = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("\r", document, StringComparison.Ordinal);
        Assert.Contains("\n  <nodes>", document, StringComparison.Ordinal);
        Assert.Contains("\n    <node ", document, StringComparison.Ordinal);
    }

    [Fact]
    public void ValuesWithMarkupSurviveTheRoundTrip()
    {
        const string tricky = "a < b & c > d \"quoted\" 'single'";
        var model = BuildModel(TestLanguage.NewProject(), tricky);

        var again = ModelXml.Deserialize(TestLanguage.NewProject(), ModelXml.Serialize(model));

        Assert.Equal(tricky, again.Roots[0].Get("name"));
    }

    [Fact]
    public void LineBreaksInsideAValueSurviveTheRoundTrip()
    {
        const string twoLines = "line one\r\nline two\nline three";
        var model = BuildModel(TestLanguage.NewProject(), twoLines);

        var again = ModelXml.Deserialize(TestLanguage.NewProject(), ModelXml.Serialize(model));

        Assert.Equal(twoLines, again.Roots[0].Get("name"));
    }

    [Fact]
    public void AnEmptyStringIsAValueAndStaysOne()
    {
        var model = BuildModel(TestLanguage.NewProject(), string.Empty);

        var again = ModelXml.Deserialize(TestLanguage.NewProject(), ModelXml.Serialize(model));

        Assert.True(again.Roots[0].IsSet("name"));
        Assert.Equal(string.Empty, again.Roots[0].Get("name"));
    }

    [Fact]
    public void ADanglingReferenceKeepsItsTargetAndItsResolveInfo()
    {
        var project = TestLanguage.NewProject();
        var model = project.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var gone = NodeId.New();
        rule.SetReference("tag", gone, "Gone");

        var again = ModelXml.Deserialize(TestLanguage.NewProject(), ModelXml.Serialize(model));

        var reference = Assert.Single(again.Roots).GetReference("tag");
        Assert.Equal(gone, reference!.Value.Target);
        Assert.Equal("Gone", reference.Value.ResolveInfo);
        Assert.Null(again.Roots[0].GetReferenceTarget("tag"));
    }

    [Fact]
    public void AReferenceMayPointAtANodeThatComesLater()
    {
        var project = TestLanguage.NewProject();
        var model = project.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var tag = model.NewNode(TestLanguage.Tag);
        rule.SetReference("tag", tag);

        var again = ModelXml.Deserialize(TestLanguage.NewProject(), ModelXml.Serialize(model));

        var target = again.Roots[0].GetReference("tag")!.Value.Target;
        Assert.Equal(Assert.Single(again.Roots, node => node.Concept.Key == TestLanguage.Tag).Id, target);
    }

    [Fact]
    public void AnUnknownConceptIsAFailure()
    {
        var broken = Text.Replace("concept=\"Test.Step\"", "concept=\"Test.Missing\"", StringComparison.Ordinal);

        Assert.Throws<ModelFormatException>(() => ModelXml.Deserialize(TestLanguage.NewProject(), broken));
    }

    [Fact]
    public void AValueTheLanguageRefusesIsAFailure()
    {
        var broken = Text.Replace("<enabled>true</enabled>", "<enabled>maybe</enabled>", StringComparison.Ordinal);

        Assert.Throws<ModelFormatException>(() => ModelXml.Deserialize(TestLanguage.NewProject(), broken));
    }

    [Fact]
    public void ASlotTheConceptDoesNotDeclareIsAFailure()
    {
        var broken = Text.Replace(
            "<text>Pay</text>",
            "<text>Pay</text>\n                        <nonsense>1</nonsense>",
            StringComparison.Ordinal);

        Assert.Throws<ModelFormatException>(() => ModelXml.Deserialize(TestLanguage.NewProject(), broken));
    }

    [Fact]
    public void ADuplicateNodeIdIsAFailure()
    {
        var duplicated = Text.Replace(
            "<node id=\"55555555-5555-5555-5555-555555555555\" concept=\"Test.Step\">",
            "<node id=\"44444444-4444-4444-4444-444444444444\" concept=\"Test.Step\">",
            StringComparison.Ordinal);

        Assert.Throws<ModelFormatException>(() => ModelXml.Deserialize(TestLanguage.NewProject(), duplicated));
    }

    [Fact]
    public void ANewerFormatVersionIsRefusedLoudly()
    {
        var newer = Text.Replace("formatVersion=\"1\"", "formatVersion=\"2\"", StringComparison.Ordinal);

        var exception = Assert.Throws<ModelFormatException>(
            () => ModelXml.Deserialize(TestLanguage.NewProject(), newer));

        Assert.Contains("version 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingFormatVersionIsReadAsTheOldestFormat()
    {
        var oldest = Text.Replace(" formatVersion=\"1\"", string.Empty, StringComparison.Ordinal);

        var model = ModelXml.Deserialize(TestLanguage.NewProject(), oldest);

        Assert.Single(model.Roots);
    }

    [Fact]
    public void SomethingThatIsNotAModelFileIsAFailure()
    {
        Assert.Throws<ModelFormatException>(() => ModelXml.Deserialize(TestLanguage.NewProject(), "<notes />"));
        Assert.Throws<ModelFormatException>(() => ModelXml.Deserialize(TestLanguage.NewProject(), "not xml at all"));
    }

    [Fact]
    public void AFailedLoadLeavesNothingBehind()
    {
        var project = TestLanguage.NewProject();
        var broken = Text.Replace("concept=\"Test.Step\"", "concept=\"Test.Missing\"", StringComparison.Ordinal);

        Assert.Throws<ModelFormatException>(() => ModelXml.Deserialize(project, broken));

        Assert.Empty(project.Models);
        Assert.Equal(0, project.NodeCount);
    }

    [Fact]
    public void SerializingAModelInsideATransactionIsRefused()
    {
        var model = TestLanguage.NewModel();
        using var transaction = model.BeginTransaction();

        Assert.Throws<InvalidOperationException>(() => ModelXml.Serialize(model));
    }

    private static Model BuildModel(Project project, string ruleName = "Payroll")
    {
        var model = project.NewModel(ruleName);

        // The rule is the first root on purpose: several tests read Roots[0] as the rule.
        var rule = model.NewNode(TestLanguage.Rule);
        rule.Set("name", ruleName);

        var tag = model.NewNode(TestLanguage.Tag);
        tag.Set("label", "Salary");
        rule.SetReference("tag", tag);

        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);
        step.Set("text", "Pay");
        step.Set("index", 3);
        step.Set("due", new DateOnly(2026, 1, 2));
        step.Set("status", "draft");

        var notes = model.NewChild(body, "notes", TestLanguage.Note);
        notes.Set("text", "check the rounding");

        return model;
    }
}
