namespace Projectional.Editing;

/// <summary>Whether an intention applies at the caret, cannot run there, or is not about this place at all.</summary>
/// <remarks>
/// <para>
/// Three states, where <see cref="FixAvailability"/> has two, and the difference is not an oversight. A fix
/// is <em>built per finding</em> by the check that found the problem, so a repair that does not apply is
/// simply not built. An intention is declared once - by a language beside its concepts, or by a host as a
/// command - and only afterwards asked "does it apply here?", so it needs an answer for "this is not my
/// caret" that is not the same as "I would apply, but not right now".
/// </para>
/// <para>
/// The distinction is what a surface can act on. <see cref="IsAbsent"/> entries are dropped from the list, so
/// they cost the user nothing. <see cref="Disabled"/> entries are kept and shown greyed with their
/// <see cref="Reason"/>, because "this concept knows about this caret and here is why it cannot act" is the
/// useful half of the message - the same argument the fix catalogue makes for keeping a disabled repair.
/// </para>
/// <para>
/// <see cref="Applies"/> is the default value, so an intention that says nothing is offered rather than
/// silently hidden - and a caller may pass <c>default</c> and mean it.
/// </para>
/// </remarks>
public readonly record struct IntentionAvailability
{
    private readonly bool _absent;
    private readonly string? _reason;

    private IntentionAvailability(bool absent, string? reason)
    {
        _absent = absent;
        _reason = reason;
    }

    /// <summary>The intention applies at this caret and can run.</summary>
    public static IntentionAvailability Applies => default;

    /// <summary>The intention is not about this caret, so it is not offered.</summary>
    /// <remarks>
    /// The state a <em>declared</em> intention answers with when the caret stands somewhere it has nothing to
    /// say about. A provider that has nothing to offer at a caret should answer an empty list instead: an
    /// <see cref="Absent"/> entry is how one declared intention declines, which is what lets a language's
    /// aspect hand back everything it declares and let the list filter it.
    /// </remarks>
    public static IntentionAvailability Absent => new(absent: true, reason: null);

    /// <summary>The intention is about this caret but cannot run, and <paramref name="reason"/> says why.</summary>
    /// <param name="reason">A sentence for the user, not for the log.</param>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty.</exception>
    public static IntentionAvailability Disabled(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);

        return new IntentionAvailability(absent: false, reason);
    }

    /// <summary>Whether the intention is not about this caret at all.</summary>
    public bool IsAbsent => _absent;

    /// <summary>Whether the intention is offered but cannot be applied.</summary>
    /// <remarks>
    /// The disabled case is the one a caller checks, so it is asked about directly rather than by comparing
    /// reasons: a disabled intention always has one, and an applicable one never does. An absent intention
    /// is neither disabled nor applicable, which is why <see cref="IsApplicable"/> exists as a third answer.
    /// </remarks>
    public bool IsDisabled => _reason is not null;

    /// <summary>Why the intention cannot run, or <see langword="null"/> when it can or is absent.</summary>
    public string? Reason => _reason;

    /// <summary>Whether the intention is offered for this caret and able to run there.</summary>
    public bool IsApplicable => !_absent && _reason is null;

    public override string ToString() => _absent ? "absent" : _reason is null ? "applies" : $"disabled: {_reason}";
}
