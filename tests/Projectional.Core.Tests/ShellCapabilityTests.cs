using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A capability set is the one statement of what a shell instance may offer, so the two presets have to be
/// exactly what they claim - an embedded host that finds a module it did not ask for is a host whose editor
/// shows something its own UI knows nothing about.
/// </summary>
public sealed class ShellCapabilityTests
{
    [Fact]
    public void TheStandaloneSetGrantsEveryModule()
    {
        foreach (var capability in Enum.GetValues<ShellCapability>())
        {
            if (capability is ShellCapability.None or ShellCapability.All)
            {
                continue;
            }

            Assert.True(ShellCapabilities.Standalone.Allows(capability), $"{capability} should be granted");
        }
    }

    [Fact]
    public void TheEmbeddedSetIsAnEditorAndNothingElse()
    {
        Assert.True(ShellCapabilities.Embedded.Allows(ShellCapability.EditModels));
        Assert.True(ShellCapabilities.Embedded.Allows(ShellCapability.Save));

        // The two modules a host asked to switch off, named as such: the run window and the project explorer.
        Assert.False(ShellCapabilities.Embedded.Allows(ShellCapability.Run));
        Assert.False(ShellCapabilities.Embedded.Allows(ShellCapability.ProjectExplorer));
    }

    [Fact]
    public void EveryEmbeddedFlagIsAlsoInTheStandaloneSet()
    {
        Assert.Equal(ShellCapabilities.Embedded, ShellCapabilities.Embedded & ShellCapabilities.Standalone);
    }

    [Fact]
    public void ACompositeRequestNeedsEveryFlagItNames()
    {
        var granted = ShellCapability.EditModels | ShellCapability.Save;

        Assert.True(granted.Allows(ShellCapability.EditModels));
        Assert.False(granted.Allows(ShellCapability.EditModels | ShellCapability.Run));
    }

    [Fact]
    public void NothingIsGrantedByAnEmptySet()
    {
        Assert.False(ShellCapability.None.Allows(ShellCapability.Outline));
    }
}
