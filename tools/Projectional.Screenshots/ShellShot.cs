using Projectional.Workbench;

namespace Projectional.Screenshots;

/// <summary>One image of the shell: the window's own size and the state to drive it into.</summary>
/// <param name="FileName">The name the PNG is written under, without the extension.</param>
/// <param name="Arrange">Drives the window into the state the feature is about.</param>
/// <param name="Width">The width the window is shown at.</param>
/// <param name="Height">The height the window is shown at.</param>
/// <remarks>
/// A shell feature is a panel of the window rather than a state of one control, so these shots are taken of
/// <see cref="WorkbenchView"/> instead of the editor control. The window is cropped by nothing: the menu bar, the
/// outline, the document and the panel are the picture, and cutting the page away would cut away the shell.
/// </remarks>
internal sealed record ShellShot(
    string FileName,
    Func<WorkbenchView, Task> Arrange,
    double Width = 980,
    double Height = 400);
