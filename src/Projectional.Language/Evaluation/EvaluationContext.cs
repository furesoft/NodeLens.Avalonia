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
    /// <summary>The scope at the top of a run, where no name is bound yet.</summary>
    public static IReadOnlyDictionary<string, Value> NoLocals { get; } =
        new Dictionary<string, Value>(StringComparer.Ordinal);

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
        : this(node, runtime, NoLocals)
    {
    }

    /// <summary>Creates the context one rule sees, with the names bound in the scope that reaches it.</summary>
    /// <param name="node">The node being evaluated.</param>
    /// <param name="runtime">The runtime that counts steps, records the trace and stops runaway runs.</param>
    /// <param name="locals">The values bound in the enclosing scope, such as a call's parameters.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// Public for the reason the two-argument form is: the runtime that calls it lives one layer up.
    /// </remarks>
    public EvaluationContext(Node node, IEvaluationRuntime runtime, IReadOnlyDictionary<string, Value> locals)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(locals);

        Node = node;
        _runtime = runtime;
        Locals = locals;
    }

    /// <summary>The node being evaluated.</summary>
    public Node Node { get; }

    /// <summary>The names bound in the scope this context stands in, empty at the top of a run.</summary>
    public IReadOnlyDictionary<string, Value> Locals { get; }

    /// <summary>The same node seen from a scope that binds <paramref name="locals"/> in front of this one.</summary>
    /// <param name="locals">The names to bind, which win over the enclosing scope and over the run own variables.</param>
    /// <returns>A context for <see cref="Node"/> whose children are evaluated inside the new scope.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="locals"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// What a call needs and a rule cannot build itself: the arguments are values the caller already computed, and a
    /// rule has no way to hand them to the body except through the context. The scope is a value, not a stack that is
    /// pushed and popped, because a rule that forgot to pop would leak one call parameters into the next.
    /// </remarks>
    public EvaluationContext With(IReadOnlyDictionary<string, Value> locals)
    {
        ArgumentNullException.ThrowIfNull(locals);

        if (locals.Count == 0)
        {
            return this;
        }

        Dictionary<string, Value> merged = new(Locals, StringComparer.Ordinal);

        foreach (var (name, value) in locals)
        {
            merged[name] = value;
        }

        return new EvaluationContext(Node, _runtime, merged);
    }

    /// <summary>What <paramref name="node"/> evaluates to.</summary>
    /// <param name="node">The node to evaluate, usually a child of <see cref="Node"/>.</param>
    /// <exception cref="EvaluationException">
    /// <paramref name="node"/> cannot be evaluated, or the run's budget or cancellation stops it.
    /// </exception>
    public Value Eval(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return _runtime.Evaluate(node, Locals);
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
            ? _runtime.Evaluate(children[0], Locals)
            : throw Error($"'{slot}' of {Node.Concept.Key} must hold exactly one child to be evaluated, but holds {children.Count}.");
    }

    /// <summary>What the node in the reference slot <paramref name="slot"/> evaluates to.</summary>
    /// <param name="slot">The reference slot to follow.</param>
    /// <exception cref="EvaluationException">
    /// The slot is not a reference, points nowhere, points at a node the model does not hold, or the node it points at
    /// cannot be evaluated.
    /// </exception>
    /// <remarks>
    /// The three refusals are separate sentences because they need different answers: a slot that is not a reference is a
    /// rule written against the wrong shape, a slot that points nowhere is a half-written document, and a dangling target
    /// is a reference whose node is gone. Answering the same thing for all three would send the reader to the wrong place.
    /// </remarks>
    public Value EvalReference(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        if (Node.Concept.FindReference(slot) is null)
        {
            throw Error($"'{slot}' is not a reference of {Node.Concept.Key}.");
        }

        if (Node.GetReferenceTarget(slot) is { } target)
        {
            return _runtime.Evaluate(target, Locals);
        }

        throw Node.GetReference(slot) is null
            ? Error($"'{slot}' of {Node.Concept.Key} points nowhere, so there is nothing to evaluate.")
            : Error($"'{slot}' of {Node.Concept.Key} points at a node this model does not hold.");
    }

    /// <summary>The same run seen from <paramref name="node"/>, which is how a rule speaks about another node.</summary>
    /// <param name="node">The node to answer for, such as an entry of a list the rule walks.</param>
    /// <returns>A context for <paramref name="node"/> with the same runtime and the same bound names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A rule is about one node, and a rule that selects an entry of a list then has to say what <em>that</em> entry answers -
    /// which is a different subject for the same body. Making it a context rather than a second evaluation path keeps the
    /// budget, the trace and the bound names in one place: a nested subject is still one run.
    /// </remarks>
    public EvaluationContext For(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new EvaluationContext(node, _runtime, Locals);
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

        return Locals.TryGetValue(name, out var bound) ? bound : _runtime.Variable(name) ?? throw Error($"'{name}' has no value in this run.");
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
            double number => new FloatValue(number),
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
