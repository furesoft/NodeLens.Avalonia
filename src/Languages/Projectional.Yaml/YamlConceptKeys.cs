using System.Diagnostics.CodeAnalysis;

namespace Projectional.Yaml;

/// <summary>The keys of the YAML language: one concept per thing a YAML file is made of.</summary>
/// <remarks>
/// Four unions, and each exists because YAML lets the thing stand somewhere the others cannot: a node can stand in any
/// slot that holds a node, an entry can be a sequence item or a line of the document, a member can be a mapping's line,
/// and a flow value is what may stand inside a flow collection - YAML forbids a block collection there, and carrying that
/// in the slot type makes the illegal state unrepresentable rather than merely discouraged.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "The members are the format's own words - a scalar, a mapping, a sequence - so the constant that spells 'Yaml.Scalar' reads as the concept key it is. Renaming them around a type name would make the language's vocabulary less recognizable, not more.")]
public static class YamlConceptKeys
{
    /// <summary>Anything that is a YAML node: a scalar, an alias, a block or a flow collection.</summary>
    public const string Node = "Yaml.Node";

    /// <summary>Anything that can be a sequence item or a line of a document.</summary>
    public const string Entry = "Yaml.Entry";

    /// <summary>A document: the notes, markers and node a file holds, in the order it has them.</summary>
    public const string Document = "Yaml.Document";

    /// <summary>Anything that can be one line of a block mapping.</summary>
    public const string Member = "Yaml.Member";

    /// <summary>Anything that may stand inside a flow collection.</summary>
    public const string FlowValue = "Yaml.FlowValue";

    /// <summary>A scalar, in whatever spelling the file used.</summary>
    public const string Scalar = "Yaml.Scalar";

    /// <summary>A reference to an anchor: <c>*name</c>.</summary>
    public const string Alias = "Yaml.Alias";

    /// <summary>A block mapping: one <c>key: value</c> per line.</summary>
    public const string Mapping = "Yaml.Mapping";

    /// <summary>A block sequence: one <c>- value</c> per line.</summary>
    public const string Sequence = "Yaml.Sequence";

    /// <summary>One item of a block sequence, which is the dash and the node it introduces.</summary>
    public const string SequenceItem = "Yaml.SequenceItem";

    /// <summary>A mapping written between braces.</summary>
    public const string FlowMapping = "Yaml.FlowMapping";

    /// <summary>A sequence written between brackets.</summary>
    public const string FlowSequence = "Yaml.FlowSequence";

    /// <summary>One line of a block mapping: a key, and the node it holds.</summary>
    public const string Pair = "Yaml.Pair";

    /// <summary>One member of a flow mapping.</summary>
    public const string FlowPair = "Yaml.FlowPair";

    /// <summary>A note, kept where the file had it.</summary>
    public const string Comment = "Yaml.Comment";

    /// <summary>A construct this language does not model, kept exactly as written.</summary>
    public const string Raw = "Yaml.Raw";
}
