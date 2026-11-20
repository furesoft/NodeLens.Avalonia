using Avalonia.Controls;

using Projectional.Workbench;

namespace Projectional.Screenshots;

/// <summary>One image of a dialog: the window it is opened from, and the dialog that window hands back.</summary>
/// <param name="FileName">The name the PNG is written under, without the extension.</param>
/// <param name="Open">Opens the dialog over the workbench and answers it, so it can be photographed.</param>
/// <param name="Width">The width the dialog is shown at.</param>
/// <param name="Height">The height the dialog is shown at.</param>
/// <remarks>
/// A dialog is a window of its own - its own tab strip, its own footer - so a picture of the workbench cannot show it.
/// The window the dialog belongs to is still shown first, because a dialog inherits the theme variant it was opened
/// with and a picture taken over nothing would be of a dialog in the wrong colours.
/// </remarks>
internal sealed record DialogShot(
    string FileName,
    Func<MainWindow, Task<Window>> Open,
    double Width = 760,
    double Height = 540);
