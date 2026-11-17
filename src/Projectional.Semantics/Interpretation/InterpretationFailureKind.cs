namespace Projectional.Semantics;

/// <summary>Why an interpretation stopped without a value.</summary>
public enum InterpretationFailureKind
{
    /// <summary>A rule reported that its node cannot produce a value.</summary>
    Evaluation,

    /// <summary>The run used up its step budget.</summary>
    BudgetExceeded,

    /// <summary>The caller cancelled the run.</summary>
    Cancelled,
}
