namespace Projectional.Workbench;

/// <summary>One line of what a module loaded: what was counted, and the count as it should be read.</summary>
/// <param name="Name">What the row is about, such as <c>Concepts</c>.</param>
/// <param name="Value">What was found, already spelled the way the dialog shows it.</param>
/// <remarks>
/// The value is a string rather than a number because not every row is one: a module either declares intentions or it
/// does not, and a column of zeros answers a question nobody asked. Formatting here rather than in the view keeps the
/// answer testable.
/// </remarks>
internal sealed record ModuleFact(string Name, string Value);
