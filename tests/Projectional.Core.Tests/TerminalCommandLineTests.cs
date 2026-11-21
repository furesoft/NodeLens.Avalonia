using Projectional.Workbench;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A command typed into the terminal's one box, split the way a process starter needs it.</summary>
public sealed class TerminalCommandLineTests
{
    [Fact]
    public void TheFirstWordIsTheProgramAndTheRestAreItsArguments()
    {
        Assert.True(TerminalCommandLine.TrySplit("dotnet --info", out var command));

        Assert.Equal("dotnet", command.FileName);
        Assert.Equal(["--info"], command.Arguments);
    }

    [Fact]
    public void AQuotedPathStaysOneWord()
    {
        // The reason the grammar exists: half the interesting programs live under a path with a space in it.
        Assert.True(TerminalCommandLine.TrySplit("\"C:\\Program Files\\x.exe\" run --quiet", out var command));

        Assert.Equal("C:\\Program Files\\x.exe", command.FileName);
        Assert.Equal(["run", "--quiet"], command.Arguments);
    }

    [Fact]
    public void AnEmptyQuotedArgumentIsAnArgument()
    {
        // `prog ""` is one argument that is empty, not an argument that was never passed - a difference a program sees.
        Assert.True(TerminalCommandLine.TrySplit("prog \"\"", out var command));

        Assert.Equal(string.Empty, Assert.Single(command.Arguments));
    }

    [Fact]
    public void BlanksBetweenWordsAreNotWords()
    {
        Assert.True(TerminalCommandLine.TrySplit("   echo    hi   ", out var command));

        Assert.Equal("echo", command.FileName);
        Assert.Equal(["hi"], command.Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ALineWithoutAProgramIsRefused(string line)
    {
        Assert.False(TerminalCommandLine.TrySplit(line, out _));
    }
}
