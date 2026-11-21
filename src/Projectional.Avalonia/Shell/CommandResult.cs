namespace Projectional.Avalonia;

/// <summary>What happened when a shell was asked to run a command.</summary>
/// <remarks>
/// A result rather than a silent return, because every refusal has to be sayable. ADR 0015 makes the registry
/// the one place a capability is enforced, and an answer nobody can read is not an enforcement: a keyboard
/// path, a host-built menu item and the shell's own toolbar all get the same sentence back, which is what keeps
/// them from disagreeing about why a command did not run.
/// </remarks>
/// <param name="Executed">Whether the command ran.</param>
/// <param name="Reason">
/// Why it did not, in a sentence a user could read; <see langword="null"/> when it ran. Never
/// <see cref="string.Empty"/>: a refusal always has a cause, and "unknown command" is one too.
/// </param>
public sealed record CommandResult(bool Executed, string? Reason)
{
    /// <summary>The command ran.</summary>
    public static CommandResult Success { get; } = new(true, null);

    /// <summary>The command did not run, and this is why.</summary>
    /// <param name="reason">A sentence naming the cause.</param>
    public static CommandResult Refused(string reason) => new(false, reason);
}
