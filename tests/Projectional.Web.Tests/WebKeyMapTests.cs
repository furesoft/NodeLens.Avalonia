using Projectional.Editing;

using Xunit;

namespace Projectional.Web.Tests;

public sealed class WebKeyMapTests
{
    [Theory]
    [InlineData("ArrowLeft", false, false, false, EditorKey.Left)]
    [InlineData("ArrowRight", true, false, false, EditorKey.ControlRight)]
    [InlineData("ArrowUp", false, false, true, EditorKey.AltUp)]
    [InlineData("Tab", false, true, false, EditorKey.ShiftTab)]
    [InlineData(" ", true, false, false, EditorKey.ControlSpace)]
    [InlineData("Enter", false, false, false, EditorKey.Enter)]
    [InlineData("Escape", false, false, false, EditorKey.Escape)]
    [InlineData("a", true, false, false, EditorKey.SelectAll)]
    [InlineData("c", true, false, false, EditorKey.Copy)]
    [InlineData("v", true, false, false, EditorKey.Paste)]
    [InlineData("Insert", false, false, false, EditorKey.InsertBefore)]
    public void ABrowserKeyMapsToTheEditorsKey(
        string key,
        bool control,
        bool shift,
        bool alt,
        EditorKey expected)
    {
        Assert.Equal(expected, WebKeyMap.Map(key, control, shift, alt));
    }

    [Theory]
    [InlineData("F5", false, false, false)]
    [InlineData("a", false, false, false)]
    [InlineData("ArrowLeft", true, true, false)]
    [InlineData("s", true, true, false)]
    public void AKeyTheEditorHasNoMeaningForIsNotClaimed(string key, bool control, bool shift, bool alt)
    {
        Assert.Null(WebKeyMap.Map(key, control, shift, alt));
        Assert.False(WebKeyMap.IsHandled(key, control, shift, alt));
    }

    [Fact]
    public void TheHandledSetAndTheTableAgreeForEverythingTheEditorDoes()
    {
        Assert.True(WebKeyMap.IsHandled("ArrowLeft", control: false, shift: false, alt: false));
        Assert.True(WebKeyMap.IsHandled("Tab", control: false, shift: true, alt: false));
        Assert.True(WebKeyMap.IsHandled("z", control: true, shift: false, alt: false));
        Assert.True(WebKeyMap.IsHandled("s", control: true, shift: false, alt: false));
    }

    [Fact]
    public void PasteIsMappedButNotSwallowedSoTheBrowserCanRaiseItsNativePaste()
    {
        Assert.Equal(EditorKey.Paste, WebKeyMap.Map("v", control: true, shift: false, alt: false));
        Assert.False(WebKeyMap.IsHandled("v", control: true, shift: false, alt: false));
    }

    [Fact]
    public void SpaceAndLettersAreSpelledOneWayWhateverTheKeyboardCallsThem()
    {
        Assert.Equal("ctrl+Space", WebKeyMap.Chord(" ", control: true, shift: false, alt: false));
        Assert.Equal("shift+z", WebKeyMap.Chord("Z", control: false, shift: true, alt: false));
        Assert.True(WebKeyMap.IsHandled(" ", control: true, shift: false, alt: false));
    }
}
