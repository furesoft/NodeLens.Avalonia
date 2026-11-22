namespace Projectional.Workbench;

/// <summary>One example language project shipped beside the executable.</summary>
/// <param name="Directory">The folder the project lives in.</param>
/// <param name="Name">What the language calls itself, or what its folder calls it when it names itself nothing.</param>
/// <param name="StandsOn">
/// The languages it declares a dependency on or an extension of, by their declared names - read from
/// <c>language.mps</c> and not resolved, because resolving is what opening it does.
/// </param>
internal sealed record ExampleProject(
    string Directory,
    string Name,
    IReadOnlyList<string> StandsOn,
    string? Description = null);
