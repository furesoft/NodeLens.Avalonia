namespace Projectional.Languages;

/// <summary>Something a language does that works, and that its author probably did not mean.</summary>
/// <param name="Aspect">The folder it came from, which is what the author has to open to act on it.</param>
/// <param name="Message">What it is, in the words the author reads.</param>
/// <remarks>
/// A warning rather than a refusal, because the language is usable: an error is what the folder cannot be loaded
/// without, and every refusal this layer makes is one. What is left over is the class of things that load and then
/// behave in a way nobody intended - a name that shadows an inherited one, for instance - and silently doing that is
/// the one thing worse than saying so.
/// </remarks>
public sealed record LanguageWarning(string Aspect, string Message);
