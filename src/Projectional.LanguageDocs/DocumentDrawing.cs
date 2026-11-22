namespace Projectional.LanguageDocs;

/// <summary>One document a language ships, drawn: its name, the picture, or why there is none.</summary>
/// <param name="Name">What the document calls itself, or its file name when it calls itself nothing.</param>
/// <param name="Html">The drawing, one line per line, every classified piece in a span.</param>
/// <param name="Refusal">Why the document could not be drawn, or <see langword="null"/> when it was.</param>
/// <remarks>
/// A refusal is part of the answer rather than an exception, because one unreadable document must not cost the page
/// the ones that do read - and a page that silently left it out would look like a language that ships fewer documents
/// than it does.
/// </remarks>
internal sealed record DocumentDrawing(string Name, string Html, string? Refusal);