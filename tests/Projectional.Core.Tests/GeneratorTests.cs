using Projectional.Core;
using Projectional.Json;
using Projectional.Language;
using Projectional.Persistence;
using Projectional.Samples;
using Projectional.Samples.StateMachine;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The template walk: what each part emits, and how a generator says it cannot.</summary>
public sealed class GeneratorTests
{
    [Fact]
    public void APropertyEmitsTheValueTheModelHolds()
    {
        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, Property("name")), Sample());

        Assert.True(result.Succeeded);
        Assert.Equal("Payroll", result.Text);
    }

    [Fact]
    public void ALiteralIsEmittedAsWritten()
    {
        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, Literal("// payroll")), Sample());

        Assert.True(result.Succeeded);
        Assert.Equal("// payroll", result.Text);
    }

    [Fact]
    public void AChildIsExpandedWithItsOwnTemplate()
    {
        var templates = Registry()
            .Add(RulesConceptKeys.Ruleset, Child(RulesLanguage.RulesSlot))
            .Add(RulesConceptKeys.Rule, Property("name"));

        Assert.Equal("Approved", Generate(templates, Sample()).Text);
    }

    [Fact]
    public void ChildrenAreJoinedByTheSeparator()
    {
        var model = PayrollSample.NewSampleModel();
        var ruleset = model.Roots[0];
        var second = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        second.Set("name", "Rejected");

        var templates = Registry()
            .Add(RulesConceptKeys.Ruleset, TextTemplate.Of(
                new ChildrenPart(RulesLanguage.RulesSlot, Literal(" | "))))
            .Add(RulesConceptKeys.Rule, Property("name"));

        Assert.Equal("Approved | Rejected", Generate(templates, ruleset).Text);
    }

    [Fact]
    public void IndentationAppliesToEveryLineBreakInside()
    {
        var templates = Registry()
            .Add(RulesConceptKeys.Ruleset, TextTemplate.Of(
                new TextPart("a"),
                new IndentPart(TextTemplate.Of(LineBreakPart.Instance, new ChildPart(RulesLanguage.RulesSlot)))))
            .Add(RulesConceptKeys.Rule, Literal("b"));

        Assert.Equal("a\n  b", Generate(templates, Sample()).Text);
    }

    [Fact]
    public void TheIndentWidthBelongsToTheGenerator()
    {
        var templates = Registry().Add(RulesConceptKeys.Ruleset, TextTemplate.Of(
            new IndentPart(TextTemplate.Of(LineBreakPart.Instance, new TextPart("b")))));

        var result = new Generator(indentWidth: 4).Generate(Sample(), new TextGenerator("test", templates));

        Assert.Equal("\n    b", result.Text);
    }

    [Fact]
    public void AConditionalPicksTheTemplateThePropertySelects()
    {
        var templates = Registry()
            .Add(RulesConceptKeys.Ruleset, Child(RulesLanguage.RulesSlot))
            .Add(RulesConceptKeys.Rule, TextTemplate.Of(new ConditionalPart(
                "outcome",
                RuleOutcomes.Accept,
                Literal("accepts"),
                Literal("does not accept"))));

        Assert.Equal("accepts", Generate(templates, Sample()).Text);

        var model = PayrollSample.NewSampleModel();
        model.Roots[0].GetChildren(RulesLanguage.RulesSlot)[0].Set("outcome", RuleOutcomes.Reject);

        Assert.Equal("does not accept", Generate(templates, model.Roots[0]).Text);
    }

    [Fact]
    public void AFilledCheckPicksTheTemplateTheSlotFillednessSelects()
    {
        var model = PayrollSample.NewSampleModel();
        var ruleset = model.Roots[0];

        var templates = Registry().Add(RulesConceptKeys.Ruleset, TextTemplate.Of(new WhenFilledPart(
            RulesLanguage.RulesSlot,
            Literal("has rules"),
            Literal("has none"))));

        Assert.Equal("has rules", Generate(templates, ruleset).Text);

        var empty = PayrollSample.NewEmptyModel();

        Assert.Equal("has none", Generate(templates, empty.Roots[0]).Text);
    }

    [Fact]
    public void AFilledCheckAsksAReferenceWhetherItWasEverLinked()
    {
        // A linked and a dangling reference are both filled: only an unwritten slot is empty, which is exactly the
        // line a reference cell draws between "unset" and the other two states.
        var (_, linked) = Transition();
        var (_, unlinked) = Transition(linkTarget: false);

        var templates = Registry().Add(SmConceptKeys.Transition, TextTemplate.Of(new WhenFilledPart(
            "target",
            Literal("->"),
            Literal("-"))));

        Assert.Equal("->", Generate(templates, linked).Text);
        Assert.Equal("-", Generate(templates, unlinked).Text);
    }

    [Fact]
    public void AFilledCheckLaysOutATree()
    {
        // The reason the part exists: a template could not ask whether a node had children, so it could emit a step's
        // detail unconditionally but never nest one level per link. Recursion through the child's own template, with
        // the indent carried by the line break, is the whole of a tree layout.
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        rule.Set("name", "Rule");
        model.AddRoot(rule);

        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);
        step.Set("text", "one");

        var nested = model.NewChild(step, "detail", TestLanguage.Step);
        nested.Set("text", "two");

        model.NewChild(nested, "detail", TestLanguage.Step).Set("text", "three");

        var templates = Registry().Add(TestLanguage.Step, TextTemplate.Of(
            new PropertyPart("text"),
            new WhenFilledPart("detail", TextTemplate.Of(
                new IndentPart(TextTemplate.Of(LineBreakPart.Instance, new ChildPart("detail")))))));

        Assert.Equal("one\n  two\n    three", Generate(templates, step).Text);
    }

    [Fact]
    public void AFilledCheckOnASlotTheConceptHasNotFails()
    {
        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, TextTemplate.Of(
            new WhenFilledPart("nothing", Literal("x")))), Sample());

        Assert.False(result.Succeeded);
        Assert.Contains("not a slot", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ALoopAppliesItsBodyToEachChild()
    {
        var templates = Registry()
            .Add(RulesConceptKeys.Ruleset, TextTemplate.Of(new LoopPart(
                RulesLanguage.RulesSlot,
                TextTemplate.Of(new TextPart("<"), SelfPart.Instance, new TextPart(">")))))
            .Add(RulesConceptKeys.Rule, Property("name"));

        Assert.Equal("<Approved>", Generate(templates, Sample()).Text);
    }

    [Fact]
    public void AMissingTemplateFailsNamingTheConcept()
    {
        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, Child(RulesLanguage.RulesSlot)), Sample());

        Assert.False(result.Succeeded);
        Assert.Contains("Rules.Rule", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyChildSlotFailsNamingTheSlot()
    {
        var model = PayrollSample.NewEmptyModel();

        var result = Generate(
            Registry().Add(RulesConceptKeys.Ruleset, Child(RulesLanguage.RulesSlot)),
            model.Roots[0]);

        Assert.False(result.Succeeded);
        Assert.Contains(RulesLanguage.RulesSlot, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APropertyTheConceptDoesNotDeclareFails()
    {
        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, Property(RulesLanguage.RulesSlot)), Sample());

        Assert.False(result.Succeeded);
        Assert.Contains("not a property", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASelfExpansionStopsAtTheDepthLimit()
    {
        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, TextTemplate.Of(SelfPart.Instance)), Sample());

        Assert.False(result.Succeeded);
        Assert.Contains("expands itself", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APropertyTheModelDefaultedEmitsTheDefault()
    {
        // The model materialises a declared default, so a generator sees "unnamed" rather than nothing: a template
        // cannot tell a default from a value the user typed, and an engine that guessed would disagree with what the
        // document shows.
        var model = PayrollSample.NewEmptyModel();

        var result = Generate(Registry().Add(RulesConceptKeys.Ruleset, Property("name")), model.Roots[0]);

        Assert.True(result.Succeeded);
        Assert.Equal("unnamed", result.Text);
        Assert.Single(result.Regions);
    }

    [Fact]
    public void TwoRunsAgreeAndTheSecondStartsClean()
    {
        var generator = new Generator();

        var failing = generator.Generate(
            Sample(),
            new TextGenerator("test", Registry().Add(RulesConceptKeys.Ruleset, Child(RulesLanguage.RulesSlot))));

        var clean = generator.Generate(
            Sample(),
            new TextGenerator("other", Registry().Add(RulesConceptKeys.Ruleset, Property("name"))));

        // A run is independent of the one before it: the failure of the first leaks neither its text nor its state
        // into the second, which is what makes one generator usable for a session that generates many documents.
        Assert.False(failing.Succeeded);
        Assert.True(clean.Succeeded);
        Assert.Equal("Payroll", clean.Text);
    }

    [Fact]
    public void RegionsSayWhichNodeProducedWhichText()
    {
        var result = Generate(
            Registry().Add(RulesConceptKeys.Ruleset, TextTemplate.Of(
                new TextPart("// "),
                new PropertyPart("name"))),
            Sample());

        // Both stretches are recorded: the literal carries the node but no slot, the property carries both - a
        // literal is still text of that node, so "go to source" on a keyword must find the node that emitted it.
        Assert.Equal(2, result.Regions.Count);
        Assert.Null(result.Regions[0].Slot);
        Assert.Equal(0, result.Regions[0].Start);
        Assert.Equal("name", result.Regions[1].Slot);
        Assert.Equal(3, result.Regions[1].Start);
        Assert.Equal(result.Regions[1], result.RegionAt(5));
        Assert.Equal(result.Regions[0], result.RegionAt(0));
    }

    [Fact]
    public void AnEntryThatEmitsNothingTakesNoSeparatorWithIt()
    {
        // This is what makes "this concept is ignored" mean the same thing inside a list as outside one: the entry
        // writes nothing, so the separator that would have joined it to its neighbour must not be written either.
        var model = JsonLanguage.NewProject().NewModel("json");
        var array = model.NewNode(JsonConceptKeys.Array);
        model.NewChild(array, JsonLanguage.ItemsSlot, JsonConceptKeys.Null);
        var number = model.NewChild(array, JsonLanguage.ItemsSlot, JsonConceptKeys.Number);
        number.Set(JsonLanguage.TextSlot, "1");

        var templates = TemplateRegistry.Empty
            .Add(JsonConceptKeys.Array, TextTemplate.Of(
                new ChildrenPart(JsonLanguage.ItemsSlot, Literal(", "))))
            .Add(JsonConceptKeys.Number, Property(JsonLanguage.TextSlot))
            .Add(JsonConceptKeys.Null, TextTemplate.Empty);

        Assert.Equal("1", Generate(templates, array).Text);
    }

    [Fact]
    public void ALoopSkipsTheChildrenItsBodyCannotSpeakAbout()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        var array = model.NewNode(JsonConceptKeys.Array);
        model.NewChild(array, JsonLanguage.ItemsSlot, JsonConceptKeys.Null);
        var number = model.NewChild(array, JsonLanguage.ItemsSlot, JsonConceptKeys.Number);
        number.Set(JsonLanguage.TextSlot, "1");

        var templates = TemplateRegistry.Empty
            .Add(JsonConceptKeys.Array, TextTemplate.Of(
                new LoopPart(
                    JsonLanguage.ItemsSlot,
                    TextTemplate.Of(SelfPart.Instance),
                    Literal(", "))))
            .Add(JsonConceptKeys.Number, Property(JsonLanguage.TextSlot))
            .Add(JsonConceptKeys.Null, TextTemplate.Empty);

        Assert.Equal("1", Generate(templates, array).Text);
    }

    [Fact]
    public void ARegionInAListStillNamesWhereItWent()
    {
        // The entries are written into a buffer of their own, so a region has to learn where that buffer ended up -
        // otherwise "go to source" points at the wrong character.
        var model = JsonLanguage.NewProject().NewModel("json");
        var array = model.NewNode(JsonConceptKeys.Array);
        model.NewChild(array, JsonLanguage.ItemsSlot, JsonConceptKeys.Null);
        var number = model.NewChild(array, JsonLanguage.ItemsSlot, JsonConceptKeys.Number);
        number.Set(JsonLanguage.TextSlot, "12");

        var templates = TemplateRegistry.Empty
            .Add(JsonConceptKeys.Array, TextTemplate.Of(
                new ChildrenPart(JsonLanguage.ItemsSlot, Literal(", "))))
            .Add(JsonConceptKeys.Number, Property(JsonLanguage.TextSlot))
            .Add(JsonConceptKeys.Null, TextTemplate.Empty);

        var generated = Generate(templates, array);

        Assert.Equal("12", generated.Text);
        Assert.Equal(number.Id, generated.RegionAt(1)?.Node);
    }

    private static TemplateRegistry Registry() => TemplateRegistry.Empty;

    [Fact]
    public void AReferenceEmitsTheNameOfItsTarget()
    {
        var (model, transition) = Transition();
        var source = transition.GetReferenceTarget("source")!;

        var result = Generate(
            Registry().Add(SmConceptKeys.Transition, TextTemplate.Of(new ReferencePart("source"))),
            transition);

        Assert.True(result.Succeeded);
        Assert.Equal(source.GetOrNull("name"), result.Text);
    }

    [Fact]
    public void AnUnsetReferenceEmitsTheStandInThePartNames()
    {
        var (_, transition) = Transition(linkTarget: false);

        // A language states what "nothing linked" says, because it is a word of that language - SQL writes
        // nothing where a key is unset.
        var result = Generate(
            Registry().Add(SmConceptKeys.Transition, TextTemplate.Of(new ReferencePart("target", unsetText: ""))),
            transition);

        Assert.True(result.Succeeded);
        Assert.Equal("", result.Text);
    }

    [Fact]
    public void AReferenceWhoseTargetIsGoneEmitsTheUnresolvedText()
    {
        var (_, transition) = Transition();
        var document = ModelXml.Serialize(transition.Model);

        // Deleting would only clear the reference; a dangling one comes from a document whose target was lost, so
        // the state is built the way it actually arises. The id is read out of the written document rather than
        // formatted here, so the test makes no assumption about how a guid is spelled.
        const string Anchor = "role=\"target\" target=\"";
        var start = document.IndexOf(Anchor, StringComparison.Ordinal) + Anchor.Length;
        var end = document.IndexOf('"', start);

        // Spliced, not replaced: the same guid is also the target node's own `id`, and a global replace would
        // rename the node too - which leaves the reference pointing at it and proves nothing.
        var loaded = ModelXml.Deserialize(
            StateMachineLanguage.NewProject(),
            string.Concat(document[..start], Guid.NewGuid(), document[end..]));
        var orphan = loaded.Roots[0].GetChildren(StateMachineLanguage.TransitionsSlot)[0];

        // Loading keeps it (the model never drops data), and a generator still has to say something.
        Assert.Null(orphan.GetReferenceTarget("target"));

        var result = Generate(
            Registry().Add(SmConceptKeys.Transition, TextTemplate.Of(new ReferencePart("target", unresolvedText: "?"))),
            orphan);

        Assert.True(result.Succeeded);
        Assert.Equal("?", result.Text);
    }

    [Fact]
    public void AReferenceEmitsARegionThatNamesItsSlot()
    {
        var (_, transition) = Transition();
        var source = transition.GetReferenceTarget("source")!;

        var text = source.GetOrNull("name")!;
        var result = Generate(
            Registry().Add(SmConceptKeys.Transition, TextTemplate.Of(new ReferencePart("source"))),
            transition);

        var region = Assert.Single(result.Regions);

        Assert.Equal(transition.Id, region.Node);
        Assert.Equal("source", region.Slot);
        Assert.Equal(text, result.Text.Substring(region.Start, region.Length));
    }

    private static Node Sample() => PayrollSample.NewSampleModel().Roots[0];

    /// <summary>A transition from one idle state to a done state, with both ends linked unless asked otherwise.</summary>
    private static (Model Model, Node Transition) Transition(bool linkTarget = true)
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

        if (linkTarget)
        {
            transition.SetReference("target", done);
        }

        return (model, transition);
    }

    private static GeneratedText Generate(TemplateRegistry templates, Node node) =>
        new Generator().Generate(node, new TextGenerator("test", templates));

    private static TextTemplate Property(string slot) => TextTemplate.Of(new PropertyPart(slot));

    private static TextTemplate Child(string slot) => TextTemplate.Of(new ChildPart(slot));

    private static TextTemplate Literal(string text) => TextTemplate.Of(new TextPart(text));
}
