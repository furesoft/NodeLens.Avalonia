using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Projectional.Avalonia;

namespace Projectional.Browser;

/// <summary>The unsaved-changes question, drawn over the shell because a browser has no modal window.</summary>
/// <remarks>
/// It uses <see cref="TopLevel.OverlayLayer"/> - the layer the platform already keeps above the page for popups - so the
/// question covers the shell without the shell knowing about it. The answer is the library's <see cref="UnsavedChoice"/>,
/// and dismissing the page never happens: the prompt stays until a button is pressed, which is what makes Cancel the
/// answer a stray click could not give.
/// </remarks>
internal static class BrowserUnsavedPrompt
{
    /// <summary>Asks what to do about <paramref name="title"/>, and answers when a button is pressed.</summary>
    internal static Task<UnsavedChoice> AskAsync(Control anchor, string title)
    {
        var answer = new TaskCompletionSource<UnsavedChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(18),
            MaxWidth = 460,
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"\"{title}\" has unsaved changes.",
                        Foreground = Brushes.Black,
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    buttons,
                },
            },
        };

        AddButton(buttons, "Save", UnsavedChoice.Save, answer);
        AddButton(buttons, "Discard", UnsavedChoice.Discard, answer);
        AddButton(buttons, "Cancel", UnsavedChoice.Cancel, answer);

        // A browser has no modal window, so the question is a popup anchored to the shell. Dismissing it is Cancel,
        // the value the enum documents for a dismissal, which is why that case is wired to the popup closing.
        var flyout = new Flyout { Content = card, Placement = PlacementMode.Center };

        flyout.Closed += (_, _) => answer.TrySetResult(UnsavedChoice.Cancel);
        flyout.ShowAt(anchor);

        return AwaitAsync(flyout, answer);
    }

    private static async Task<UnsavedChoice> AwaitAsync(Flyout flyout, TaskCompletionSource<UnsavedChoice> answer)
    {
        var choice = await answer.Task.ConfigureAwait(true);

        flyout.Hide();

        return choice;
    }

    private static void AddButton(
        Panel panel,
        string text,
        UnsavedChoice choice,
        TaskCompletionSource<UnsavedChoice> answer)
    {
        // The card is white whatever the shell's variant is, so the buttons state their own colours: a themed button
        // in the dark variant is white-on-transparent and would vanish into the card.
        var button = new Button
        {
            Content = text,
            Foreground = Brushes.Black,
            Background = Brushes.WhiteSmoke,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 4),
            MinWidth = 84,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        button.Click += (_, _) => answer.TrySetResult(choice);
        panel.Children.Add(button);
    }
}
