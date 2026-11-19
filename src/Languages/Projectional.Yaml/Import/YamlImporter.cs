using Projectional.Core;
using Projectional.Language;
using YamlDotNet.Core;

namespace Projectional.Yaml;

/// <summary>Turns YAML text into a document of this language, once.</summary>
/// <remarks>
/// <para>
/// <b>The tokenizer is the package's, the tree is ours.</b> <see cref="YamlDotNet"/> resolves indentation into block
/// tokens, which is the part of YAML that no hand-written reader gets right; assembling those tokens into concepts is the
/// mapping this reference language exists to prove. The scanner is asked for comments too, because a note the importer
/// dropped would be a note the next save deleted.
/// </para>
/// <para>
/// <b>A scalar keeps the slice of the file it came from</b>, quotes, escapes, anchor and block body included - so a
/// template emits it verbatim and the round trip needs no escaping part, exactly as the four other reference languages
/// settle spelling on the way in.
/// </para>
/// <para>
/// <b>What this language does not model is kept as raw text and reported.</b> A directive, a document marker, a complex
/// key and an anchored collection survive as <see cref="YamlConceptKeys.Raw"/> with a warning, which is tier 2: the file
/// is not rewritten, and the construct is not pretended to be understood.
/// </para>
/// <para>
/// <b>Line breaks are normalized to LF before the tokens are read.</b> A block scalar's body is a slice of the text, so
/// its line endings would otherwise travel into the model and out into a file that no longer matches the corpus.
/// </para>
/// </remarks>
public sealed class YamlImporter : ITextImporter
{
    /// <summary>The diagnostic for text that is not a YAML document.</summary>
    public const string UnexpectedToken = "yaml.unexpected-token";

    /// <summary>The diagnostic for a construct kept as raw text.</summary>
    public const string KeptAsRaw = "yaml.kept-as-raw";

    /// <summary>The diagnostic for a note that had to move out of a flow collection.</summary>
    public const string CommentMoved = "yaml.comment-moved";

    /// <summary>The format name a host's file dialog filters on.</summary>
    public string Format => "yaml";

    /// <summary>Whether the text starts like a YAML document.</summary>
    /// <param name="text">The text to look at.</param>
    /// <remarks>
    /// A cheap shape question on the first meaningful line: a pair (<c>key: value</c>), a dash, a document marker or a
    /// flow collection. A colon that comes before any equals sign is what tells it from TOML and HOCON, and the answer
    /// that matters is <see cref="Import"/>'s.
    /// </remarks>
    public bool CanImport(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            if (trimmed is "---" or "..." || trimmed[0] == '-' || trimmed[0] == '{' || trimmed[0] == '[')
            {
                return true;
            }

            var colon = trimmed.IndexOf(':', StringComparison.Ordinal);
            var equals = trimmed.IndexOf('=', StringComparison.Ordinal);

            return colon > 0 && (equals < 0 || colon < equals);
        }

