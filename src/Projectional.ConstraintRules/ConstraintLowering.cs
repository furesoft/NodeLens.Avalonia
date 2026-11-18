using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.ConstraintRules;

/// <summary>Turns written rules into the <see cref="ConstraintSet"/> a language's engine runs.</summary>
/// <remarks>
/// <para>
/// This is the half that makes the declared path equal rather than weaker (ADR 0014): every rule here becomes an
/// <see cref="IConstraint"/>, so there is no second engine, no declared-only finding and no repair path that only one
/// of the two halves has.
/// </para>
/// <para>
/// It checks what a compiler would have checked - a rule says what is wrong, names a severity the language has, and
/// asks a question about a slot that exists and is the kind the body reads - and it deliberately leaves the graph to
/// the concept registry: whether a named concept exists is <see cref="ConceptRegistry.TryGet"/>'s answer.
/// </para>
/// <para>
/// A rule about <em>every</em> node cannot have its slot checked, because no single concept was named. That is not a
/// gap to close later: the rule is asked of every node and answers for the ones whose concept declares the slot with
/// the kind it reads.
/// </para>
/// </remarks>
public static class ConstraintLowering
{
    /// <summary>The rules a language's constraint documents declare.</summary>
    /// <param name="documents">
    /// The roots of those documents: one <see cref="ConstraintConceptKeys.Rule"/> per document, in file order.
    /// </param>
    /// <param name="concepts">The concepts those rules may name, which is the language's own registry.</param>
    /// <returns>The set, in the order the documents were given.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">A document cannot be lowered.</exception>
    public static ConstraintSet CreateConstraints(IEnumerable<Node> documents, ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(concepts);

        var rules = documents as IReadOnlyList<Node> ?? [.. documents];
        var set = ConstraintSet.Empty;

        foreach (var rule in rules)
        {
            if (rule.Concept.Key != ConstraintConceptKeys.Rule)
            {
                throw new ConceptDefinitionException(
                    $"'{rule.Concept.Key}' is not a constraint; a constraints document holds one '{ConstraintConceptKeys.Rule}'.");
            }

            var message = Required(rule, ConstraintLanguage.MessageSlot, "A rule has to say what is wrong.");

            var severity = Required(rule, ConstraintLanguage.SeveritySlot, "A rule has to say how much its finding matters.") switch
            {
                ConstraintSeverities.Error => DiagnosticSeverity.Error,
                ConstraintSeverities.Warning => DiagnosticSeverity.Warning,
                ConstraintSeverities.Hint => DiagnosticSeverity.Hint,
                var other => throw new ConceptDefinitionException(
                    $"'{other}' is not a severity; a rule reports an {ConstraintSeverities.Error}, a {ConstraintSeverities.Warning} or a {ConstraintSeverities.Hint}."),
            };

            var code = Optional(rule, ConstraintLanguage.CodeSlot);
            var conceptKey = Optional(rule, ConstraintLanguage.ConceptKeySlot);

            Concept? target = null;

            if (conceptKey is not null && !concepts.TryGet(conceptKey, out target))
            {
                throw new ConceptDefinitionException(
                    $"A rule names '{conceptKey}', which is not a concept of this language; leave the concept empty to state a rule about every node.");
            }

            var constraint = CreateBody(Single(rule, ConstraintLanguage.BodySlot), target, message, severity, code);

            set = target is null ? set.Everywhere(constraint) : set.Add(target.Key, constraint);
        }

        return set;
    }

    /// <summary>The constraint one body stands for.</summary>
    /// <param name="body">The body node, which is one of the four kinds the language declares.</param>
    /// <param name="target">The concept the rule is about, or <see langword="null"/> for every node.</param>
    /// <param name="message">The sentence the finding says.</param>
    /// <param name="severity">How much it matters.</param>
    /// <param name="code">The identifier to report under, or <see langword="null"/>.</param>
    private static IConstraint CreateBody(
        Node body,
        Concept? target,
        string message,
        DiagnosticSeverity severity,
        string? code)
    {
        var slot = Required(body, ConstraintLanguage.TargetSlot, $"'{body.Concept.Key}' has to name the slot it reads.");
        var kind = body.Concept.Key;

        if (target is not null)
        {
            Check(slot, kind, target);
        }
        else if (kind is not (ConstraintConceptKeys.EmptySlot
            or ConstraintConceptKeys.DefaultValue
            or ConstraintConceptKeys.SurroundingWhitespace
            or ConstraintConceptKeys.EmptyReference))
        {
            throw new ConceptDefinitionException($"'{kind}' is not a kind of check this framework runs.");
        }

        return kind switch
        {
            ConstraintConceptKeys.EmptySlot => new EmptySlotConstraint(slot, message, severity, code),
            ConstraintConceptKeys.DefaultValue => new DefaultValueConstraint(slot, message, severity, code),
            ConstraintConceptKeys.SurroundingWhitespace => new SurroundingWhitespaceConstraint(slot, message, severity, code),
            ConstraintConceptKeys.EmptyReference => new EmptyReferenceConstraint(slot, message, severity, code),
            _ => throw new ConceptDefinitionException($"'{kind}' is not a kind of check this framework runs."),
        };
    }

    /// <summary>Refuses a body whose slot the concept does not declare, or declares as another kind.</summary>
    /// <remarks>
    /// Checked here rather than at the check so that a typo is a load error naming the concept and the slot, at the
    /// moment the author is looking at the file - the rule the notation registry and the template lowering follow
    /// too. A rule that could never fire is the one failure a language author cannot see from the outside.
    /// </remarks>
    private static void Check(string slot, string kind, Concept target)
    {
        switch (kind)
        {
            case ConstraintConceptKeys.EmptySlot:
                if (target.FindChild(slot) is not { Cardinality: Cardinality.One })
                {
                    throw new ConceptDefinitionException(
                        $"'{slot}' is not a single-child slot of {target.Key}, so '{kind}' cannot ask whether it is empty.");
                }

                break;

            case ConstraintConceptKeys.DefaultValue:
                if (target.FindProperty(slot) is not { DefaultValue.Length: > 0 })
                {
                    throw new ConceptDefinitionException(
                        $"'{slot}' of {target.Key} is not a property with a declared default, so '{kind}' could never fire.");
                }

                break;

            case ConstraintConceptKeys.SurroundingWhitespace:
                if (target.FindProperty(slot) is not { } property || !PropertyType.String.Equals(property.Type))
                {
                    throw new ConceptDefinitionException(
                        $"'{slot}' of {target.Key} is not a text property, so '{kind}' cannot look for whitespace around it.");
                }

                break;

            case ConstraintConceptKeys.EmptyReference:
                if (target.FindReference(slot) is null)
                {
                    throw new ConceptDefinitionException(
                        $"'{slot}' is not a reference of {target.Key}, so '{kind}' cannot ask where it points.");
                }

                break;

            default:
                throw new ConceptDefinitionException($"'{kind}' is not a kind of check this framework runs.");
        }
    }

    private static string Required(Node node, string slot, string message)
    {
        var value = node.GetOrNull(slot);

        return string.IsNullOrWhiteSpace(value) ? throw new ConceptDefinitionException(message) : value;
    }

    private static string? Optional(Node node, string slot)
    {
        var value = node.GetOrNull(slot);

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Node Single(Node node, string slot)
    {
        var children = node.GetChildren(slot);

        return children.Count == 1
            ? children[0]
            : throw new ConceptDefinitionException(
                $"'{slot}' of {node.Concept.Key} must hold exactly one child to be lowered, but holds {children.Count}.");
    }
}
