namespace Projectional.EvaluationRules;

/// <summary>A rule model that cannot be turned into the rule the interpreter runs.</summary>
/// <remarks>
/// Its own type rather than <see cref="InvalidOperationException"/>, because the caller is a language loader: a folder whose
/// rule says nothing, or names a concept the language does not declare, is a language error a user has to be told about -
/// the same way a structure definition that names an unknown concept is.
/// </remarks>
public sealed class EvaluationRuleException : Exception
{
    /// <summary>Creates the exception for the body or rule <paramref name="node"/>, saying <paramref name="message"/>.</summary>
    /// <param name="node">The node the rule could not be built from.</param>
    /// <param name="message">What is wrong, in the words a user sees.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public EvaluationRuleException(Core.Node node, string message)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(node);

        ConceptKey = node.Concept.Key;
    }

    /// <summary>The concept of the node that could not be built into a rule.</summary>
    public string ConceptKey { get; }
}
