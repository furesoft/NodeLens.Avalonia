using Projectional.Avalonia;

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Projectional.Workbench;

/// <summary>
/// Asks whether to keep, throw away or abandon the changes to a document.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia ships no message box, and a shell that closed a window over unsaved work would be the one
/// bug users never forgive, so the question is a small window of its own. It is built in code rather
/// than in XAML because it is three buttons and a sentence: a markup file would add a code-generation
/// step and an <c>x:Class</c> to a dialog nobody will ever restyle.
/// </para>
/// <para>
/// Dismissing the prompt - Escape, the window close button - answers <see cref="UnsavedChoice.Cancel"/>,
/// which is the only safe default: a question that loses data when it is ignored is worse than no
/// question at all.
/// </para>
/// </remarks>
internal sealed class UnsavedChangesPrompt : Window
{
    private UnsavedChangesPrompt(string documentName)
    {
        Title = "Unsaved changes";
        SizeToContent = SizeToContent.Height;
        Width = 420;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var save = Button("_Save", UnsavedChoice.Save, isDefault: true);
        var discard = Button("_Discard", UnsavedChoice.Discard);
        var cancel = Button("_Cancel", UnsavedChoice.Cancel, isCancel: true);

        Content = new StackPanel
        {
            Margin = new global::Avalonia.Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"{documentName} has changes that are not on disk.",
                    TextWrapping = TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { discard, cancel, save },
                },
            },
        };
    }

    /// <summary>Shows the prompt and waits for the answer.</summary>
    /// <param name="owner">The window the prompt belongs to.</param>
    /// <param name="documentName">What to call the document in the question.</param>
    /// <returns>The user's choice, or <see cref="UnsavedChoice.Cancel"/> if they dismissed it.</returns>
    internal static async Task<UnsavedChoice> AskAsync(Window owner, string documentName)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var prompt = new UnsavedChangesPrompt(documentName);

        return await prompt.ShowDialog<UnsavedChoice>(owner).ConfigureAwait(true);
    }

    private Button Button(string text, UnsavedChoice choice, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button
        {
            Content = text,
            IsDefault = isDefault,
            IsCancel = isCancel,
            MinWidth = 88,
        };

        button.Click += (_, _) => Close(choice);

        return button;
    }
}
