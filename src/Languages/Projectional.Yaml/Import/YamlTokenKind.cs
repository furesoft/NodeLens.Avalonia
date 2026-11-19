namespace Projectional.Yaml;

/// <summary>The kinds of token a YAML file is reduced to here.</summary>
/// <remarks>
/// The tokenizer's own kinds are thirty-odd because it also models every punctuation mark a flow collection can hold.
/// This is the subset an assembler needs: where a block or flow collection starts and ends, the two markers of a pair,
/// the dash of a sequence item, and the three leaves. Everything else - anchors, tags, directives, the stream markers - is
/// folded into <see cref="Raw"/> or ignored, because a token this language does not model must not become a node it
/// pretends to understand.
/// </remarks>
internal enum YamlTokenKind
{
    /// <summary>A block mapping begins.</summary>
    MappingStart,

    /// <summary>A block sequence begins.</summary>
    SequenceStart,

    /// <summary>A flow mapping begins.</summary>
    FlowMappingStart,

    /// <summary>A flow sequence begins.</summary>
    FlowSequenceStart,

    /// <summary>A block mapping or sequence ends, by indentation.</summary>
    BlockEnd,

    /// <summary>A flow mapping ends.</summary>
    FlowMappingEnd,

    /// <summary>A flow sequence ends.</summary>
    FlowSequenceEnd,

    /// <summary>A key begins, in a block or a flow mapping.</summary>
    Key,

    /// <summary>The colon of a pair.</summary>
    Value,

    /// <summary>One entry of a flow collection.</summary>
    FlowEntry,

    /// <summary>The dash of a block sequence item.</summary>
    Entry,

    /// <summary>A scalar, in whatever spelling the file used.</summary>
    Scalar,

    /// <summary>A reference to an anchor.</summary>
    Alias,

    /// <summary>An anchor, which is text in front of a node.</summary>
    Anchor,

    /// <summary>A tag, which is text in front of a node.</summary>
    Tag,

    /// <summary>A note.</summary>
    Comment,

    /// <summary>A directive, a document marker or the end of the stream.</summary>
    Other,
}
