using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;

namespace Projectional.Samples.StateMachine;

/// <summary>
/// A state machine: states, and transitions that reference the state they start in and the one they lead to.
/// </summary>
/// <remarks>
/// <para>
/// The first of the three worked examples (docs/architecture/worked-examples.md), and the axis it proves is
/// <b>references</b>: a transition names two states instead of holding them, which is what makes a diagram
/// out of a tree - the same state is one node reached from many transitions.
/// </para>
/// <para>
/// Its second axis is the one an interface made cheap: `Sm.State` and `Sm.Machine` implement
/// <see cref="FrameworkInterfaces.Named"/>, so neither declares a name slot, and a reference to "any named
/// node" would accept them without this language saying anything else.
/// </para>
/// </remarks>
public static class StateMachineLanguage
{
    /// <summary>The variant that reads a machine as a table of its transitions.</summary>
    public const string TableVariant = "table";

    /// <summary>The slot of a machine that holds its states.</summary>
    public const string StatesSlot = "states";

    /// <summary>The slot of a machine that holds its transitions.</summary>
    public const string TransitionsSlot = "transitions";

    /// <summary>The reference a machine uses to name the state it starts in.</summary>
    public const string InitialSlot = "initial";

    /// <summary>The concepts this language adds, which are the only ones it declares.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } = CreateDeclarations();

    /// <summary>The registry, composed with the expression language it borrows.</summary>
    /// <remarks>
    /// A guard is an <see cref="ExpressionsConceptKeys.Expression"/>, so the expression declarations are part of
    /// what a host has to load with this language - the same composition the rules language makes.
    /// </remarks>
    public static ConceptRegistry Concepts { get; } =
        ConceptRegistry.Create([.. ExpressionsLanguage.ConceptDeclarations, .. ConceptDeclarations]);

    /// <summary>A registry for the concepts of this language and nothing else.</summary>
    /// <param name="concepts">The registry to draw with.</param>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);

        // The borrowed sub-language's own notation comes with it: a guard is drawn by the expression
        // notations, and a host that forgot them would see missing-notation cells inside every transition.
        ExpressionsLanguage.AddNotations(notations);
        StateMachineNotations.AddTo(notations);

        return notations;
    }

    /// <summary>A project for a document of this language.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>What the language says about text typed into an empty slot.</summary>
    public static ITypingRules TypingRules => ExpressionsLanguage.TypingRules;

    private static List<ConceptDeclaration> CreateDeclarations() =>
    [
        new ConceptDeclaration(
            SmConceptKeys.Machine,
            [
                // Declared here rather than left to the interface because the placeholder is the language's
                // word: a machine is called "machine" until someone names it.
                new PropertySlot("name", PropertyType.String, "machine"),
                new ChildSlot(StatesSlot, SmConceptKeys.State, Cardinality.Many),
                new ChildSlot(TransitionsSlot, SmConceptKeys.Transition, Cardinality.Many),

                // `allowOwnSubtree` is what makes a graph out of a tree: the initial state *is* one of this
                // machine's children, so the invariant that a reference never points into its own subtree has to
                // be lifted for exactly this slot - deliberately, here, rather than globally.
                new ReferenceSlot(InitialSlot, SmConceptKeys.State, allowOwnSubtree: true),
            ],
            description: "A set of states and the transitions between them.",
            interfaces: [FrameworkInterfaces.Named],
            nameRule: NameRule.Pascal),

        new ConceptDeclaration(
            SmConceptKeys.State,
            [
                new PropertySlot("name", PropertyType.String, "state"),
                new PropertySlot(
                    "isFinal",
                    PropertyType.Boolean,
                    "false",
                    "Whether the machine may stop here.",
                    showDescriptionWhenFilled: true),
            ],
            description: "One state of a machine.",
            interfaces: [FrameworkInterfaces.Named],
            nameRule: NameRule.Pascal),

        new ConceptDeclaration(
            SmConceptKeys.Transition,
            [
                // The reference is what makes this a graph: a transition does not hold its states, it points
                // at them - and it points at states of the machine that holds *it*, so both slots lift the
                // own-subtree rule for the same reason the initial state does.
                new ReferenceSlot("source", SmConceptKeys.State, allowOwnSubtree: true),
                new ReferenceSlot("target", SmConceptKeys.State, allowOwnSubtree: true),
                new PropertySlot("event", PropertyType.String, description: "The event that fires this transition."),
                new ChildSlot("guard", ExpressionsConceptKeys.Expression, Cardinality.One),
            ],
            description: "One arrow: from a state to a state, when an event arrives and the guard holds."),
    ];
}
