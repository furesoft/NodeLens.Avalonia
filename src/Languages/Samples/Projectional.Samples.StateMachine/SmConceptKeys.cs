namespace Projectional.Samples.StateMachine;

/// <summary>The keys of the concepts <see cref="StateMachineLanguage"/> declares.</summary>
public static class SmConceptKeys
{
    /// <summary>A machine: its states, its transitions and the state it starts in.</summary>
    public const string Machine = "Sm.Machine";

    /// <summary>One state, with a name and whether it is a final one.</summary>
    public const string State = "Sm.State";

    /// <summary>One transition: a source, a target, an event and an optional guard.</summary>
    public const string Transition = "Sm.Transition";
}
