using System.Reflection;

using Projectional.Behaviours;
using Projectional.Core;
using Projectional.CSharp;
using Projectional.LanguageExport;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Structures;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A language folder declares what its concepts are called: the documents load, lower, and the name a concept computes is
/// the name its nodes answer with.
/// </summary>
/// <remarks>
/// The folder is written here rather than copied from <c>samples/</c>, because the sample languages name their nodes
/// through a name slot: the shape under test - a concept with no name slot, called after what it points at - needs a
/// structure this test declares itself. The sample's own behaviour is asserted where the sample is, in
/// <c>DeclaredAtlasTests</c>.
/// </remarks>
public sealed class LanguageProjectBehaviourTests
{
    [Fact]
    public void ABehaviourFolderNamesAConceptThatHasNoNameSlot()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Link", slot: "to");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.Single(language.BehaviourDocuments);

        var project = language.NewProject();
        var item = project.NewModel("items").NewNode("Doc.Item");
        item.Set("name", "Bedrock");

        var link = project.NewModel("links").NewNode("Doc.Link");
        link.SetReference("to", item);

        // `Doc.Link` has no name slot, so the only answer there can be is the one its folder declares.
        Assert.False(link.IsNamed);
        Assert.Equal("Bedrock", link.Name);
    }

    [Fact]
    public void ABehaviourThatNamesAConceptTheStructureDoesNotDeclareIsRefusedWhenTheFolderLoads()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Nope", slot: "to");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("is not a concept of this language", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ALanguageWithoutTheFolderNamesItsNodesThroughTheNameSlotItAlwaysHad()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.Empty(language.BehaviourDocuments);

        var item = language.NewProject().NewModel("items").NewNode("Doc.Item");
        item.Set("name", "Bedrock");

        Assert.Equal("Bedrock", item.Name);
    }

    [Fact]
    public void ABehaviourOnAnAbstractConceptNamesEverySubtype()
    {
        // A name belongs to a concept, and a concept's subtypes inherit it - which is what makes one document enough for
        // a whole family of nodes.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Spot", slot: "to");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        var project = language.NewProject();
        var item = project.NewModel("items").NewNode("Doc.Item");
        item.Set("name", "Bedrock");

        var place = project.NewModel("places").NewNode("Doc.Place");
        place.SetReference("to", item);

        Assert.Equal("Bedrock", place.Name);
    }

    [Fact]
    public void ADependentLanguageKeepsTheNamesItsBaseDeclares()
    {
        // A name is part of a declaration now, so a language that depends on another has to receive the named ones - the
        // declaration the registry was built from, not the one the structure folder wrote.
        using var folder = new TemporaryDirectory();
        var baseFolder = Path.Combine(folder.Root, "Base");
        var dependentFolder = Path.Combine(folder.Root, "Dependent");

        WriteStructure(baseFolder);
        WriteBehaviour(baseFolder, conceptKey: "Doc.Link", slot: "to");
        WriteLanguageRoot(baseFolder, "Base");

        WriteLanguageRoot(dependentFolder, "Dependent", dependsOn: "Base");

        var structure = Path.Combine(dependentFolder, LanguageProject.StructureFolder);
        Directory.CreateDirectory(structure);

        var definition = new DefinitionBuilder();
        definition.Concept("Dep.Thing", slots: [definition.Property("name", "string")], nameSlot: "name");
        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Dep.Thing.mps"));

        var baseLanguage = LanguageProject.Open(baseFolder, out var baseError);

        Assert.Null(baseError);
        Assert.NotNull(baseLanguage);

        var dependent = LanguageProject.Open(dependentFolder, new ResolvedLanguages(baseLanguage), out var error);

        Assert.Null(error);
        Assert.NotNull(dependent);
        Assert.NotNull(dependent.Concepts.Get("Doc.Link").NameFunction);
    }

    private static void WriteLanguageRoot(string directory, string name, string? dependsOn = null)
    {
        var model = new Project(StructureLanguage.Concepts).NewModel("language");
        var root = model.NewNode(StructureConceptKeys.Language);

        root.Set("name", name);
        model.AddRoot(root);

        if (dependsOn is not null)
        {
            model.NewChild(root, "dependencies", StructureConceptKeys.Dependency).Set("name", dependsOn);
        }

        Directory.CreateDirectory(directory);
        ModelFile.Save(model, Path.Combine(directory, LanguageProject.LanguageFile));
    }

    [Fact]
    public void ANameThatShadowsTheOneItInheritsIsWarnedAboutRatherThanRefused()
    {
        // The language is usable - the subtype's own body wins - so this is not an error. It is also almost never what
        // an author means, which is why the loaded project carries a warning rather than staying quiet.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Spot", slot: "to", file: "spot");
        WriteBehaviour(folder.Root, conceptKey: "Doc.Place", slot: "to", file: "place");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        var warning = Assert.Single(language.Warnings);

        Assert.Equal(LanguageProject.BehaviourFolder, warning.Aspect);
        Assert.Contains("'Doc.Place' declares a name it inherits from 'Doc.Spot'", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ALanguageThatNamesEachConceptOnceWarnsAboutNothing()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Link", slot: "to");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.Empty(language.Warnings);
    }

    [Fact]
    public void ACSharpBehaviourInTheSameFolderBindsTheNameTheCompiledLanguageWillCompute()
    {
        // The folder holds one aspect in its two forms: a body of data and a C# method, both for `Doc.Link`. The editor
        // cannot run C#, so it keeps answering with the declarative body; what the folder does with the class is
        // *bind* it, and the warning says so rather than leaving the author to wonder why the method changed nothing.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Link", slot: "to");
        WriteCSharpBehaviour(folder.Root, conceptKey: "Doc.Link");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        // The document is a model of the C# base language, which is what makes it editable as the C# it is - and it is
        // not in BehaviourDocuments, because a C# file is not a declaration the lowering could read.
        Assert.Single(language.CSharpDocuments);
        Assert.Single(language.BehaviourDocuments);
        Assert.Equal("Sample.LinkBehaviour.Name", language.CSharpNames["Doc.Link"]);

        var warning = Assert.Single(language.Warnings);

        Assert.Equal(LanguageProject.BehaviourFolder, warning.Aspect);
        Assert.Contains("'Doc.Link' is named by 'Sample.LinkBehaviour.Name'", warning.Message, StringComparison.Ordinal);
        Assert.Contains("the editor cannot run", warning.Message, StringComparison.Ordinal);

        // The editor's answer is the declarative one, because a method this layer cannot call is not an answer.
        var project = language.NewProject();
        var item = project.NewModel("items").NewNode("Doc.Item");
        var link = project.NewModel("links").NewNode("Doc.Link");

        item.Set("name", "Bedrock");
        link.SetReference("to", item);

        Assert.Equal("Bedrock", link.Name);
    }

    [Fact]
    public void ACompiledLanguageAnswersWithTheCSharpMethodAndNotTheDeclarativeBody()
    {
        // What the binding is for. Both forms name `Doc.Link`, the folder binds the method, and the export writes the
        // binding into the declaration - so the package answers what the C# computes while the folder keeps answering
        // what the data says. The general form wins, which is the whole reason for offering it.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteBehaviour(folder.Root, conceptKey: "Doc.Link", slot: "to");
        WriteCSharpBehaviour(folder.Root, conceptKey: "Doc.Link");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        var exported = LanguageExporter.Export(language, output: ExportOutput.Assembly);
        var assembly = Assembly.Load(Assert.Single(exported.Assemblies).Bytes);

        // The generated class is found by what it holds rather than by its name, because the naming rule is the
        // export's business and a test that repeated it would pass while the export renamed things.
        var generated = assembly.GetTypes().Single(type => type.GetProperty("ConceptDeclarations") is not null);
        var declarations = Assert.IsAssignableFrom<IReadOnlyList<ConceptDeclaration>>(
            generated.GetProperty("ConceptDeclarations")!.GetValue(null));

        var declaration = declarations.Single(each => each.Key == "Doc.Link");
        var nameOf = Assert.IsAssignableFrom<Func<Node, string?>>(declaration.NameOf);

        var project = language.NewProject();
        var item = project.NewModel("items").NewNode("Doc.Item");
        var link = project.NewModel("links").NewNode("Doc.Link");

        item.Set("name", "Bedrock");
        link.SetReference("to", item);

        // The body the method was compiled from answers the node's own concept key, so the two answers cannot be
        // confused with one another: one is the name of the node the reference reaches, the other is a fact about the
        // node itself.
        Assert.Equal("Bedrock", link.Name);
        Assert.Equal("Doc.Link", nameOf(link));

        // The C# file travels into the package beside the declaration that names it, which is what lets the emitted
        // source compile at all.
        Assert.Contains(exported.Files, file => file.Name.EndsWith(".Behaviour.cs", StringComparison.Ordinal));
        Assert.Contains("// behaviour Doc.Link", exported.Files.Single(file => file.Name.EndsWith(".Behaviour.cs", StringComparison.Ordinal)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ACSharpBehaviourForAConceptTheStructureDoesNotDeclareIsRefusedWhenTheFolderLoads()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteCSharpBehaviour(folder.Root, conceptKey: "Doc.Nope");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("'Doc.Nope' is not a concept of this language", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ACSharpBehaviourClosesItsEnumerationNamesAgainstTheLanguageItDefines()
    {
        // The point the two enum concepts exist for: the names refer to enumerations of the language *being defined*.
        // `structure/` declares `Doc.WeekDays`, the behaviour names `enum/Doc.WeekDays/.Thu`, and what the name means
        // is decided here - not by C#, which only ever sees the symbol as text.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteCSharpBehaviour(folder.Root, conceptKey: "Doc.Link", enumeration: "Doc.WeekDays", symbol: "Thu");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.Equal("Sample.LinkBehaviour.Name", language.CSharpNames["Doc.Link"]);
    }

    [Fact]
    public void ACSharpBehaviourThatNamesAnEnumerationTheLanguageDoesNotDeclareIsRefused()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteCSharpBehaviour(folder.Root, conceptKey: "Doc.Link", enumeration: "Doc.Weekday", symbol: "Thu");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("names the enumeration 'Doc.Weekday', which this language does not declare", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ACSharpBehaviourThatNamesASymbolItsEnumerationDoesNotOfferIsRefused()
    {
        // A symbol is text once it is generated, so a wrong one compiles into a comparison that can never be true -
        // the one mistake a compiler cannot catch, and therefore the one this layer has to.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteCSharpBehaviour(folder.Root, conceptKey: "Doc.Link", enumeration: "Doc.WeekDays", symbol: "Tursday");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("is not a symbol of the enumeration 'Doc.WeekDays'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLanguageOffersItsOwnEnumerationsToABehaviourWrittenInCSharp()
    {
        // A behaviour names an enumeration by text, so no declared type can answer the list. The language project is
        // what offers it: the enumerations `structure` declares for `enum/`, and the symbols of the one that was chosen
        // for `/.` - which is how a projectional editor makes a name the user cannot see anywhere else completable.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteCSharpBehaviour(folder.Root, conceptKey: "Doc.Link", enumeration: "Doc.WeekDays", symbol: "Thu");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        var value = language.CSharpDocuments
            .Single()
            .Roots[0]
            .ChildrenInDocumentOrder()
            .SelectMany(Descendants)
            .Single(node => node.Concept.Key == CSharpConceptKeys.EnumValue);

        Assert.Equal(["Doc.WeekDays"], language.Values.ValuesOf(value, CSharpLanguage.EnumSlot));
        Assert.Equal(["Mon", "Thu"], language.Values.ValuesOf(value, CSharpLanguage.MemberSlot));

        // Nothing else is answered: every other slot keeps whatever its declared type says, which is what makes
        // installing the provider harmless.
        Assert.Null(language.Values.ValuesOf(value, "conceptKey"));

        // A half-written value offers nothing for its symbol, because there is no set to choose from until there is an
        // enumeration - which is the honest answer rather than an empty list that closes the door.
        value.Set(CSharpLanguage.EnumSlot, string.Empty);

        Assert.Null(language.Values.ValuesOf(value, CSharpLanguage.MemberSlot));
    }

    private static IEnumerable<Node> Descendants(Node node) =>
        [node, .. node.ChildrenInDocumentOrder().SelectMany(Descendants)];

    private static void WriteStructure(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Doc.Item", slots: [definition.Property("name", "string")], nameSlot: "name");
        definition.Concept("Doc.Link", slots: [definition.Reference("to", "Doc.Item")]);
        definition.Concept("Doc.Spot", slots: [definition.Reference("to", "Doc.Item")], isAbstract: true);
        definition.Concept("Doc.Place", supers: ["Doc.Spot"]);
        definition.Enumeration("Doc.WeekDays", "Mon", "Thu");

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);
        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Doc.Item.mps"));
        ModelFile.Save(definition.Documents[1], Path.Combine(structure, "Doc.Link.mps"));
        ModelFile.Save(definition.Documents[2], Path.Combine(structure, "Doc.Spot.mps"));
        ModelFile.Save(definition.Documents[3], Path.Combine(structure, "Doc.Place.mps"));
        ModelFile.Save(definition.Documents[4], Path.Combine(structure, "Doc.WeekDays.mps"));
    }

    private static void WriteBehaviour(string directory, string conceptKey, string slot, string file = "link")
    {
        var model = BehaviourLanguage.NewModel("link");
        var document = model.Roots[0];

        document.Set(BehaviourConceptKeys.ConceptSlot, conceptKey);

        var function = model.NewChild(document, BehaviourConceptKeys.FunctionsSlot, BehaviourConceptKeys.Function);
        function.Set(BehaviourConceptKeys.KindSlot, BehaviourConceptKeys.NameKind);

        var body = model.NewChild(function, BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Reference);
        body.Set(BehaviourConceptKeys.SlotSlot, slot);

        var behaviour = Path.Combine(directory, LanguageProject.BehaviourFolder);

        Directory.CreateDirectory(behaviour);
        ModelFile.Save(model, Path.Combine(behaviour, file + ".mps"));
    }

    /// <summary>
    /// The same folder's other form: a C# behaviour class that names <paramref name="conceptKey"/> after what the node
    /// API says about the node itself - or, when an enumeration is named, after a symbol of it.
    /// </summary>
    /// <remarks>
    /// The body is <c>return node.concept;</c> - one read of the node API - because it is the one answer that cannot be
    /// mistaken for the declarative body's: the name of the node a reference reaches, against a fact about the node.
    /// With <paramref name="enumeration"/>, the body is <c>return enum/…/…;</c> instead, which is what the definition
    /// layer has to close against a `structure` enumeration.
    /// </remarks>
    private static void WriteCSharpBehaviour(
        string directory,
        string conceptKey,
        string file = "csharp",
        string? enumeration = null,
        string symbol = "Thu")
    {
        var model = new Project(CSharpLanguage.Concepts).NewModel("behaviour");
        var root = model.NewNode(CSharpConceptKeys.File);
        var space = model.NewChild(root, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var behaviour = model.NewChild(space, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Behaviour);
        var method = model.NewChild(behaviour, CSharpConceptKeys.MembersSlot, CSharpConceptKeys.Method);
        var returnType = model.NewChild(method, "returnType", CSharpConceptKeys.TypeReference);
        var parameter = model.NewChild(method, CSharpConceptKeys.ParametersSlot, CSharpConceptKeys.Parameter);
        var nodeType = model.NewChild(parameter, "type", CSharpConceptKeys.NodeType);
        var body = model.NewChild(method, CSharpConceptKeys.BodySlot, CSharpConceptKeys.Block);
        var @return = model.NewChild(body, CSharpConceptKeys.StatementsSlot, CSharpConceptKeys.Return);

        space.Set(CSharpLanguage.NameSlot, "Sample");
        behaviour.Set(CSharpConceptKeys.ConceptKeySlot, conceptKey);
        behaviour.Set(CSharpLanguage.NameSlot, conceptKey[(conceptKey.IndexOf('.') + 1)..] + "Behaviour");
        method.Set(CSharpLanguage.VisibilitySlot, CSharpVisibility.Public);
        method.Set(CSharpLanguage.IsStaticSlot, true);
        method.Set(CSharpLanguage.NameSlot, "Name");
        returnType.Set(CSharpLanguage.NameSlot, "string");
        nodeType.Set(CSharpLanguage.NameSlot, "Doc.Item");
        parameter.Set(CSharpLanguage.NameSlot, "node");

        if (enumeration is null)
        {
            var concept = model.NewChild(@return, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.NodeConcept);
            var operand = model.NewChild(concept, CSharpConceptKeys.OperandSlot, CSharpConceptKeys.Name);

            operand.SetReference(CSharpConceptKeys.TargetSlot, parameter);
        }
        else
        {
            var value = model.NewChild(@return, CSharpConceptKeys.ValueSlot, CSharpConceptKeys.EnumValue);

            value.Set(CSharpLanguage.EnumSlot, enumeration);
            value.Set(CSharpLanguage.MemberSlot, symbol);
        }

        model.AddRoot(root);

        var behaviourFolder = Path.Combine(directory, LanguageProject.BehaviourFolder);

        Directory.CreateDirectory(behaviourFolder);
        ModelFile.Save(model, Path.Combine(behaviourFolder, file + ".mps"));
    }
}
