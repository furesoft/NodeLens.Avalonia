namespace Projectional.Avalonia;

/// <summary>What a host decides about the shell before it opens, in one value.</summary>
/// <remarks>
/// <para>
/// ADR 0015 asks for the shell to be composable, and composition is a set of decisions a host makes once: which
/// modules this instance may offer, and which model it starts on. They travel together because they are answered at
/// the same moment - the constructor - and because a host that had to thread three arguments through would end up
/// keeping them in fields of its own, which is the shell's job.
/// </para>
/// <para>
/// The default is the standalone editor: every module, and no model open. A host that wants less says so by naming a
/// narrower <see cref="ShellCapability"/> set rather than by hiding entries afterwards, because hiding is presentation
/// and refusing is the guarantee.
/// </para>
/// <para>
/// This is the seam the storage and dialog interfaces will hang from, so a host that supplies its own document store
/// will hand it over in the same value rather than in a second constructor. Adding a member is therefore an additive
/// change a host can ignore, which is why the type exists now, with the two decisions that are settled.
/// </para>
/// </remarks>
public sealed record ShellOptions
{
    /// <summary>What this shell may offer; <see cref="ShellCapabilities.Standalone"/> unless a host narrows it.</summary>
    public ShellCapability Capabilities { get; init; } = ShellCapabilities.Standalone;

    /// <summary>
    /// The model to open first, as a path the shell's storage can read, or <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    /// A path rather than a loaded model, because loading belongs to the shell: it is the shell that decides what a
    /// document is, and a host that handed over a half-built project would be composing the shell's internals.
    /// </remarks>
    public string? InitialModel { get; init; }

    /// <summary>
    /// Where the shell keeps the little it persists; <see cref="FileShellStorage.Default"/> unless a host supplies one.
    /// </summary>
    public IShellStorage Storage { get; init; } = FileShellStorage.Default;
}
