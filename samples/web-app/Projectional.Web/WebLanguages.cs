using Projectional.Core;
using Projectional.Expressions;
using Projectional.Expressions.Intentions;
using Projectional.Samples;
using Projectional.Samples.Checks;
using Projectional.Samples.FeatureModel;
using Projectional.Samples.Intentions;
using Projectional.Samples.SqlSchema;
using Projectional.Samples.StateMachine;
using Projectional.Samples.StateMachine.Checks;

namespace Projectional.Web;

/// <summary>The languages the browser editor offers, and what each one needs to be opened.</summary>
/// <remarks>
/// The repository's worked examples plus the declared samples under <c>samples/</c>, which is everything the desktop
/// shell can open except C# - a base language rather than a document language, because its concepts are declared
/// <c>isRoot: false</c> and only a declared language supplies the root a document opens with.
/// </remarks>
public static class WebLanguages
{
    private static IReadOnlyList<WebLanguage>? _all;

    /// <summary>The payroll rules language: a ruleset, its rules, and expressions for the conditions.</summary>
    public static WebLanguage Rules { get; } = new()
    {
        Name = "Rules",
        Concepts = RulesLanguage.Concepts,
        Notations = RulesLanguage.CreateNotations(RulesLanguage.Concepts),
        Typing = RulesLanguage.TypingRules,
        Types = ExpressionsLanguage.Types,
        Constraints = SampleConstraints.All.With(ExpressionsLanguage.Constraints),
        Emphasis = RulesLanguage.Emphasis,
        Intentions = [ExpressionsIntentions.Provider, RulesIntentions.Provider],
        NewEmpty = PayrollSample.NewEmptyModel,
        NewSample = PayrollSample.NewSampleModel,
    };

    /// <summary>The state machine: states and transitions that reference them.</summary>
    public static WebLanguage StateMachine { get; } = new()
    {
        Name = "StateMachine",
        Concepts = StateMachineLanguage.Concepts,
        Notations = StateMachineLanguage.CreateNotations(StateMachineLanguage.Concepts),
        Typing = StateMachineLanguage.TypingRules,
        Types = ExpressionsLanguage.Types,
        Constraints = StateMachineConstraints.All.With(ExpressionsLanguage.Constraints),
        Intentions = [ExpressionsIntentions.Provider],
        NewEmpty = StateMachineDocuments.NewEmptyModel,
        NewSample = StateMachineDocuments.NewSampleModel,
    };

    /// <summary>A feature model: features and the requires/excludes rules between them.</summary>
    public static WebLanguage FeatureModel { get; } = new()
    {
        Name = "FeatureModel",
        Concepts = FeatureModelLanguage.Concepts,
        Notations = FeatureModelLanguage.CreateNotations(FeatureModelLanguage.Concepts),
        NewEmpty = () =>
        {
            var model = FeatureModelLanguage.NewProject().NewModel("features");
            model.AddRoot(model.NewNode(FmConceptKeys.Model));

            return model;
        },
        NewSample = SampleDocuments.FeatureModel,
    };

    /// <summary>A SQL schema: tables, columns, and foreign keys that reference them.</summary>
    public static WebLanguage SqlSchema { get; } = new()
    {
        Name = "SqlSchema",
        Concepts = SqlSchemaLanguage.Concepts,
        Notations = SqlSchemaLanguage.CreateNotations(SqlSchemaLanguage.Concepts),
        Types = SqlSchemaLanguage.Types,
        Constraints = SqlSchemaLanguage.Constraints,
        NewEmpty = () =>
        {
            var model = SqlSchemaLanguage.NewProject().NewModel("schema");
            model.AddRoot(model.NewNode(SqlConceptKeys.Schema));

            return model;
        },
        NewSample = SampleDocuments.SqlSchema,
    };

    /// <summary>
    /// Every language: the code-declared four and the declared samples, in the order the menus offer them.
    /// </summary>
    /// <remarks>
    /// Built on first use rather than at type initialization, because reading the declared samples touches the file
    /// system. A restore of a code-declared document therefore does not pay for it.
    /// </remarks>
    public static IReadOnlyList<WebLanguage> All =>
        _all ??= [Rules, StateMachine, FeatureModel, SqlSchema, .. DeclaredSamples.All];

    /// <summary>The language named <paramref name="name"/>, or the rules language when there is none.</summary>
    /// <param name="name">The name a saved document stored.</param>
    /// <remarks>
    /// The four code languages are checked before <see cref="All"/> is touched, so restoring a document does not
    /// have to read the declared samples; an unknown name still falls back to the first language rather than failing
    /// the whole restore. A concept the fallback does not know is preserved and reported, which is what the
    /// persistence rule asks of every reader.
    /// </remarks>
    public static WebLanguage ByName(string? name)
    {
        foreach (var language in new[] { Rules, StateMachine, FeatureModel, SqlSchema })
        {
            if (string.Equals(language.Name, name, StringComparison.Ordinal))
            {
                return language;
            }
        }

        return All.FirstOrDefault(language => string.Equals(language.Name, name, StringComparison.Ordinal)) ?? Rules;
    }
}
