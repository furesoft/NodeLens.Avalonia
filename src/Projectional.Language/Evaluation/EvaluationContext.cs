using Projectional.Core;

namespace Projectional.Language;

/// <summary>
/// What one evaluation rule sees: its node, the way to ask about other nodes, and the variables of the run.
/// </summary>
/// <remarks>
/// <para>
/// The context is small on purpose, like a type's and a constraint's: a rule can read the node, evaluate a
/// child and read a variable, and nothing else. A rule that could mutate the model, reach the editor or walk
/// the tree on its own would be a second editing path, and a rule that ran on a model nobody is drawing would
/// stop working.
/// </para>
/// <para>
/// <see cref="Eval(Node)"/> is the whole of composition: a binary expression asks its operands, a ruleset asks
/// its rules, and the runtime - not the rule - decides what that costs, how deep it goes and what it records.
/// An empty slot is therefore not silently absent: <see cref="EvalChild(string)"/> throws, because a node whose
/// meaning needs a child that is not there cannot produce a value, and the user has to be told which slot.
/// </para>
/// </remarks>
public sealed class EvaluationContext
{
    private readonly IEvaluationRuntime _runtime;

    /// <summary>Creates the context one rule sees.</summary>
    /// <param name="node">The node being evaluated.</param>
    /// <param name="runtime">The runtime that counts steps, records the trace and stops runaway runs.</param>
    /// <remarks>
    /// Public because the runtime that calls it lives one layer up (<c>Projectional.Semantics</c>) and this
    /// assembly grants no internals to it. Only a runtime constructs one: a rule receives it, never builds it,
    /// which is what keeps "who owns the budget" answerable.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public EvaluationContext(Node node, IEvaluationRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(runtime);

        Node = node;
        _runtime = runtime;
    }

    /// <summary>The node being evaluated.</summary>
    public Node Node { get; }

    /// <summary>What <paramref name="node"/> evaluates to.</summary>
    /// <param name="node">The node to evaluate, usually a child of <see cref="Node"/>.</param>
    /// <exception cref="EvaluationException">
    /// <paramref name="node"/> cannot be evaluated, or the run's budget or cancellation stops it.
    /// </exception>
    public Value Eval(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return _runtime.Evaluate(node);
    }

    /// <summary>What the single child in <paramref name="slot"/> evaluates to.</summary>
    /// <param name="slot">The child slot to read, which must hold exactly one child.</param>
    /// <exception cref="EvaluationException">
    /// The slot is empty - a half-written node cannot produce a value - or the child cannot be evaluated.
    /// </exception>
    public Value EvalChild(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        var children = Node.GetChildren(slot);

        return children.Count == 1
            ? _runtime.Evaluate(children[0])
            : throw Error($"'{slot}' of {Node.Concept.Key} must hold exactly one child to be evaluated, but holds {children.Count}.");
    }

    /// <summary>The value of the variable <paramref name="name"/> in this run.</summary>
    /// <param name="name">The name a node reads, such as <c>amount</c>.</param>
    /// <exception cref="EvaluationException">The run binds no value to that name.</exception>
    /// <remarks>
    /// A <c>Name</c> node is a free variable rather than a reference: what it means is supplied by whoever
    /// runs the model, which is what makes one decision table runnable against many inputs. Where those
    /// bindings come from is the host's business - the interpreter only carries them.
    /// </remarks>
    public Value Variable(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return _runtime.Variable(name) ?? throw Error($"'{name}' has no value in this run.");
    }

    /// <summary>The value of the property <paramref name="slot"/> as a <see cref="Value"/>.</summary>
    /// <param name="slot">The property slot to read.</param>
    /// <remarks>
    /// <para>
    /// The property's declared type is the authority on how its text is read - the same
    /// <c>PropertyType.TryParse</c> the model validates every write with - so a literal cannot mean one thing in
    /// the interpreter and another in the document. A missing value, a value the type refuses and a type with no
    /// value kind are all failures with a message, never a default.
    /// </para>
    /// <para>
    /// What a type turns into is framework knowledge rather than language knowledge, which is why this lives
    /// here: every language that evaluates a property would otherwise repeat the same switch, and the first one
    /// to forget a case would silently disagree with the others.
    /// </para>
    /// </remarks>
    /// <exception cref="EvaluationException">
    /// The slot is not a property, holds no value, or holds a value the interpreter has no kind for.
    /// </exception>
    public Value PropertyValue(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        if (Node.Concept.FindProperty(slot) is not { } property)
        {
            throw Error($"'{slot}' is not a property of {Node.Concept.Key}.");
        }

        if (!property.Type.TryParse(Node.GetOrNull(slot), out var parsed))
        {
            throw Error($"'{slot}' of {Node.Concept.Key} holds no valid {property.Type.Name}.");
        }

        return parsed switch
        {
            int number => new IntValue(number),
            bool flag => new BoolValue(flag),
            DateOnly date => new DateValue(date),
            string text => new StringValue(text),
            _ => throw Error($"'{slot}' of {Node.Concept.Key} holds a {property.Type.Name}, which has no value here."),
        };
    }

    /// <summary>The exception a rule throws when its node cannot produce a value.</summary>
    /// <param name="message">What is wrong, in the words the user sees in the trace.</param>
    /// <remarks>
    /// A factory rather than a constructor call so a rule does not have to name the framework's exception type
    /// at every throw site - the same shape <c>ctx.Error(node, key)</c> has in
    /// <c>docs/architecture/semantics.md</c>.
    /// </remarks>
    public EvaluationException Error(string message) => new(Node, message);
}
