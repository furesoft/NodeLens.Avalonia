namespace Projectional.Editing;

/// <summary>Whether a fix can run right now, and why not when it cannot.</summary>
/// <remarks>
/// <para>
/// Two states, not three. An intention lives in a registry and is asked "do you apply <em>here</em>?"
/// after it already exists, so it needs an <c>Absent</c> answer on top of yes and no. A fix does not:
/// it is built per finding by the check that found the problem, so a repair that does not apply is
/// simply not built. What is left is the distinction that carries information - a fix that applies, and
/// a fix that is listed but cannot run yet, with the reason the user needs to understand why.
/// </para>
/// <para>
/// A disabled fix is kept rather than dropped because "this language knows about this problem and here
/// is why it cannot act" is the useful half of the message. When fixes later feed the general intention
/// list, they map to <see cref="Applies"/> or <see cref="Disabled"/> and never to an absent state, so
/// the two vocabularies compose without a translation layer.
/// </para>
/// </remarks>
public readonly record struct FixAvailability
{
    private FixAvailability(string reason) => Reason = reason;

    /// <summary>The fix can be applied.</summary>
    /// <remarks>
    /// The default value of the struct, so "no reason given" and "can be applied" cannot disagree - which
    /// is also why a caller may pass <c>default</c> for a fix's availability and mean it.
    /// </remarks>
    public static FixAvailability Applies => default;

    /// <summary>
    /// The fix is offered but cannot run, and <paramref name="reason"/> says why.
    /// </summary>
    /// <param name="reason">A sentence for the user, not for the log.</param>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty.</exception>
    public static FixAvailability Disabled(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);

        return new FixAvailability(reason);
    }

    /// <summary>Why the fix cannot run, or <see langword="null"/> when it can.</summary>
    public string? Reason { get; }

    /// <summary>Whether the fix is offered but cannot be applied.</summary>
    /// <remarks>
    /// The disabled case is the one a caller has to check, so it is asked about directly rather than by
    /// comparing reasons: a disabled fix always has one, and an applicable fix never does.
    /// </remarks>
    public bool IsDisabled => Reason is not null;

    public override string ToString() => Reason is null ? "applies" : $"disabled: {Reason}";
}
