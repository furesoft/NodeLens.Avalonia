namespace Projectional.Editing;

/// <summary>
/// What an edit action did, and where the caret should be afterwards.
/// </summary>
/// <remarks>
/// The caret travels with the answer rather than being read back from the model, because where the
/// caret belongs is part of what the action meant: typing puts it after the inserted text, deleting
/// pulls it back, and filling a slot puts it inside what was created. An action that had to compute
/// that from the model afterwards would need the layout again, and would have lost the intent by then.
/// </remarks>
public readonly record struct EditResult
{
    private EditResult(EditOutcome outcome, Selection selection, string? reason)
    {
        Outcome = outcome;
        Selection = selection;
        Reason = reason;
    }

    /// <summary>What happened.</summary>
    public EditOutcome Outcome { get; }

    /// <summary>Where the selection should be once the action has been applied.</summary>
    public Selection Selection { get; }

    /// <summary>Why the edit was refused, when it was.</summary>
    public string? Reason { get; }

    /// <summary>Whether the model changed and the tree has to be rebuilt.</summary>
    public bool Changed => Outcome == EditOutcome.Applied;

    /// <summary>The edit changed the model.</summary>
    public static EditResult Applied(Selection selection) => new(EditOutcome.Applied, selection, null);

    /// <summary>The edit had nothing to do.</summary>
    public static EditResult Unchanged(Selection selection) => new(EditOutcome.Unchanged, selection, null);

    /// <summary>The edit was refused because the result would be invalid.</summary>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty.</exception>
    public static EditResult Rejected(Selection selection, string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);

        return new EditResult(EditOutcome.Rejected, selection, reason);
    }

    public override string ToString() => Reason is null ? Outcome.ToString() : $"{Outcome}: {Reason}";
}
