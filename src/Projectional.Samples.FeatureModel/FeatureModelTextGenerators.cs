using Projectional.Language;

namespace Projectional.Samples.FeatureModel;

/// <summary>The feature model as configuration: one <c>name=value</c> line per feature.</summary>
/// <remarks>
/// <para>
/// A generator, not a notation: the same model read as something a build would consume, which is what the
/// framework's template walk is for. It is the sample's second axis and it is deliberately small - the pilot
/// already proves that a language has as many generators as it declares, and this one exists to show a
/// <em>different</em> target rather than a bigger one.
/// </para>
/// <para>
/// The list is separated by <see cref="LineBreakPart"/> and the template opens with nothing, so a model with no
/// features emits no text at all and one with features emits one clean line each - no leading blank line, which a
/// leading line break inside an indentation would have produced. That is a shape a nested tree could not have:
/// see the gap recorded in <c>docs/architecture/worked-examples.md</c>.
/// </para>
/// <para>
/// The constraints do not appear in the output. They are rules about the selection, and a configuration file is
/// what is left once the rules hold - the interpreter is what checks them.
/// </para>
/// </remarks>
public static class FeatureModelTextGenerators
{
    /// <summary>The name of the configuration generator.</summary>
    public const string ConfigName = "Configuration";

    /// <summary>Every feature as <c>name=true</c> or <c>name=false</c>, one per line.</summary>
    public static TextGenerator Config { get; } = new(ConfigName, TemplateRegistry.Empty
        .Add(FmConceptKeys.Model, TextTemplate.Of(
            new ChildrenPart(FeatureModelLanguage.FeaturesSlot, TextTemplate.Of(LineBreakPart.Instance))))

        .Add(FmConceptKeys.Feature, TextTemplate.Of(
            new PropertyPart("name"),
            new TextPart("="),
            new ConditionalPart(
                FeatureModelLanguage.SelectedSlot,
                "true",
                TextTemplate.Of(new TextPart("true")),
                TextTemplate.Of(new TextPart("false"))))));

    /// <summary>Every generator this language declares, in the order a menu should list them.</summary>
    public static IReadOnlyList<TextGenerator> All { get; } = [Config];
}
