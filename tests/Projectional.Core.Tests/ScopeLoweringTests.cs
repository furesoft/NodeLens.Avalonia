using Projectional.Core;
using Projectional.Samples.StateMachine;
using Projectional.ScopeRules;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Turning written scope rules into the providers a session installs.
/// </summary>
/// <remarks>
/// The fixture is the state machine because it declares real references (<c>StateMachine.Transition.source</c> and
/// <c>target</c>), so a rule is checked against the same registry a host would hand the lowering.
/// </remarks>
public sealed class ScopeLoweringTests
{
    [Fact]
    public void ARuleBecomesAProviderThatAnswersItsSlot()
    {
        var model = ScopeLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(ScopeLanguage.ConceptKeySlot, SmConceptKeys.Transition);
        rule.Set(ScopeLanguage.SlotSlot, "source");
        rule.Set(ScopeLanguage.PlaceSlot, ScopePlaces.Model);
        rule.Set(ScopeLanguage.TargetSlot, SmConceptKeys.State);

        var providers = ScopeLowering.CreateProviders(model.Roots, StateMachineLanguage.Concepts);

        Assert.Equal(1, providers.Count);

        var provider = providers.ProviderFor(StateMachineLanguage.Concepts.Get(SmConceptKeys.Transition));

        Assert.NotNull(provider);
        Assert.Equal(
            new ScopeBinding(ScopePlace.Model, SmConceptKeys.State),
            ((NamedScopeProvider)provider).Slots["source"]);
    }

    [Fact]
    public void TwoReferencesOfOneConceptShareOneProvider()
    {
        var first = ScopeLanguage.NewModel("source");
        var second = ScopeLanguage.NewModel("target");

        Bind(first.Roots[0], "source", ScopePlaces.Model);
        Bind(second.Roots[0], "target", ScopePlaces.Local);

        var providers = ScopeLowering.CreateProviders([first.Roots[0], second.Roots[0]], StateMachineLanguage.Concepts);

        var provider = (NamedScopeProvider)providers.ProviderFor(StateMachineLanguage.Concepts.Get(SmConceptKeys.Transition))!;

        Assert.Equal(2, provider.Slots.Count);
    }

    [Fact]
    public void AConceptThatIsNotDeclaredIsRefused()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(
            () => Lower(conceptKey: "Sm.Nothing", slot: "source"));

        Assert.Contains("Sm.Nothing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASlotThatIsNotAReferenceIsRefused()
    {
        // `name` is a property; a scope rule names a reference, because only a reference has a scope.
        var exception = Assert.Throws<ConceptDefinitionException>(
            () => Lower(conceptKey: SmConceptKeys.State, slot: "name"));

        Assert.Contains("reference slot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownTargetIsRefused()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(
            () => Lower(conceptKey: SmConceptKeys.Transition, slot: "source", target: "Sm.Nothing"));

        Assert.Contains("Sm.Nothing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoRulesForOneSlotAreRefusedRatherThanMerged()
    {
        var first = ScopeLanguage.NewModel("first");
        var second = ScopeLanguage.NewModel("second");

        Bind(first.Roots[0], "source", ScopePlaces.Model);
        Bind(second.Roots[0], "source", ScopePlaces.Project);

        var exception = Assert.Throws<ConceptDefinitionException>(
            () => ScopeLowering.CreateProviders([first.Roots[0], second.Roots[0]], StateMachineLanguage.Concepts));

        Assert.Contains("one scope", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentThatIsNotAScopeRuleIsRefused()
    {
        var model = StateMachineLanguage.NewProject().NewModel("machine");
        var machine = model.NewNode(SmConceptKeys.Machine);

        Assert.Throws<ConceptDefinitionException>(
            () => ScopeLowering.CreateProviders([machine], StateMachineLanguage.Concepts));
    }

    [Fact]
    public void AnAbsentPlaceIsRefusedAndTheModelRefusesOneTheLanguageDoesNotHave()
    {
        // The place is an enumerated property, so a value outside the five symbols never reaches the lowering - the
        // model refuses it at the write. The lowering therefore has to refuse only "no place at all", which is a
        // rule whose property was never given a value.
        var model = ScopeLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(ScopeLanguage.ConceptKeySlot, SmConceptKeys.Transition);
        rule.Set(ScopeLanguage.SlotSlot, "source");

        // The property has a default, so "unset" cannot be expressed; the only empty place is a cleared one, and the
        // model refuses that. This pins the rule that the lowering never has to guess a place.
        Assert.Throws<ModelInvariantException>(() => rule.Set(ScopeLanguage.PlaceSlot, string.Empty));
    }

    private static ScopeProviderRegistry Lower(
        string conceptKey,
        string slot,
        string? target = null,
        string place = ScopePlaces.Everywhere)
    {
        var model = ScopeLanguage.NewModel();

        Bind(model.Roots[0], slot, place, conceptKey, target);

        return ScopeLowering.CreateProviders(model.Roots, StateMachineLanguage.Concepts);
    }

    private static void Bind(Node rule, string slot, string place, string conceptKey = SmConceptKeys.Transition, string? target = null)
    {
        rule.Set(ScopeLanguage.ConceptKeySlot, conceptKey);
        rule.Set(ScopeLanguage.SlotSlot, slot);
        rule.Set(ScopeLanguage.PlaceSlot, place);

        if (target is not null)
        {
            rule.Set(ScopeLanguage.TargetSlot, target);
        }
    }
}
