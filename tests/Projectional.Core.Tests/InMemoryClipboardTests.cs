using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The in-memory clipboard is the fallback an <see cref="EditorSession"/> gets when no host supplies one.
/// </summary>
/// <remarks>
/// It is one string and nothing else, so the tests are about the two properties that matter: text survives
/// being read back, and two clipboards do not share it. <see cref="ITextClipboard.RefreshAsync"/> is the
/// default implementation, which completes without doing anything because there is nothing to re-read.
/// </remarks>
public sealed class InMemoryClipboardTests
{
    [Fact]
    public void StartsEmpty()
    {
        Assert.Null(new InMemoryClipboard().Text);
    }

    [Fact]
    public void KeepsTheTextItIsGiven()
    {
        var clipboard = new InMemoryClipboard { Text = "abc" };

        Assert.Equal("abc", clipboard.Text);
    }

    [Fact]
    public void ReplacesTheTextItHolds()
    {
        var clipboard = new InMemoryClipboard { Text = "abc" };
        clipboard.Text = "xyz";

        Assert.Equal("xyz", clipboard.Text);
    }

    [Fact]
    public void CanBeEmptiedAgain()
    {
        var clipboard = new InMemoryClipboard { Text = "abc" };
        clipboard.Text = null;

        Assert.Null(clipboard.Text);
    }

    [Fact]
    public async Task RefreshAsyncCompletesWithoutChangingTheText()
    {
        ITextClipboard clipboard = new InMemoryClipboard { Text = "abc" };

        await clipboard.RefreshAsync();

        Assert.Equal("abc", clipboard.Text);
    }

    [Fact]
    public void TwoClipboardsDoNotShareText()
    {
        var first = new InMemoryClipboard { Text = "first" };
        var second = new InMemoryClipboard();

        Assert.Null(second.Text);

        second.Text = "second";

        Assert.Equal("first", first.Text);
        Assert.Equal("second", second.Text);
    }
}
