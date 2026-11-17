namespace Projectional.Language;

/// <summary>One way to turn a model into text: a name and the template of every concept.</summary>
/// <remarks>
/// <para>
/// A language may have several - the same rule table as C# and as JSON, or a generator per target version - which is
/// why a generator is a value with a name rather than a registry the language owns: the host picks one by name, the
/// way the model picks a notation variant, and nothing about the model changes.
/// </para>
/// <para>
/// The name is an identifier, not a sentence: it is what a menu shows and what a host stores, so a language is
/// expected to declare two generators that differ by more than punctuation. It is never translated.
/// </para>
/// </remarks>
public sealed class TextGenerator
{
    /// <summary>Creates a generator named <paramref name="name"/>.</summary>
    /// <param name="name">The generator's identifier, unique within its language.</param>
    /// <param name="templates">What each concept emits.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="templates"/> is <see langword="null"/>.</exception>
    public TextGenerator(string name, TemplateRegistry templates)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(templates);

        Name = name;
        Templates = templates;
    }

    /// <summary>The generator's identifier, unique within its language.</summary>
    public string Name { get; }

    /// <summary>What each concept emits.</summary>
    public TemplateRegistry Templates { get; }

    public override string ToString() => Name;
}
