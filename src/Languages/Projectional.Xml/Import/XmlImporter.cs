using System.Xml;
using System.Xml.Linq;
using Projectional.Core;
using Projectional.Language;

namespace Projectional.Xml;

/// <summary>Turns XML text into a document of this language, once.</summary>
/// <remarks>
/// <para>
/// <b>The parser is the platform's</b> - <see cref="XDocument"/> over an <see cref="XmlReader"/> - so the grammar,
/// entity handling and line endings are not re-derived here; this class owns the mapping. What it does own is the
/// security posture, and that is not negotiable: a DTD is refused, because resolving one is exactly how an importer
/// turns into a file reader and gives every host an XXE. The reader is configured with
/// <see cref="DtdProcessing.Prohibit"/> and no <see cref="XmlReaderSettings.XmlResolver"/>, and the refusal is
/// classified by looking for the declaration in the text rather than by reading a localised message.
/// </para>
/// <para>
/// <b>Whitespace is content.</b> The document is loaded with <see cref="LoadOptions.PreserveWhitespace"/> and every text
/// node is kept, including a run that holds nothing but spaces - in XML, whitespace between markup is what a
/// mixed-content reader sees, so dropping it would rewrite the file.
/// </para>
/// <para>
/// <b>A document is its top-level nodes.</b> XML allows comments and processing instructions before and after the root
/// element, and they are content, so they become roots of their own in document order. Only the XML declaration is not
/// modelled: it is a statement about the bytes a host read rather than about the tree, and the host that writes the
/// generated text states it again. That is the one thing this importer drops, and it is dropped on purpose.
/// </para>
/// <para>
/// <b>Text and attribute values are stored escaped</b> (see <see cref="XmlEscaping"/>): the parser hands back the
/// decoded value, and the model holds the canonical spelling the generator must emit.
/// </para>
/// </remarks>
public sealed class XmlImporter : ITextImporter
{
    /// <summary>The diagnostic for text that is not a well-formed XML document.</summary>
    public const string UnexpectedToken = "xml.unexpected-token";

    /// <summary>The diagnostic for a document that declares a DTD.</summary>
    public const string ExternalEntityRefused = "xml.external-entity-refused";

    /// <summary>The format name a host's file dialog filters on.</summary>
    public string Format => "xml";