        return false;
    }

    /// <inheritdoc/>
    public ImportResult Import(Model model, string text)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return ImportResult.Refused(new ImportDiagnostic(
                UnexpectedToken,
                "The text holds no YAML document.",
                1,
                1));
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var positions = new YamlTextPositions(normalized);

        try
        {
            var reader = YamlTokens.Of(normalized);

            // One transaction for the whole text: a throw below disposes it and the model is exactly as it was.
            using var transaction = model.BeginTransaction();

            var document = model.NewNode(YamlConceptKeys.Document);
            var diagnostics = new List<ImportDiagnostic>();

            ReadStream(model, document, reader, diagnostics, positions);

            transaction.Complete();
            return ImportResult.Imported([document], diagnostics);
        }
        catch (YamlSyntaxException exception)
        {
            return ImportResult.Refused(Refusal(UnexpectedToken, exception.Message, exception.Offset, positions));
        }
        catch (YamlException exception)
        {
            return ImportResult.Refused(Refusal(
                UnexpectedToken,
                Sentence(exception),
                (int)exception.Start.Index,
                positions));
        }
        catch (FormatException exception)
        {
            return ImportResult.Refused(Refusal(UnexpectedToken, exception.Message, 0, positions));
        }
    }

    private static ImportDiagnostic Refusal(string code, string message, int offset, YamlTextPositions positions) =>
        new(code, message, positions.LineOf(offset), positions.ColumnOf(offset));

    /// <summary>Reads every line of the stream into the document, in the order the file has them.</summary>
    private static void ReadStream(
        Model model,
        Node document,
        YamlTokens reader,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions)
    {
        void Attach(YamlToken comment) =>
            NewComment(model, document, YamlLanguage.EntriesSlot, comment, reader);

        while (!reader.AtEnd)
        {
            var token = reader.Current;

            switch (token.Kind)
            {
                case YamlTokenKind.Comment:
                    Attach(token);
                    reader.Advance();
                    break;

                case YamlTokenKind.Other:
                    KeepMarker(model, reader, document, diagnostics, positions, token);
                    break;

                default:
                    ReadNode(model, document, YamlLanguage.EntriesSlot, reader, Attach, diagnostics, positions);
                    break;
            }
        }
    }

    /// <summary>A directive or a document marker, kept as raw text when it is content.</summary>
    private static void KeepMarker(
        Model model,
        YamlTokens reader,
        Node document,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions,
        YamlToken token)
    {
        // A directive carries no span in the tokenizer, so its spelling is rebuilt from the token itself; the two document
        // markers do have one, and slicing keeps whatever the file wrote.
        var spelled = token.Text.Length > 0 ? token.Text : reader.Slice(token);

        if (spelled.Length > 0 && spelled.StartsWith('%'))
        {
            // A directive is content the model does not understand; a `---` or `...` marker is one it keeps without a
            // diagnostic, because nothing about it is lost.
            var raw = model.NewChild(document, YamlLanguage.EntriesSlot, YamlConceptKeys.Raw);
            raw.Set(YamlLanguage.TextSlot, spelled);

            diagnostics.Add(Warning(KeptAsRaw, "This construct is kept as raw text.", token, positions));
        }
        else if (spelled is "---" or "...")
        {
            var marker = model.NewChild(document, YamlLanguage.EntriesSlot, YamlConceptKeys.Raw);
            marker.Set(YamlLanguage.TextSlot, spelled);
        }

        reader.Advance();
    }

    /// <summary>The node the reader stands on, hung into <paramref name="parent"/> when there is one.</summary>
    private static Node ReadNode(
        Model model,
        Node? parent,
        string? slot,
        YamlTokens reader,
        Action<YamlToken> attachComment,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions)
    {
        var token = reader.Current;

        switch (token.Kind)
        {
            case YamlTokenKind.Scalar:
                reader.Advance();
                var scalar = Create(model, parent, slot, YamlConceptKeys.Scalar);
                scalar.Set(YamlLanguage.TextSlot, reader.Slice(token));
                return scalar;

            case YamlTokenKind.Alias:
                reader.Advance();
                var alias = Create(model, parent, slot, YamlConceptKeys.Alias);
                alias.Set(YamlLanguage.NameSlot, token.Text);
                return alias;

            case YamlTokenKind.Anchor or YamlTokenKind.Tag:
                return ReadAnchored(model, parent, slot, reader, attachComment, diagnostics, positions);

            case YamlTokenKind.MappingStart:
                return ReadBlockMapping(model, parent, slot, reader, diagnostics, positions);

            case YamlTokenKind.SequenceStart:
                return ReadBlockSequence(model, parent, slot, reader, diagnostics, positions);

            case YamlTokenKind.FlowMappingStart:
                return ReadFlowMapping(model, parent, slot, reader, attachComment, diagnostics, positions);

            case YamlTokenKind.FlowSequenceStart:
                return ReadFlowSequence(model, parent, slot, reader, attachComment, diagnostics, positions);

            default:
                throw new YamlSyntaxException($"'{token.Kind}' cannot stand where a node belongs.", token.Start);
        }
    }

    /// <summary>An anchor or a tag: part of a scalar's spelling, and a raw node in front of a collection.</summary>
    private static Node ReadAnchored(
        Model model,
        Node? parent,
        string? slot,
        YamlTokens reader,
        Action<YamlToken> attachComment,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions)
    {
        var start = reader.Current.Start;

        while (!reader.AtEnd && reader.Current.Kind is YamlTokenKind.Anchor or YamlTokenKind.Tag)
        {
            reader.Advance();
        }

        if (reader.Current.Kind is YamlTokenKind.MappingStart or YamlTokenKind.SequenceStart
            or YamlTokenKind.FlowMappingStart or YamlTokenKind.FlowSequenceStart)
        {
            // A template cannot ask what kind of node a child is, so an anchored collection has no spelling slot to keep
            // its prefix in: the whole node stays raw, which is the honest tier 2 rather than a lost anchor.
            var end = SkipCollection(reader);

            var raw = Create(model, parent, slot, YamlConceptKeys.Raw);
            raw.Set(YamlLanguage.TextSlot, reader.Spelling(start, end));

            diagnostics.Add(Warning(
                KeptAsRaw,
                "The anchor or tag in front of a collection is kept as raw text.",
                new YamlToken(YamlTokenKind.Other, string.Empty, start, end),
                positions));

            return raw;
        }

        var node = ReadNode(model, parent, slot, reader, attachComment, diagnostics, positions);

        if (node.Concept.Key == YamlConceptKeys.Scalar)
        {
            // The anchor or tag is one run of text with the scalar, so the slice from the prefix is its spelling.
            node.Set(YamlLanguage.TextSlot, reader.Spelling(start, reader.LastEnd));
        }

        return node;
    }

    /// <summary>Steps over a whole collection, and answers where it ends.</summary>
    private static int SkipCollection(YamlTokens reader)
    {
        var depth = 0;

        do
        {
            switch (reader.Current.Kind)
            {
                case YamlTokenKind.MappingStart or YamlTokenKind.SequenceStart
                    or YamlTokenKind.FlowMappingStart or YamlTokenKind.FlowSequenceStart:
                    depth++;
                    break;

                case YamlTokenKind.BlockEnd or YamlTokenKind.FlowMappingEnd or YamlTokenKind.FlowSequenceEnd:
                    depth--;
                    break;
            }

            reader.Advance();
        }
        while (!reader.AtEnd && depth > 0);

        return reader.LastEnd;
    }

    /// <summary>A block mapping: one member per line, in the order the file has them.</summary>
    private static Node ReadBlockMapping(
        Model model,
        Node? parent,
        string? slot,
        YamlTokens reader,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions)
    {
        var mapping = Create(model, parent, slot, YamlConceptKeys.Mapping);
        reader.Advance();

        void Attach(YamlToken comment) =>
            NewComment(model, mapping, YamlLanguage.MembersSlot, comment, reader);

        DrainHeld(model, mapping, YamlLanguage.MembersSlot, reader);

        while (!reader.AtEnd)
        {
            switch (reader.Current.Kind)
            {
                case YamlTokenKind.BlockEnd:
                    reader.Advance();
                    return mapping;

                case YamlTokenKind.Comment:
                    WarnIfWrittenInline(reader.Current, reader, diagnostics, positions);
                    Attach(reader.Current);
                    reader.Advance();
                    break;

                case YamlTokenKind.Key:
                    var keyStart = reader.Current.Start;
                    reader.Advance();

                    if (reader.Current.Kind is YamlTokenKind.MappingStart or YamlTokenKind.SequenceStart
                        or YamlTokenKind.FlowMappingStart or YamlTokenKind.FlowSequenceStart)
                    {
                        KeepComplexKey(model, mapping, reader, diagnostics, positions, keyStart);
                        break;
                    }

                    ReadPair(model, mapping, reader, Attach, diagnostics, positions);
                    break;

                default:
                    throw new YamlSyntaxException($"'{reader.Current.Kind}' cannot stand in a block mapping.", reader.Current.Start);
            }
        }

        return mapping;
    }

    /// <summary>One line of a block mapping, with the placement of its value.</summary>
    private static void ReadPair(
        Model model,
        Node mapping,
        YamlTokens reader,
        Action<YamlToken> attachComment,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions)
    {
        var pair = model.NewChild(mapping, YamlLanguage.MembersSlot, YamlConceptKeys.Pair);

        if (reader.AtNodeStart())
        {
            ReadNode(model, pair, YamlLanguage.KeySlot, reader, attachComment, diagnostics, positions);
        }

        if (reader.Current.Kind != YamlTokenKind.Value)
        {
            throw new YamlSyntaxException("A key has to be followed by a colon.", reader.Current.Start);
        }

        reader.Advance();

        QueueNotes(reader, diagnostics, positions);

        if (reader.AtNodeStart())
        {
            ReadNode(model, pair, YamlLanguage.ValueSlot, reader, attachComment, diagnostics, positions);
        }

        // A note the value's own list did not take belongs after the pair, in the list that asked.
        foreach (var note in reader.TakeHeld())
        {
            attachComment(note);
        }

        if (pair.GetChild(YamlLanguage.ValueSlot) is not null)
        {
            // A block collection cannot be written on its key's line, and no template can ask what kind its child is - so
            // the file's placement is what the model keeps.
            pair.Set(
                YamlLanguage.LayoutSlot,
                pair.GetChild(YamlLanguage.ValueSlot)!.Concept.Key is YamlConceptKeys.Mapping or YamlConceptKeys.Sequence
                    ? YamlLanguage.Nested
                    : YamlLanguage.Inline);
        }
    }

    /// <summary>A pair whose key is not a scalar, kept as one raw line.</summary>
    /// <remarks>
    /// A complex key (<c>? [a, b]</c>) has no implicit spelling in a block mapping, and no template could express one - so
    /// the line stays raw rather than being printed as a key a YAML reader would refuse. The key and the value are stepped
    /// over rather than built, because a node that is created and deleted would be a second undo step and a second way to
    /// build a pair.
    /// </remarks>
    private static void KeepComplexKey(
        Model model,
        Node mapping,
        YamlTokens reader,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions,
        int start)
    {
        SkipCollection(reader);

        if (reader.Current.Kind == YamlTokenKind.Value)
        {
            reader.Advance();

            if (reader.AtNodeStart())
            {
                SkipNode(reader);
            }
        }

        var end = reader.LastEnd;

        var raw = model.NewChild(mapping, YamlLanguage.MembersSlot, YamlConceptKeys.Raw);
        raw.Set(YamlLanguage.TextSlot, reader.Spelling(start, end));

        diagnostics.Add(Warning(
            KeptAsRaw,
            "A key that is not a scalar is kept as raw text.",
            new YamlToken(YamlTokenKind.Other, string.Empty, start, end),
            positions));
    }

    /// <summary>Steps over one node, whatever kind it is.</summary>
    private static void SkipNode(YamlTokens reader)
    {
        while (!reader.AtEnd && reader.Current.Kind is YamlTokenKind.Anchor or YamlTokenKind.Tag)
        {
            reader.Advance();
        }

        if (reader.Current.Kind is YamlTokenKind.MappingStart or YamlTokenKind.SequenceStart
            or YamlTokenKind.FlowMappingStart or YamlTokenKind.FlowSequenceStart)
        {
            SkipCollection(reader);
            return;
        }

        if (!reader.AtEnd)
        {
            reader.Advance();
        }
    }

    /// <summary>A block sequence: one item per line, in the order the file has them.</summary>
    private static Node ReadBlockSequence(
        Model model,
        Node? parent,
        string? slot,
        YamlTokens reader,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions)
    {
        var sequence = Create(model, parent, slot, YamlConceptKeys.Sequence);
        reader.Advance();

        void Attach(YamlToken comment) =>
            NewComment(model, sequence, YamlLanguage.ItemsSlot, comment, reader);

        DrainHeld(model, sequence, YamlLanguage.ItemsSlot, reader);

        while (!reader.AtEnd)
        {
            switch (reader.Current.Kind)
            {
                case YamlTokenKind.BlockEnd:
                    reader.Advance();
                    return sequence;

                case YamlTokenKind.Comment:
                    WarnIfWrittenInline(reader.Current, reader, diagnostics, positions);
                    Attach(reader.Current);
                    reader.Advance();
                    break;

                case YamlTokenKind.Entry:
                    reader.Advance();
                    var item = model.NewChild(sequence, YamlLanguage.ItemsSlot, YamlConceptKeys.SequenceItem);

                    QueueNotes(reader, diagnostics, positions);

                    if (reader.AtNodeStart())
                    {
                        ReadNode(model, item, YamlLanguage.ValueSlot, reader, Attach, diagnostics, positions);
                    }

                    foreach (var note in reader.TakeHeld())
                    {
                        NewComment(model, sequence, YamlLanguage.ItemsSlot, note, reader);
                    }

                    break;

                default:
                    throw new YamlSyntaxException($"'{reader.Current.Kind}' cannot stand in a block sequence.", reader.Current.Start);
            }
        }

        return sequence;
    }

    /// <summary>A flow mapping: its members between braces, separated by commas.</summary>
    private static Node ReadFlowMapping(
        Model model,
        Node? parent,
        string? slot,
        YamlTokens reader,
        Action<YamlToken> attachComment,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions)
    {
        var mapping = Create(model, parent, slot, YamlConceptKeys.FlowMapping);
        reader.Advance();

        while (!reader.AtEnd)
        {
            switch (reader.Current.Kind)
            {
                case YamlTokenKind.FlowMappingEnd:
                    reader.Advance();
                    return mapping;

                case YamlTokenKind.FlowEntry:
                    reader.Advance();
                    break;

                case YamlTokenKind.Comment:
                    // A flow collection has no member list for a note, so it moves out to the nearest block list.
                    attachComment(reader.Current);
                    diagnostics.Add(Warning(
                        CommentMoved,
                        "The comment stood inside a flow collection; it is kept in the surrounding block list.",
                        reader.Current,
                        positions));
                    reader.Advance();
                    break;

                case YamlTokenKind.Key:
                    reader.Advance();
                    var pair = model.NewChild(mapping, YamlLanguage.MembersSlot, YamlConceptKeys.FlowPair);
                    ReadNode(model, pair, YamlLanguage.KeySlot, reader, attachComment, diagnostics, positions);

                    if (reader.Current.Kind == YamlTokenKind.Value)
                    {
                        reader.Advance();

                        if (reader.AtNodeStart())
                        {
                            ReadNode(model, pair, YamlLanguage.ValueSlot, reader, attachComment, diagnostics, positions);
                        }
                    }

                    break;

                default:
                    throw new YamlSyntaxException($"'{reader.Current.Kind}' cannot stand in a flow mapping.", reader.Current.Start);
            }
        }

        return mapping;
    }

    /// <summary>A flow sequence: its items between brackets, separated by commas.</summary>
    private static Node ReadFlowSequence(
        Model model,
        Node? parent,
        string? slot,
        YamlTokens reader,
        Action<YamlToken> attachComment,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions)
    {
        var sequence = Create(model, parent, slot, YamlConceptKeys.FlowSequence);
        reader.Advance();

        while (!reader.AtEnd)
        {
            switch (reader.Current.Kind)
            {
                case YamlTokenKind.FlowSequenceEnd:
                    reader.Advance();
                    return sequence;

                case YamlTokenKind.FlowEntry:
                    reader.Advance();
                    break;

                case YamlTokenKind.Comment:
                    attachComment(reader.Current);
                    diagnostics.Add(Warning(
                        CommentMoved,
                        "The comment stood inside a flow collection; it is kept in the surrounding block list.",
                        reader.Current,
                        positions));
                    reader.Advance();
                    break;

                default:
                    ReadNode(model, sequence, YamlLanguage.ItemsSlot, reader, attachComment, diagnostics, positions);
                    break;
            }
        }

        return sequence;
    }

    /// <summary>Holds the notes that stand between a colon or a dash and the node it introduces.</summary>
    private static void QueueNotes(
        YamlTokens reader,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions)
    {
        while (!reader.AtEnd && reader.Current.Kind == YamlTokenKind.Comment)
        {
            WarnIfWrittenInline(reader.Current, reader, diagnostics, positions);
            reader.Hold(reader.Current);
            reader.Advance();
        }
    }

    /// <summary>Makes the notes held for a list that is starting its first members.</summary>
    private static void DrainHeld(Model model, Node parent, string slot, YamlTokens reader)
    {
        foreach (var note in reader.TakeHeld())
        {
            NewComment(model, parent, slot, note, reader);
        }
    }

    /// <summary>Reports a note that was written at the end of a line of content, which the model keeps on its own.</summary>    /// <remarks>
    /// A block list holds a note as a member, so a note beside a value is written on the next line when the file is
    /// generated. Nothing is lost, and the placement is not a fact the model could keep - a template cannot draw a note
    /// after the value it comments on - so it is reported rather than pretended.
    /// </remarks>
    private static void WarnIfWrittenInline(
        YamlToken comment,
        YamlTokens reader,
        List<ImportDiagnostic> diagnostics,
        YamlTextPositions positions)
    {
        if (positions.LineOf(reader.LastEnd) != positions.LineOf(comment.Start))
        {
            return;
        }

        diagnostics.Add(Warning(
            CommentMoved,
            "The comment was written beside a value; it is kept on a line of its own.",
            comment,
            positions));
    }

    /// <summary>A note node, with the hash the notation draws taken off its text.</summary>
    private static Node NewComment(Model model, Node? parent, string? slot, YamlToken comment, YamlTokens reader)
    {
        var spelled = reader.Slice(comment);
        var node = Create(model, parent, slot, YamlConceptKeys.Comment);
        node.Set(YamlLanguage.TextSlot, spelled.StartsWith('#') ? spelled[1..] : spelled);

        return node;
    }

    /// <summary>The node of <paramref name="conceptKey"/>, as a root or inside its parent.</summary>
    private static Node Create(Model model, Node? parent, string? slot, string conceptKey)
    {
        if (parent is null)
        {
            return model.NewNode(conceptKey);
        }

        ArgumentException.ThrowIfNullOrEmpty(slot);
        return model.NewChild(parent, slot, conceptKey);
    }

    private static ImportDiagnostic Warning(
        string code,
        string message,
        YamlToken token,
        YamlTextPositions positions) =>
        new(
            code,
            message,
            positions.LineOf(token.Start),
            positions.ColumnOf(token.Start),
            Length: Math.Max(0, token.End - token.Start),
            Severity: ImportSeverity.Warning);

    /// <summary>The tokenizer's message without the position it appends.</summary>
    private static string Sentence(YamlException exception)
    {
        var note = exception.Message.IndexOf(" at line", StringComparison.Ordinal);

        return note < 0 ? exception.Message : exception.Message[..note];
    }
}
