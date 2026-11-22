using Projectional.Core;

namespace Projectional.Language;

/// <summary>The templates a generator declares, keyed by concept, with inheritance and nothing behind them.</summary>
/// <remarks>
/// <para>
/// A concept's template is its own or, failing that, the nearest super concept's - the same lookup notation,
/// triggers, type rules, constraints and evaluation rules use, so a language has one answer to "which declaration
/// applies". Inheriting is what lets a generator cover an abstract concept once (<c>Expression</c> renders as
/// <c>SelfPart</c> for every operand that overrides nothing) without repeating it per subtype.
/// </para>
/// <para>
/// The registry is immutable and <see cref="Add"/> answers a new one, so a generator can be composed without mutating
/// a registry another session is using - the same shape as <see cref="EvaluationRegistry"/>.
/// </para>
/// </remarks>
public sealed class TemplateRegistry
{
    private readonly Dictionary<string, TextTemplate> _templates;

    private TemplateRegistry(Dictionary<string, TextTemplate> templates) => _templates = templates;

    /// <summary>A registry that declares nothing, so no node can be generated.</summary>
    public static TemplateRegistry Empty { get; } = new([]);

    /// <summary>How many concepts declare a template.</summary>
    public int Count => _templates.Count;

    /// <summary>The templates this registry declares, keyed by concept, for a caller that has to read it out.</summary>
    /// <remarks>
    /// The registry answers <see cref="TemplateFor"/> for a lookup, and that is all a generator needs. An export needs
    /// the other direction - what is in here - because it writes the registry as code rather than querying it.
    /// </remarks>
    public IReadOnlyDictionary<string, TextTemplate> Templates => _templates;

    /// <summary>The same registry with <paramref name="template"/> answering for <paramref name="conceptKey"/>.</summary>
    /// <param name="conceptKey">The concept the template is about.</param>
    /// <param name="template">What a node of that concept emits.</param>
    /// <exception cref="ArgumentException">The concept key is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="template"/> is <see langword="null"/>.</exception>
    public TemplateRegistry Add(string conceptKey, TextTemplate template)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentNullException.ThrowIfNull(template);

        var templates = new Dictionary<string, TextTemplate>(_templates, StringComparer.Ordinal)
        {
            [conceptKey] = template,
        };

        return new TemplateRegistry(templates);
    }

    /// <summary>The template that answers for <paramref name="concept"/>, or <see langword="null"/>.</summary>
    /// <param name="concept">The concept to ask about.</param>
    public TextTemplate? TemplateFor(Concept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        return _templates.Count == 0 ? null : Find(concept, []);
    }

    private TextTemplate? Find(Concept concept, HashSet<string> seen)
    {
        if (!seen.Add(concept.Key))
        {
            return null;
        }

        if (_templates.TryGetValue(concept.Key, out var template))
        {
            return template;
        }

        foreach (var super in concept.SuperConcepts)
        {
            if (Find(super, seen) is { } inherited)
            {
                return inherited;
            }
        }

        return null;
    }

    public override string ToString() => $"{_templates.Count} template(s)";
}
