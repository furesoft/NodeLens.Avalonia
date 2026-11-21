using Projectional.Workbench;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>What the screen shows for what the user typed into a program that cannot echo for itself.</summary>
public sealed class TerminalEchoTests
{
    [Fact]
    public void TypedTextShowsAsItself()
    {
        Assert.Equal("abc"u8.ToArray(), TerminalEcho.For("abc"));
    }

    [Fact]
    public void EnterAlsoCarriesTheLineBack()
    {
        // A newline without a carriage return would start the next line where the last one ended - which is what the
        // first picture of this panel showed.
        Assert.Equal("\r\n"u8.ToArray(), TerminalEcho.For("\n"));
        Assert.Equal("\r\n"u8.ToArray(), TerminalEcho.For("\r"));
    }

    [Fact]
    public void BackspaceErasesACell()
    {
        Assert.Equal("\b \b"u8.ToArray(), TerminalEcho.For("\u007f"));
    }

    [Fact]
    public void AKeySequenceShowsNothing()
    {
        // The arrows send an escape sequence, and printing it would put "^[[A" on the screen for every key pressed.
        Assert.Empty(TerminalEcho.For("\u001b[A"u8.ToArray()));
    }

    [Fact]
    public void AControlCharacterShowsNothing()
    {
        // Ctrl+C is an instruction to the program, not a character to write: the emulator's screen would show it as a
        // missing glyph instead of the break it asked for.
        Assert.Empty(TerminalEcho.For([0x03]));
    }
}