    /// <summary>Whether the text starts like an XML document.</summary>
    /// <param name="text">The text to look at.</param>
    /// <remarks>A cheap test on the first non-blank character; the answer that matters is <see cref="Import"/>'s.</remarks>
    public bool CanImport(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return character == '<';
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
                "The text holds no XML document.",
                1,
                1));
        }

        var settings = new XmlReaderSettings
        {
            // Not "ignore" and not "parse": an ignored DTD loses data silently, and a parsed one resolves what it is
            // told to resolve. Prohibition is the only answer that keeps the importer pure.
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = false,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
        };

        try
        {
            XDocument document;
            using (var reader = XmlReader.Create(new StringReader(text), settings))
            {
                document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }

            var diagnostics = new List<ImportDiagnostic>();

            // One transaction for the whole document, as every importer promises: a throw while building disposes it and
            // the model is exactly as it was.
            using var transaction = model.BeginTransaction();

            var roots = new List<Node>();
            foreach (var node in document.Nodes())
            {
                ReadTopLevel(model, node, roots);
            }

            if (roots.Count == 0)
            {
                return ImportResult.Refused(new ImportDiagnostic(
                    UnexpectedToken,
                    "The text holds no XML document.",
                    1,
                    1));
            }

            transaction.Complete();
            return ImportResult.Imported(roots, diagnostics);
        }
        catch (XmlException exception)
        {
            return ImportResult.Refused(Refusal(exception, text));
        }
    }

    /// <summary>Adds one of the document's own nodes as a root, in the order the file has them.</summary>
    private static void ReadTopLevel(Model model, XNode node, List<Node> roots)
    {
        switch (node)
        {
            case XElement element:
                roots.Add(ReadElement(model, parent: null, slot: null, element));
                break;

            // Tested before XText, because XCData derives from it and CDATA is the one construct this language keeps as
            // raw text rather than as character data.
            case XCData cdata:
                roots.Add(WithText(model, null, null, XmlConceptKeys.Raw, RawOf(cdata)));
                break;

            case XText text:
                roots.Add(WithText(model, null, null, XmlConceptKeys.Text, XmlEscaping.Text(text.Value)));
                break;

            case XComment comment:
                roots.Add(WithText(model, null, null, XmlConceptKeys.Comment, comment.Value));
                break;

            case XProcessingInstruction instruction:
                roots.Add(WithText(model, null, null, XmlConceptKeys.Raw, RawOf(instruction)));
                break;
        }
    }

    private static Node ReadElement(Model model, Node? parent, string? slot, XElement element)
    {
        var node = Create(model, parent, slot, XmlConceptKeys.Element);
        node.Set(XmlLanguage.NameSlot, SpellingOf(element.Name, element));

        foreach (var attribute in element.Attributes())
        {
            var written = model.NewChild(node, XmlLanguage.AttributesSlot, XmlConceptKeys.Attribute);
            written.Set(XmlLanguage.NameSlot, SpellingOf(attribute.Name, element));
            written.Set(XmlLanguage.ValueSlot, XmlEscaping.Attribute(attribute.Value));
        }

        foreach (var child in element.Nodes())
        {
            AddChild(model, node, child);
        }

        return node;
    }

    private static void AddChild(Model model, Node parent, XNode child)
    {
        switch (child)
        {
            case XElement element:
                ReadElement(model, parent, XmlLanguage.ChildrenSlot, element);
                break;

            case XCData cdata:
                WithText(model, parent, XmlLanguage.ChildrenSlot, XmlConceptKeys.Raw, RawOf(cdata));
                break;

            case XText text:
                WithText(model, parent, XmlLanguage.ChildrenSlot, XmlConceptKeys.Text, XmlEscaping.Text(text.Value));
                break;

            case XComment comment:
                WithText(model, parent, XmlLanguage.ChildrenSlot, XmlConceptKeys.Comment, comment.Value);
                break;

            case XProcessingInstruction instruction:
                WithText(model, parent, XmlLanguage.ChildrenSlot, XmlConceptKeys.Raw, RawOf(instruction));
                break;
        }
    }

    /// <summary>The name as the file spells it, prefix included.</summary>
    /// <remarks>
    /// LINQ to XML resolves a prefix to a URI and forgets which prefix it was, so the spelling is reconstructed from the
    /// declarations in scope. A namespace declaration is the exception that has to be spelled from its own shape:
    /// <c>xmlns</c> for the default namespace and <c>xmlns:prefix</c> for the other one.
    /// </remarks>
    private static string SpellingOf(XName name, XElement owner)
    {
        if (name.Namespace == XNamespace.Xmlns)
        {
            return name.LocalName == "xmlns" ? "xmlns" : $"xmlns:{name.LocalName}";
        }

        if (name.Namespace == XNamespace.None)
        {
            return name.LocalName;
        }

        var prefix = owner.GetPrefixOfNamespace(name.Namespace);
        return string.IsNullOrEmpty(prefix) ? name.LocalName : $"{prefix}:{name.LocalName}";
    }

    private static string RawOf(XCData cdata) => $"<![CDATA[{cdata.Value}]]>";

    private static string RawOf(XProcessingInstruction instruction) =>
        string.IsNullOrEmpty(instruction.Data)
            ? $"<?{instruction.Target}?>"
            : $"<?{instruction.Target} {instruction.Data}?>";

    private static Node WithText(Model model, Node? parent, string? slot, string conceptKey, string text)
    {
        var node = Create(model, parent, slot, conceptKey);
        node.Set(XmlLanguage.TextSlot, text);
        return node;
    }

    private static Node Create(Model model, Node? parent, string? slot, string conceptKey)
    {
        if (parent is null)
        {
            return model.NewNode(conceptKey);
        }

        ArgumentException.ThrowIfNullOrEmpty(slot);
        return model.NewChild(parent, slot, conceptKey);
    }

    /// <summary>The refusal for text the parser could not read.</summary>
    /// <remarks>
    /// <see cref="XmlException"/> already counts in lines and characters, so its place is used as it stands. A DTD
    /// refusal is recognised by the declaration in the text rather than by the exception's message, which is localised
    /// - and the message is ours in that case, because the platform's says "prohibited" without saying why that is the
    /// right answer.
    /// </remarks>
    private static ImportDiagnostic Refusal(XmlException exception, string text)
    {
        var line = Math.Max(exception.LineNumber, 1);
        var column = Math.Max(exception.LinePosition, 1);

        return text.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            ? new ImportDiagnostic(
                ExternalEntityRefused,
                "The document declares a DTD. An importer that resolves one reads and expands whatever it points at, "
                    + "so a DTD is refused rather than followed.",
                line,
                column)
            : new ImportDiagnostic(UnexpectedToken, exception.Message, line, column);
    }
}
