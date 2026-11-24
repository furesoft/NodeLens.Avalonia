using Projectional.Core;
using Projectional.Expressions;
using Projectional.Samples.StateMachine;

namespace Projectional.Web;

/// <summary>The state-machine documents the browser editor opens, built through the public model API.</summary>
/// <remarks>
/// The desktop shell opens this language only from a declared project folder, and a browser cannot read one - so
/// the browser sample builds the same shape here. That is the honest reason this file exists: the sample needs a
/// document, not a language, and building it from the public API is what a host that embeds the editor would do.
/// </remarks>
internal static class StateMachineDocuments
{
    /// <summary>A machine with one state and nothing else: the empty shape a new tab starts with.</summary>
    internal static Model NewEmptyModel()
    {
        var model = StateMachineLanguage.NewProject().NewModel("machine");
        model.AddRoot(model.NewNode(SmConceptKeys.Machine));

        return model;
    }

    /// <summary>
    /// A machine with a loop, a guard and one state nothing reaches, so the problems panel has something to say.
    /// </summary>
    internal static Model NewSampleModel()
    {
        var model = StateMachineLanguage.NewProject().NewModel("release");
        var machine = model.NewNode(SmConceptKeys.Machine);
        machine.Set("name", "Release");
        model.AddRoot(machine);

        var idle = State(model, machine, "Idle");
        var building = State(model, machine, "Building");
        var broken = State(model, machine, "Broken");
        var shipped = State(model, machine, "Shipped", isFinal: true);

        machine.SetReference(StateMachineLanguage.InitialSlot, idle);

        Transition(model, machine, "start", idle, building, (">", 0));
        Transition(model, machine, "retry", building, building, ("<", 3));
        Transition(model, machine, "ship", building, shipped);
        Transition(model, machine, "rescind", broken, idle);

        return model;
    }

    private static Node State(Model model, Node machine, string name, bool isFinal = false)
    {
        var state = model.NewChild(machine, StateMachineLanguage.StatesSlot, SmConceptKeys.State);
        state.Set("name", name);
        state.Set("isFinal", isFinal);

        return state;
    }

    private static void Transition(
        Model model,
        Node machine,
        string @event,
        Node source,
        Node target,
        (string Operator, int Value)? guard = null)
    {
        var transition = model.NewChild(machine, StateMachineLanguage.TransitionsSlot, SmConceptKeys.Transition);
        transition.Set("event", @event);
        transition.SetReference("source", source);
        transition.SetReference("target", target);

        if (guard is not { } comparison)
        {
            return;
        }

        // A guard is the shared expression language's binary, which is why this language needs no vocabulary of
        // its own for conditions: `attempts <op> <value>` is an expression like any other.
        var condition = model.NewChild(transition, "guard", ExpressionsConceptKeys.Binary);
        condition.Set("operator", comparison.Operator);
        model.NewChild(condition, "left", ExpressionsConceptKeys.Name).Set("name", "attempts");
        model.NewChild(condition, "right", ExpressionsConceptKeys.IntLiteral).Set("value", comparison.Value);
    }
}
