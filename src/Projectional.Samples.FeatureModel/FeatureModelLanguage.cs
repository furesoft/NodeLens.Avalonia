using Projectional.Core;
using Projectional.Language;

namespace Projectional.Samples.FeatureModel;

/// <summary>
/// A feature model: the features of a product line, and the `requires`/`excludes` constraints between them.
/// </summary>
/// <remarks>
/// <para>
/// The second of the three worked examples (docs/architecture/worked-examples.md), and the axis it proves is
/// <b>constraints that read references</b>: a rule about two features, stated as a node of its own rather than
/// as a property of either. That is a different shape from the state machine's, where a reference *is* the
/// structure - here a constraint is a statement <em>about</em> the structure, which is what a checker reads.
/// </para>
/// <para>
/// `requires` and `excludes` are two concepts instead of one concept with an operator, for the same reason a
/// language would rather have two: each is drawn with its own word, and the check that reads them asks two
/// different questions without a switch over an enumeration.
/// </para>
/// <para>
/// The selection is a property (<c>Fm.Feature.isSelected</c>) rather than an input to the interpreter: a
/// configuration is part of the document - it is saved with it and undone with it - while a variable is a value
/// the reader supplies for one run. The state machine's `attempts` is the second; this is the first.
/// </para>
/// </remarks>
public static class FeatureModelLanguage
{
    /// <summary>The slot of a model that holds its features.</summary>
    public const string FeaturesSlot = "features";

    /// <summary>The slot of a model that holds its constraints.</summary>
    public const string ConstraintsSlot = "constraints";

    /// <summary>The reference a constraint uses to name the feature it is about.</summary>
    public const string SubjectSlot = "subject";

    /// <summary>The reference a constraint uses to name the other feature.</summary>
    public const string OtherSlot = "other";

    /// <summary>The property of a feature that says whether the configuration selects it.</summary>
    public const string SelectedSlot = "isSelected";

    /// <summary>The concepts this language adds, which are the only ones it declares.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } = CreateDeclarations();

    /// <summary>The registry this language declares.</summary>
    /// <remarks>
    /// No borrowed sub-language: a feature model is a closed language, which is the other half of what a
    /// worked example should show - not every DSL is composed out of `Expressions`.
    /// </remarks>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A registry for the concepts of this language.</summary>
    /// <param name="concepts">The registry to draw with.</param>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);
        FeatureModelNotations.AddTo(notations);

        return notations;
    }

    /// <summary>A project for a document of this language.</summary>
    public static Project NewProject() => new(Concepts);

    private static List<ConceptDeclaration> CreateDeclarations() =>
    [
        new ConceptDeclaration(
            FmConceptKeys.Model,
            [
                new PropertySlot("name", PropertyType.String, "model"),
                new ChildSlot(FeaturesSlot, FmConceptKeys.Feature, Cardinality.Many),
                new ChildSlot(ConstraintsSlot, FmConceptKeys.Constraint, Cardinality.Many),
            ],
            description: "A set of features and the rules that hold between them.",
            interfaces: [FrameworkInterfaces.Named]),

        new ConceptDeclaration(
            FmConceptKeys.Feature,
            [
                new PropertySlot("name", PropertyType.String, "feature"),
                new PropertySlot(
                    SelectedSlot,
                    PropertyType.Boolean,
                    "false",
                    "Whether this configuration selects the feature.",
                    showDescriptionWhenFilled: true),
            ],
            description: "One feature of the product line.",
            interfaces: [FrameworkInterfaces.Named]),

        // Abstract, because a slot name a concept and not a set: it is what lets one `constraints` list hold
        // both kinds. It is never instantiated - `DefaultInstantiableFor` answers its first concrete
        // descendant - and it has no notation, because nothing in a document is one.
        new ConceptDeclaration(
            FmConceptKeys.Constraint,
            [
                // A constraint holds two references into the model that holds *it*, so both slots lift the
                // own-subtree rule for the same reason the state machine's arrows do.
                new ReferenceSlot(SubjectSlot, FmConceptKeys.Feature, allowOwnSubtree: true),
                new ReferenceSlot(OtherSlot, FmConceptKeys.Feature, allowOwnSubtree: true),
            ],
            isAbstract: true,
            description: "A rule about two features."),

        new ConceptDeclaration(
            FmConceptKeys.Requires,
            superConcepts: [FmConceptKeys.Constraint],
            description: "If the first feature is selected, the second one must be too."),

        new ConceptDeclaration(
            FmConceptKeys.Excludes,
            superConcepts: [FmConceptKeys.Constraint],
            description: "If the first feature is selected, the second one must not be."),
    ];
}
