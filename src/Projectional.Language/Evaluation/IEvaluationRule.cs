using Projectional.Core;

namespace Projectional.Language;

/// <summary>The aspect that says what a node evaluates to, per concept.</summary>
/// <remarks>
/// <para>
/// It lives in <c>Projectional.Language</c>, beside the notation, the typing rules and the type rules, for the
/// same reason they do: interpretation is a statement about what a language <em>means</em>, so a language
/// package must be able to declare it without referencing the semantics layer that runs it. The runtime
/// (<c>Interpreter</c> in <c>Projectional.Semantics</c>) sits above and reads the registry this assembly hands
/// it.
/// </para>
/// <para>
/// It is one function per concept, not a set of methods: a DSL that wants "this node's meaning is this
/// expression" needs exactly this, and a rule only ever answers a question - it does not mutate, does not
/// generate and does not cache. Everything a rule cannot know on its own (the variables of a run, the budget,
/// how deep it is) arrives through <see cref="EvaluationContext"/>, so a rule written for a host and a rule
/// written for a test are the same rule.
/// </para>
/// </remarks>
public interface IEvaluationRule
{
    /// <summary>What <paramref name="context"/>'s node evaluates to.</summary>
    /// <param name="context">The node, its children and the variables of the run.</param>
    /// <exception cref="EvaluationException">The node cannot be evaluated, with a message for the user.</exception>
    Value Evaluate(EvaluationContext context);
}
