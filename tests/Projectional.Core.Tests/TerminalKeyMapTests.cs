using Avalonia.Input;

using Projectional.Workbench;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Which keys a terminal has a sequence for, and which arrive as text instead.</summary>
public sealed class TerminalKeyMapTests
{
    [Theory]
    [InlineData(Key.Enter, "Enter")]
    [InlineData(Key.Escape, "Escape")]
    [InlineData(Key.Up, "Up")]
    [InlineData(Key.PageDown, "PageDown")]
    [InlineData(Key.F1, "F1")]
    [InlineData(Key.F12, "F12")]
    public void ANamedKeyHasTheEmulatorsNameForIt(Key key, string name) =>
        Assert.Equal(name, TerminalKeyMap.NameOf(key));

    [Fact]
    public void ALetterIsTextAndNotAKey()
    {
        // A letter that were also a key would send its sequence *and* its character, so the map and the text input
        // event must not both answer for it.
        Assert.Null(TerminalKeyMap.NameOf(Key.A));
        Assert.Null(TerminalKeyMap.NameOf(Key.Space));
    }

    [Fact]
    public void TheControlLetterIsTheCharacterAChordStands()
    {
        Assert.Equal("C", TerminalKeyMap.ControlLetterOf(Key.C));
        Assert.Equal("Z", TerminalKeyMap.ControlLetterOf(Key.Z));
    }

    [Fact]
    public void AChordOnSomethingThatIsNotALetterHasNoControlCharacter()
    {
        Assert.Null(TerminalKeyMap.ControlLetterOf(Key.D1));
        Assert.Null(TerminalKeyMap.ControlLetterOf(Key.Up));
    }
}
