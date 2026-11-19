using Projectional.Core;
using Projectional.Language;
using Projectional.Samples;
using Projectional.Samples.StateMachine;
using Projectional.Semantics;
using Projectional.TextGen;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The declared generator language: what a model of it lowers to, and what it refuses.</summary>
/// <remarks>
/// The parity test is the one that matters. ADR 0014 says the declared form expresses nothing the code form cannot, so
/// a generator written as a model must produce the text the hand-written twin produces - compared as text, because
/// that is what a consumer sees.
/// </remarks>
public sealed class TextGenLoweringTests
{
    [Fact]
    public void ADeclaredGeneratorEmitsWhatItsCSharpTwinEmits()
    {
        var rules = PayrollSample.NewSampleModel();
        var model = TextGenLanguage.NewModel();
        var template = Mapping(model, Generator(model, "Plain"), RulesConceptKeys.Ruleset);

        Text(model, template, "// ");
        Property(model, template, "name");

        var declared = TextGenLowering.CreateGenerators(model.Roots, rules.Concepts).Single();
        var twin = new TextGenerator("Plain", TemplateRegistry.Empty.Add(
            RulesConceptKeys.Ruleset,
            TextTemplate.Of(new TextPart("// "), new PropertyPart("name"))));

        Assert.Equal("Plain", declared.Name);
        Assert.Equal(
            new Generator().Generate(rules.Roots[0], twin).Text,
            new Generator().Generate(rules.Roots[0], declared).Text);
        Assert.Equal("// Payroll", new Generator().Generate(rules.Roots[0], declared).Text);
    }

    [Fact]
    public void ADeclaredFilledCheckEmitsWhatItsCSharpTwinEmits()
    {
        // The second conditional, declared: a slot the document fills and one it leaves empty must choose the same
        // branch as the hand-written part, or the declared path would be the weaker one (ADR 0014).
        var rules = PayrollSample.NewSampleModel();
        var model = TextGenLanguage.NewModel();
        var template = Mapping(model, Generator(model, "Filled"), RulesConceptKeys.Ruleset);

        var check = Part(model, template, TextGenConceptKeys.WhenFilled);

        check.Set(TextGenLanguage.ConceptKeySlot, RulesLanguage.RulesSlot);
        Text(model, Body(model, check), "has rules");
        Text(model, Otherwise(model, check), "has none");

        var declared = TextGenLowering.CreateGenerators(model.Roots, rules.Concepts).Single();
        var twin = new TextGenerator("Filled", TemplateRegistry.Empty.Add(
            RulesConceptKeys.Ruleset,
            TextTemplate.Of(new WhenFilledPart(
                RulesLanguage.RulesSlot,
                TextTemplate.Of(new TextPart("has rules")),
                TextTemplate.Of(new TextPart("has none"))))));

        var lowered = Assert.IsType<WhenFilledPart>(
            Assert.Single(declared.Templates.TemplateFor(rules.Concepts.Get(RulesConceptKeys.Ruleset))!.Parts));

        Assert.Equal(RulesLanguage.RulesSlot, lowered.Slot);
        Assert.Equal(
            new Generator().Generate(rules.Roots[0], twin).Text,
            new Generator().Generate(rules.Roots[0], declared).Text);
        Assert.Equal("has rules", new Generator().Generate(rules.Roots[0], declared).Text);
    }

