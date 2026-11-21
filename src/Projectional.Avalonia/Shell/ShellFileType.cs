namespace Projectional.Avalonia;

/// <summary>One kind of file a dialog offers: how it is called, and the patterns that match it.</summary>
/// <param name="Name">What the picker shows the user, in their language.</param>
/// <param name="Patterns">The glob patterns, such as <c>*.mps</c>.</param>
/// <remarks>
/// A value rather than two parallel arguments, and free of Avalonia's own <c>FilePickerFileType</c> on purpose: the
/// interface that consumes it has to be implementable by a host whose dialogs are not Avalonia's, and a signature
/// carrying a toolkit type would make that impossible without also taking the toolkit.
/// </remarks>
public sealed record ShellFileType(string Name, IReadOnlyList<string> Patterns);
