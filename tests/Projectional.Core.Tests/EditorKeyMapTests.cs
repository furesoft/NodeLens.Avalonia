using Avalonia.Input;

using Projectional.Avalonia;
using Projectional.Editing;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The translation from window keys to editor keys, which is the only part of input handling that has
/// no window in it.
/// </summary>
public sealed class EditorKeyMapTests
{
    [Theory]
    [InlineData(Key.Left, EditorKey.Left)]
    [InlineData(Key.Right, EditorKey.Right)]
    [InlineData(Key.Up, EditorKey.Up)]
    [InlineData(Key.Down, EditorKey.Down)]
    [InlineData(Key.Home, EditorKey.Home)]
    [InlineData(Key.End, EditorKey.End)]
    [InlineData(Key.Enter, EditorKey.Enter)]
    [InlineData(Key.Back, EditorKey.Backspace)]
    [InlineData(Key.Delete, EditorKey.Delete)]
    [InlineData(Key.Escape, EditorKey.Escape)]
    public void NavigationKeysMapToOneEditorKey(Key key, EditorKey expected)
    {
        Assert.Equal(expected, EditorKeyMap.Map(key, KeyModifiers.None));
    }

    [Theory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift)]
    public void ControlSpaceIsTheCompletionGesture(KeyModifiers modifiers)
    {
        // The second exception to "Control belongs to the host": every editor with completion uses it.
        Assert.Equal(EditorKey.ControlSpace, EditorKeyMap.Map(Key.Space, modifiers));

        // A bare space is a character, so it arrives as text input and is not an editor key.
        Assert.Null(EditorKeyMap.Map(Key.Space, KeyModifiers.None));
    }

    [Fact]
    public void TabMapsToTabAndShiftTabToShiftTab()
    {
        Assert.Equal(EditorKey.Tab, EditorKeyMap.Map(Key.Tab, KeyModifiers.None));
        Assert.Equal(EditorKey.ShiftTab, EditorKeyMap.Map(Key.Tab, KeyModifiers.Shift));
    }

    [Fact]
    public void ShiftDoesNotChangeTheNavigationKeys()
    {
        Assert.Equal(EditorKey.Left, EditorKeyMap.Map(Key.Left, KeyModifiers.Shift));
        Assert.Equal(EditorKey.Delete, EditorKeyMap.Map(Key.Delete, KeyModifiers.Shift));
    }

    [Theory]
    [InlineData(KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Meta)]
    [InlineData(KeyModifiers.Alt | KeyModifiers.Shift)]
    public void HostModifiersLeaveTheKeyToTheHost(KeyModifiers modifiers)
    {
        Assert.Null(EditorKeyMap.Map(Key.Left, modifiers));
        Assert.Null(EditorKeyMap.Map(Key.Back, modifiers));
    }

    [Theory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift)]
    public void ControlReachesTheArrows(KeyModifiers modifiers)
    {
        // The arrows move a cell at a time, so the character step has to stay reachable.
        Assert.Equal(EditorKey.ControlLeft, EditorKeyMap.Map(Key.Left, modifiers));
        Assert.Equal(EditorKey.ControlRight, EditorKeyMap.Map(Key.Right, modifiers));

        Assert.Null(EditorKeyMap.Map(Key.Back, modifiers));
        Assert.Null(EditorKeyMap.Map(Key.Home, modifiers));
    }

    [Fact]
    public void AltReachesOnlyTheVerticalArrows()
    {
        // Reordering a node is an edit no host can offer for the editor, so Alt+Up/Down is the one
        // exception to "Alt belongs to the host" - and it is where every editor puts the same job.
        Assert.Equal(EditorKey.AltUp, EditorKeyMap.Map(Key.Up, KeyModifiers.Alt));
        Assert.Equal(EditorKey.AltDown, EditorKeyMap.Map(Key.Down, KeyModifiers.Alt));

        Assert.Null(EditorKeyMap.Map(Key.Left, KeyModifiers.Alt));
        Assert.Null(EditorKeyMap.Map(Key.Right, KeyModifiers.Alt));
        Assert.Null(EditorKeyMap.Map(Key.Up, KeyModifiers.Alt | KeyModifiers.Control));
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.Space)]
    [InlineData(Key.F5)]
    public void KeysTheEditorDoesNotRouteMapToNothing(Key key)
    {
        // Text arrives as text input, not as key events, so a letter is not an editor key.
        Assert.Null(EditorKeyMap.Map(key, KeyModifiers.None));
    }
}
