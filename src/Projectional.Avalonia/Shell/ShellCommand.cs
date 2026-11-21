using Avalonia.Input;

namespace Projectional.Avalonia;

/// <summary>One named thing a shell can do, and the facts that decide whether it may.</summary>
/// <remarks>
/// <para>
/// The unit ADR 0015 makes the shell's public action surface: a menu entry, a toolbar button, a keyboard
/// shortcut and a host's own button are four views of one of these instead of four paths into a window method.
/// The registry - not the call site - asks <see cref="Capability"/> and <see cref="CanExecute"/>, so a command
/// cannot be reached around its own refusal.
/// </para>
/// <para>
/// <see cref="Capability"/> and <see cref="CanExecute"/> answer different questions and must not be merged.
/// The capability is a fact about the shell that never changes after it was constructed; <see cref="CanExecute"/>
/// is a fact about the moment - no document open, nothing to undo, a run already in flight - and it changes as
/// the user works. A host that narrows the capability set is changing what the shell *is*; a document opening
/// or closing is changing what it momentarily *can*.
/// </para>
/// </remarks>
public sealed class ShellCommand
{
    /// <summary>The stable identifier a caller runs the command by; never shown to a user.</summary>
    /// <remarks>
    /// Stable across versions, because it is what a host's own menu items and its settings bind to. Two commands
    /// with one name is a defect the registry rejects at construction.
    /// </remarks>
    public required string Name { get; init; }

    /// <summary>The human-readable text a menu shows, without mnemonic or gesture.</summary>
    public required string Label { get; init; }

    /// <summary>The capability the shell must have been granted for this command to run at all.</summary>
    /// <remarks>
    /// <see cref="ShellCapability.None"/> means "no capability needed", which is the default: a shell-wide action
    /// such as Exit is not something a host narrows. A command that does carry one is refused by the registry even
    /// when a caller asks for it by name, which is the difference between hiding an entry and enforcing a rule.
    /// </remarks>
    public ShellCapability Capability { get; init; } = ShellCapability.None;

    /// <summary>The keyboard shortcut, or <see langword="null"/> for a command that has none.</summary>
    public KeyGesture? Gesture { get; init; }

    /// <summary>Whether the command applies right now; <see langword="null"/> means "whenever allowed".</summary>
    public Func<bool>? CanExecute { get; init; }

    /// <summary>Why the command does not apply right now, for a user who asked; <see langword="null"/> when it does.</summary>
    /// <remarks>
    /// Only consulted when <see cref="CanExecute"/> already said no, and it is allowed to be absent: a disabled
    /// menu entry is usually self-explanatory, and a shell should not invent an explanation it does not have.
    /// </remarks>
    public Func<string?>? BlockedReason { get; init; }

    /// <summary>What running the command does, given the target the caller had in mind.</summary>
    /// <remarks>
    /// <para>
    /// The target is what a data-driven menu supplies: the Export menu is one row per opened language, so "export"
    /// is one command whose subject the row already knows. A command that is about whatever is in front ignores it
    /// and receives <see langword="null"/>.
    /// </para>
    /// <para>
    /// It is a parameter here rather than a captured field on purpose. A closure over a field would make the
    /// command's meaning depend on what the last menu rebuild happened to assign, which is the "who ran me" bug
    /// that a registry exists to prevent.
    /// </para>
    /// </remarks>
    public required Func<object?, ValueTask> Execute { get; init; }
}
