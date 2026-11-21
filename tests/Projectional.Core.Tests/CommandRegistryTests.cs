using Avalonia.Input;

using Projectional.Avalonia;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The one place a capability is enforced, and the reason it is not the menu.</summary>
/// <remarks>
/// ADR 0015's negative consequence is that a command written as a window method bypasses the lock and still
/// compiles. The defence is that the registry refuses even when it is called directly, which is exactly what
/// these tests do: run the command by name, around every menu, and check that it did not run.
/// </remarks>
public sealed class CommandRegistryTests
{
    [Fact]
    public async Task ACapabilityTheShellWasNotGrantedRefusesTheCommandEvenWhenCalledByName()
    {
        var ran = false;
        var registry = Registry(ShellCapabilities.Embedded);

        registry.Add(Command("export", ShellCapability.Export, () => ran = true));

        var result = await registry.ExecuteAsync("export");

        // The menu never showed it and the call still refuses: hiding is presentation, this is the guarantee.
        Assert.False(result.Executed);
        Assert.False(ran);
        Assert.Equal("'Export' is not available in this editor.", result.Reason);
        Assert.False(registry.CanExecute("export"));
        Assert.Equal(result.Reason, registry.BlockedReason("export"));
    }

    [Fact]
    public async Task AGrantedCapabilityLetsTheCommandRun()
    {
        var ran = false;
        var registry = Registry(ShellCapabilities.Standalone);

        registry.Add(Command("export", ShellCapability.Export, () => ran = true));

        var result = await registry.ExecuteAsync("export");

        Assert.True(result.Executed);
        Assert.Null(result.Reason);
        Assert.True(ran);
    }

    [Fact]
    public async Task ACommandWhoseOwnConditionIsFalseIsRefusedAndExplainsItself()
    {
        var open = false;
        var registry = Registry(ShellCapabilities.Standalone);

        registry.Add(new ShellCommand
        {
            Name = "save",
            Label = "Save",
            Capability = ShellCapability.Save,
            CanExecute = () => open,
            BlockedReason = () => "There is no document to save.",
            Execute = _ => ValueTask.CompletedTask,
        });

        Assert.Equal("There is no document to save.", registry.BlockedReason("save"));

        open = true;

        // The same command, the same registry, a different moment: a capability is a fact about the shell, this is a
        // fact about now, and the two must not be folded into one check.
        Assert.True((await registry.ExecuteAsync("save")).Executed);
    }

    [Fact]
    public void ACommandWithNoConditionAndNoReasonIsRefusedWithASentenceOfItsOwn()
    {
        var registry = Registry(ShellCapabilities.Standalone);

        registry.Add(new ShellCommand
        {
            Name = "undo",
            Label = "Undo",
            CanExecute = () => false,
            Execute = _ => ValueTask.CompletedTask,
        });

        // A shell should not invent an explanation - but it must not answer with silence either.
        Assert.Equal("'Undo' is not available right now.", registry.BlockedReason("undo"));
    }

    [Fact]
    public async Task AnUnknownNameIsRefusedRatherThanThrowing()
    {
        var registry = Registry(ShellCapabilities.Standalone);

        var result = await registry.ExecuteAsync("nope");

        Assert.False(result.Executed);
        Assert.Equal("There is no command named 'nope'.", result.Reason);
        Assert.False(registry.CanExecute("nope"));
        Assert.Null(registry.Find("nope"));
    }

    [Fact]
    public void TheSameNameTwiceIsRejectedBecauseTheLoserWouldBeUnreachable()
    {
        var registry = Registry(ShellCapabilities.Standalone);

        registry.Add(Command("save", ShellCapability.Save, () => { }));

        var thrown = Assert.Throws<ArgumentException>(
            () => registry.Add(Command("save", ShellCapability.Save, () => { })));

        Assert.Contains("already registered", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AGestureIsResolvedThroughTheRegistrySoTheMenuAndTheKeyPathCannotDrift()
    {
        var gesture = new KeyGesture(Key.F5);
        var registry = Registry(ShellCapabilities.Standalone);

        registry.Add(new ShellCommand
        {
            Name = "run",
            Label = "Run",
            Capability = ShellCapability.Run,
            Gesture = gesture,
            Execute = _ => ValueTask.CompletedTask,
        });

        registry.Add(Command("save", ShellCapability.Save, () => { }));

        Assert.Equal("run", registry.FindByGesture(gesture)?.Name);
        Assert.Null(registry.FindByGesture(new KeyGesture(Key.F9)));
    }

    [Fact]
    public void AModifiedGestureIsDistinctFromTheBareKey()
    {
        var registry = Registry(ShellCapabilities.Standalone);
        var findUsages = new KeyGesture(Key.F7, KeyModifiers.Alt);

        registry.Add(new ShellCommand
        {
            Name = "find-usages",
            Label = "Find Usages",
            Capability = ShellCapability.FindUsages,
            Gesture = findUsages,
            Execute = _ => ValueTask.CompletedTask,
        });

        Assert.Equal("find-usages", registry.FindByGesture(findUsages)?.Name);

        // F7 alone is a different shortcut and must not resolve to the same command: the modifier is part of the
        // gesture, which is why the key path asks the registry instead of comparing key codes.
        Assert.Null(registry.FindByGesture(new KeyGesture(Key.F7)));
    }

    [Fact]
    public void TheCommandListKeepsTheOrderTheShellRegisteredItIn()
    {
        var registry = Registry(ShellCapabilities.Standalone);

        registry.Add(Command("new", ShellCapability.CreateModels, () => { }));
        registry.Add(Command("open", ShellCapability.OpenFiles, () => { }));
        registry.Add(Command("save", ShellCapability.Save, () => { }));

        // A menu built from this list reads top to bottom the way the shell declared it, so the order is part of the
        // contract rather than an accident of the dictionary.
        Assert.Equal(["new", "open", "save"], registry.Commands.Select(command => command.Name));
    }

    private static CommandRegistry Registry(ShellCapability capabilities) => new(capabilities);

    private static ShellCommand Command(string name, ShellCapability capability, Action execute) => new()
    {
        Name = name,
        Label = char.ToUpperInvariant(name[0]) + name[1..],
        Capability = capability,
        Execute = _ =>
        {
            execute();

            return ValueTask.CompletedTask;
        },
    };

    [Fact]
    public async Task ACommandThatTakesASubjectIsGivenTheOneTheMenuRowHad()
    {
        // The Export menu is one row per opened language, so the command is one and the subject comes from the row.
        // Passing it through is what keeps "export" behind the same gate as every parameterless command instead of
        // making the per-language rows a second enforcement point.
        var exported = new List<string>();
        var registry = Registry(ShellCapabilities.Standalone);

        registry.Add(new ShellCommand
        {
            Name = "export",
            Label = "Export",
            Capability = ShellCapability.Export,
            Execute = target =>
            {
                exported.Add((string)target!);

                return ValueTask.CompletedTask;
            },
        });

        Assert.True((await registry.ExecuteAsync("export", "Rules")).Executed);
        Assert.Equal(["Rules"], exported);

        // A shell that was not granted the capability refuses the subject too, which is the whole point of routing the
        // data-driven menu through here.
        var restricted = Registry(ShellCapabilities.Embedded);

        restricted.Add(new ShellCommand
        {
            Name = "export",
            Label = "Export",
            Capability = ShellCapability.Export,
            Execute = target =>
            {
                exported.Add((string)target!);

                return ValueTask.CompletedTask;
            },
        });

        Assert.False((await restricted.ExecuteAsync("export", "Other")).Executed);
        Assert.Equal(["Rules"], exported);
    }
}