    [Fact]
    public void AFilledCheckOnASlotTheConceptHasNotIsRefused()
    {
        var model = TextGenLanguage.NewModel();
        var check = Part(
            model,
            Mapping(model, Generator(model, "Json"), RulesConceptKeys.Ruleset),
            TextGenConceptKeys.WhenFilled);

        check.Set(TextGenLanguage.ConceptKeySlot, "nothing");

        var error = Assert.Throws<ConceptDefinitionException>(() => Lower(model));

        Assert.Contains("'nothing' is not a slot", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ALanguageCanDeclareSeveralGenerators()
    {
        // One generator per document: a language's generator aspect is a folder, and the names are what a host
        // picks between - so several generators are several files, not a list in one.
        var json = TextGenLanguage.NewModel("Json");
        Text(json, Mapping(json, Generator(json, "Json"), RulesConceptKeys.Ruleset), "json");

        var plain = TextGenLanguage.NewModel("Plain");
        Text(plain, Mapping(plain, Generator(plain, "Plain"), RulesConceptKeys.Ruleset), "plain");

        var generators = Lower(json, plain);

        Assert.Equal(["Json", "Plain"], generators.Select(generator => generator.Name));
    }

    [Fact]
    public void TwoGeneratorsWithOneNameAreRefused()
    {
        var first = TextGenLanguage.NewModel();
        var second = TextGenLanguage.NewModel();

        Generator(first, "Json");
        Generator(second, "Json");

        var error = Assert.Throws<ConceptDefinitionException>(() => Lower(first, second));

        Assert.Contains("Two generators", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AGeneratorWithoutANameIsRefused()
    {
        // A new generator document holds one unnamed generator, which is exactly the state the lowering refuses.
        var error = Assert.Throws<ConceptDefinitionException>(() => Lower(TextGenLanguage.NewModel()));

        Assert.Contains("name", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AMappingOfAConceptTheLanguageDoesNotHaveIsRefused()
    {
        var model = TextGenLanguage.NewModel();

        Text(model, Mapping(model, Generator(model, "Json"), "Rules.Nothing"), "x");

        var error = Assert.Throws<ConceptDefinitionException>(() => Lower(model));

        Assert.Contains("Rules.Nothing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APropertyTheConceptDoesNotHaveIsRefused()
    {
        var model = TextGenLanguage.NewModel();

        Property(model, Mapping(model, Generator(model, "Json"), RulesConceptKeys.Ruleset), "nothing");

        var error = Assert.Throws<ConceptDefinitionException>(() => Lower(model));

        Assert.Contains("not a property", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AListSlotCannotBeExpandedAsOneChild()
    {
        var model = TextGenLanguage.NewModel();
        var template = Mapping(model, Generator(model, "Json"), RulesConceptKeys.Ruleset);
        var part = Part(model, template, TextGenConceptKeys.Child);

        part.Set(TextGenLanguage.ConceptKeySlot, RulesLanguage.RulesSlot);

        var error = Assert.Throws<ConceptDefinitionException>(() => Lower(model));

        Assert.Contains("list", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPartKindLowersToTheRuntimePartItStandsFor()
    {
        var model = TextGenLanguage.NewModel();
        var generator = Generator(model, "Json");

        var ruleset = Mapping(model, generator, RulesConceptKeys.Ruleset);
        var indent = Part(model, ruleset, TextGenConceptKeys.Indent);
        var list = Part(model, Body(model, indent), TextGenConceptKeys.Children);

        list.Set(TextGenLanguage.ConceptKeySlot, RulesLanguage.RulesSlot);
        Text(model, Separator(model, list), ",");

        var rule = Mapping(model, generator, RulesConceptKeys.Rule);
        Part(model, rule, TextGenConceptKeys.Line);
        var conditional = Part(model, rule, TextGenConceptKeys.Conditional);

        conditional.Set(TextGenLanguage.ConceptKeySlot, "outcome");
        conditional.Set(TextGenLanguage.ValueSlot, RuleOutcomes.Accept);

        Text(model, Body(model, conditional), "accept");
        Text(model, Otherwise(model, conditional), "other");

        var concepts = PayrollSample.NewSampleModel().Concepts;
        var lowered = TextGenLowering.CreateGenerators(model.Roots, concepts).Single();

        Assert.True(concepts.TryGet(RulesConceptKeys.Ruleset, out var rulesetConcept));

        var template = lowered.Templates.TemplateFor(rulesetConcept!)!;
        var inner = Assert.IsType<IndentPart>(Assert.Single(template.Parts));
        var children = Assert.IsType<ChildrenPart>(Assert.Single(inner.Body.Parts));

        Assert.Equal(RulesLanguage.RulesSlot, children.Slot);
        Assert.Equal(",", Assert.IsType<TextPart>(Assert.Single(children.Separator.Parts)).Text);
    }

    private static IReadOnlyList<TextGenerator> Lower(params Model[] models) =>
        TextGenLowering.CreateGenerators(
            models.SelectMany(model => model.Roots),
            PayrollSample.NewSampleModel().Concepts);

    /// <summary>Names the one generator a fresh generator document holds.</summary>
    private static Node Generator(Model model, string name)
    {
        var generator = model.Roots[0];

        generator.Set(TextGenLanguage.NameSlot, name);

        return generator;
    }

    private static Node Mapping(Model model, Node generator, string conceptKey)
    {
        var mapping = model.NewChild(generator, TextGenLanguage.MappingsSlot, TextGenConceptKeys.Mapping);

        mapping.Set(TextGenLanguage.ConceptKeySlot, conceptKey);

        return model.NewChild(mapping, TextGenLanguage.TemplateSlot, TextGenConceptKeys.Template);
    }

    private static Node Part(Model model, Node template, string kind) =>
        model.NewChild(template, TextGenLanguage.PartsSlot, kind);

    private static Node Text(Model model, Node template, string text)
    {
        var part = Part(model, template, TextGenConceptKeys.Text);

        part.Set(TextGenLanguage.TextSlot, text);

        return part;
    }

    private static Node Property(Model model, Node template, string slot)
    {
        var part = Part(model, template, TextGenConceptKeys.Property);

        part.Set(TextGenLanguage.ConceptKeySlot, slot);

        return part;
    }

    private static Node Body(Model model, Node part) =>
        model.NewChild(part, TextGenLanguage.BodySlot, TextGenConceptKeys.Template);

    private static Node Separator(Model model, Node part) =>
        model.NewChild(part, TextGenLanguage.SeparatorSlot, TextGenConceptKeys.Template);

    private static Node Otherwise(Model model, Node part) =>
        model.NewChild(part, TextGenLanguage.OtherwiseSlot, TextGenConceptKeys.Template);

    /// <summary>
    /// A reference part, lowered against a language that has one - the pilot's rules language has none, and a part
    /// can only be declared over a slot the language actually owns.
    /// </summary>
    [Fact]
    public void AReferencePartLowersToTheNameOfItsTarget()
    {
        var (_, transition) = Transition();

        var generator = LowerAgainst(
            StateMachineLanguage.Concepts,
            SmConceptKeys.Transition,
            part => part.Set(TextGenLanguage.ConceptKeySlot, "source")).Single();

        var generated = new Generator().Generate(transition, generator);

        Assert.True(generated.Succeeded, generated.Failure?.Message);
        Assert.Equal("Idle", generated.Text);
    }

    [Fact]
    public void AReferencePartThatNamesAPropertyIsRefusedWhenTheGeneratorLoads()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => LowerAgainst(
            StateMachineLanguage.Concepts,
            SmConceptKeys.Transition,
            part => part.Set(TextGenLanguage.ConceptKeySlot, "event")));

        // Naming the concept and the slot: the author has to be able to find the mistake in the file.
        Assert.Contains("'event' is not a reference of Sm.Transition", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Lowers one generator whose only mapping is <paramref name="conceptKey"/>, with one part in it.</summary>
    private static IReadOnlyList<TextGenerator> LowerAgainst(
        ConceptRegistry concepts,
        string conceptKey,
        Action<Node> configure)
    {
        var model = new Project(TextGenLanguage.Concepts).NewModel("generator");
        var generator = model.NewNode(TextGenConceptKeys.Generator);
        generator.Set(TextGenLanguage.NameSlot, "Test");
        model.AddRoot(generator);

        var template = Mapping(model, generator, conceptKey);
        configure(Part(model, template, TextGenConceptKeys.Reference));

        return TextGenLowering.CreateGenerators(model.Roots, concepts);
    }

    /// <summary>A machine with one transition from `Idle` to `Done`, and that transition.</summary>
    private static (Model Model, Node Transition) Transition()
    {
        var model = StateMachineLanguage.NewProject().NewModel("Machine");
        var machine = model.NewNode(SmConceptKeys.Machine);
        machine.Set("name", "Machine");
        model.AddRoot(machine);

        var idle = model.NewChild(machine, StateMachineLanguage.StatesSlot, SmConceptKeys.State);
        idle.Set("name", "Idle");

        var done = model.NewChild(machine, StateMachineLanguage.StatesSlot, SmConceptKeys.State);
        done.Set("name", "Done");

        var transition = model.NewChild(machine, StateMachineLanguage.TransitionsSlot, SmConceptKeys.Transition);
        transition.Set("event", "go");
        transition.SetReference("source", idle);
        transition.SetReference("target", done);

        return (model, transition);
    }
}
