namespace Projectional.Workbench;

/// <summary>What one shell instance is allowed to offer: the modules and actions it composes.</summary>
/// <remarks>
/// <para>
/// A flag set rather than a pile of booleans, because the question a shell asks is "may I offer this" and the
/// answer has to be one value that is handed over once. The standalone app ships with
/// <see cref="ShellCapability.All"/>; an embedded shell is given a narrower set by its host
/// (<c>docs/architecture/embedded-shell.md</c>, ADR 0015).
/// </para>
/// <para>
/// A module that is not granted is not composed at all where it is a panel or a menu, and the command it would
/// run refuses as well. Hiding an entry is presentation; refusing is the guarantee - the blind spot ADR 0015
/// names as "a capability check that exists only in the UI is a bug".
/// </para>
/// </remarks>
[Flags]
internal enum ShellCapability
{
    /// <summary>Nothing: the shell is a place to look at a document somebody else opened.</summary>
    None = 0,

    /// <summary>Open a saved model or a language folder.</summary>
    OpenFiles = 1 << 0,

    /// <summary>Create a document, including an empty one from the menu or the strip's "+".</summary>
    CreateModels = 1 << 1,

    /// <summary>Edit the model with the editor.</summary>
    EditModels = 1 << 2,

    /// <summary>Write a document back to disk.</summary>
    Save = 1 << 3,

    /// <summary>Read a foreign file through an importer.</summary>
    Import = 1 << 4,

    /// <summary>Write a language folder as C#.</summary>
    Export = 1 << 5,

    /// <summary>The find bar and its replace row.</summary>
    Find = 1 << 6,

    /// <summary>Find usages, and the panel that lists them.</summary>
    FindUsages = 1 << 7,

    /// <summary>Run a document, and the output tab that shows the answer.</summary>
    Run = 1 << 8,

    /// <summary>Run a language's tests.</summary>
    Tests = 1 << 9,

    /// <summary>The project explorer: what is open, and what the opened languages are made of.</summary>
    ProjectExplorer = 1 << 10,

    /// <summary>The outline of the document in front.</summary>
    Outline = 1 << 11,

    /// <summary>The problems tab, and the count in the status bar while it is closed.</summary>
    Problems = 1 << 12,

    /// <summary>The terminal tool: a program of the user's choice, its output, and keys typed back into it.</summary>
    Terminal = 1 << 13,

    /// <summary>Everything the standalone editor offers.</summary>
    All = OpenFiles | CreateModels | EditModels | Save | Import | Export | Find | FindUsages | Run | Tests
        | ProjectExplorer | Outline | Problems | Terminal,
}

/// <summary>The two sets the shell ships with, and the check both are read through.</summary>
/// <remarks>
/// <see cref="Standalone"/> is the app; <see cref="Embedded"/> is the host that wants an editor in its own
/// window - create, edit and save a model, with the outline and the problems to orient by, and none of the
/// language-workbench modules. A host is free to name any other subset; these are the two that must not drift.
/// </remarks>
internal static class ShellCapabilities
{
    /// <summary>The standalone editor: every module.</summary>
    internal const ShellCapability Standalone = ShellCapability.All;

    /// <summary>An editor a host embeds: models can be made, edited and saved, and nothing else is offered.</summary>
    internal const ShellCapability Embedded =
        ShellCapability.CreateModels | ShellCapability.EditModels | ShellCapability.Save
        | ShellCapability.OpenFiles | ShellCapability.Outline | ShellCapability.Problems;

    /// <summary>Whether <paramref name="granted"/> contains every flag of <paramref name="capability"/>.</summary>
    /// <param name="granted">The set the shell was declared with.</param>
    /// <param name="capability">The module or action being asked about.</param>
    /// <remarks>
    /// Every flag, not any flag: a composite request such as <c>OpenFiles | Save</c> must be answered by a set that
    /// grants both, which is what makes a caller able to ask one question for one command.
    /// </remarks>
    internal static bool Allows(this ShellCapability granted, ShellCapability capability) =>
        (granted & capability) == capability;
}
