using Avalonia.Input;

namespace Projectional.Avalonia;

/// <summary>The one place a shell's capabilities are enforced, and the only thing its UI may execute through.</summary>
/// <remarks>
/// <para>
/// ADR 0015 names the defect this exists for: when a command is a window method, "this shell may not do that" has
/// no single place to be true, so the menu hides an entry, the shortcut still fires, and a host's own button
/// bypasses both. Here the capability lives on the command and is checked on the way in, so the three paths agree
/// by construction rather than by everyone remembering.
/// </para>
/// <para>
/// The registry refuses rather than throws. A refusal is an ordinary outcome - a host may well wire a menu from
/// the full command list and let the shell disable what it was not granted - and an exception would make the
/// honest answer the expensive one.
/// </para>
/// <para>
/// It is not a dispatcher and holds no UI: it knows names, labels and delegates. That is what lets the same
/// registry sit behind the standalone window and an embedded shell while the menu objects differ.
/// </para>
/// </remarks>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, ShellCommand> _commands = new(StringComparer.Ordinal);

    /// <summary>Creates a registry enforcing <paramref name="capabilities"/>.</summary>
    /// <param name="capabilities">The set this shell was declared with.</param>
    public CommandRegistry(ShellCapability capabilities) => Capabilities = capabilities;

    /// <summary>The capabilities this shell was declared with: the rule every command is checked against.</summary>
    public ShellCapability Capabilities { get; }

    /// <summary>Every registered command, in the order it was added - which is the order a menu may use.</summary>
    public IReadOnlyCollection<ShellCommand> Commands => _commands.Values;

    /// <summary>Registers <paramref name="command"/>.</summary>
    /// <param name="command">The command to add.</param>
    /// <exception cref="ArgumentException">Another command already has that name.</exception>
    /// <remarks>
    /// A duplicate name is rejected here rather than resolved by "last one wins", because the loser would be
    /// silently unreachable: the failure would show up as a menu entry that runs somebody else's code.
    /// </remarks>
    public void Add(ShellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_commands.TryAdd(command.Name, command))
        {
            throw new ArgumentException($"A command named '{command.Name}' is already registered.", nameof(command));
        }
    }

    /// <summary>The command called <paramref name="name"/>, or <see langword="null"/> if there is none.</summary>
    /// <param name="name">The identifier to look up.</param>
    public ShellCommand? Find(string name) => _commands.GetValueOrDefault(name);

    /// <summary>The command <paramref name="gesture"/> is bound to, or <see langword="null"/> if none is.</summary>
    /// <param name="gesture">The shortcut to resolve.</param>
    /// <remarks>
    /// A shortcut is looked up rather than compared at every key press, so the table in the menus and the handling
    /// in the key path cannot drift apart: both read this one answer.
    /// </remarks>
    public ShellCommand? FindByGesture(KeyGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);

        return _commands.Values.FirstOrDefault(command => gesture.Equals(command.Gesture));
    }

    /// <summary>Whether <paramref name="name"/> would run now, without running it.</summary>
    /// <param name="name">The identifier to ask about.</param>
    /// <remarks>An unknown name is not enabled: a menu built from a stale name should be grey, not a trap.</remarks>
    public bool CanExecute(string name) => Check(name).Executed;

    /// <summary>Why <paramref name="name"/> would not run, or <see langword="null"/> if it would.</summary>
    /// <param name="name">The identifier to ask about.</param>
    public string? BlockedReason(string name) => Check(name).Reason;

    /// <summary>Runs <paramref name="name"/>, or says why it did not.</summary>
    /// <param name="name">The identifier to run.</param>
    /// <param name="target">What the command acts on, for the commands that take a subject; otherwise ignored.</param>
    /// <returns>Whether it ran, and the reason when it did not.</returns>
    public ValueTask<CommandResult> ExecuteAsync(string name, object? target = null)
    {
        var permission = Check(name);

        return permission.Executed ? RunAsync(_commands[name], target) : ValueTask.FromResult(permission);
    }

    /// <summary>Whether the command may run, decided by its capability first and its own condition second.</summary>
    private CommandResult Check(string name)
    {
        if (!_commands.TryGetValue(name, out var command))
        {
            return CommandResult.Refused($"There is no command named '{name}'.");
        }

        if (!Capabilities.Allows(command.Capability))
        {
            return CommandResult.Refused($"'{command.Label}' is not available in this editor.");
        }

        if (command.CanExecute is { } canExecute && !canExecute())
        {
            return CommandResult.Refused(command.BlockedReason?.Invoke() ?? $"'{command.Label}' is not available right now.");
        }

        return CommandResult.Success;
    }

    private static async ValueTask<CommandResult> RunAsync(ShellCommand command, object? target)
    {
        await command.Execute(target);

        return CommandResult.Success;
    }
}
