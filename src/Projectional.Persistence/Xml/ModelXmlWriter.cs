using Projectional.Core;

using System.Xml;

namespace Projectional.Persistence;

/// <summary>
/// Writes a model as the element tree the format prescribes. Everything that decides the bytes
/// lives here — slot order, attribute order, indentation, line endings — so that the data
/// structures never get a say in how a file looks.
/// </summary>
/// <remarks>
/// The order is the declaration order of the concept's slots, computed freshly on every save
/// rather than remembered from the load, which is what makes save → load → save identical even
/// after the language has gained slots.
/// </remarks>
internal static class ModelXmlWriter
{
    /// <summary>
    /// The one setting that makes the output stable: two spaces of indent, <c>\n</c> between
    /// lines, and characters inside a value kept as characters — an entity where a line break
    /// could not survive intact.
    /// </summary>
    internal static XmlWriterSettings CreateSettings() => new()
    {
        Indent = true,
        IndentChars = "  ",
        NewLineChars = "\n",
        NewLineHandling = NewLineHandling.Entitize,
        Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        ConformanceLevel = ConformanceLevel.Document,
        CloseOutput = false,
    };

    internal static void Write(Model model, XmlWriter writer)
    {
        writer.WriteStartDocument();
        writer.WriteStartElement(ModelXmlNames.Model);
        writer.WriteAttributeString(
            ModelXmlNames.FormatVersion,
            ModelXmlNames.SupportedFormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteAttributeString(ModelXmlNames.Id, model.Id.ToString());

        if (model.Name is { } modelName)
        {
            writer.WriteAttributeString(ModelXmlNames.Name, modelName);
        }

        writer.WriteStartElement(ModelXmlNames.Nodes);
        foreach (var root in model.Roots)
        {
            WriteNode(writer, root);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteNode(XmlWriter writer, Node node)
    {
        writer.WriteStartElement(ModelXmlNames.Node);
        writer.WriteAttributeString(ModelXmlNames.Id, node.Id.ToString());
        writer.WriteAttributeString(ModelXmlNames.Concept, node.Concept.Key);

        // The name is duplicated on purpose: it is what makes a file greppable and a reference
        // readable without chasing an id. The property below stays authoritative.
        if (node.Name is { } name)
        {
            writer.WriteAttributeString(ModelXmlNames.Name, name);
        }

        WriteProperties(writer, node);
        WriteReferences(writer, node);
        WriteChildren(writer, node);

        writer.WriteEndElement();
    }

    private static void WriteProperties(XmlWriter writer, Node node)
    {
        if (!HasProperties(node))
        {
            return;
        }

        writer.WriteStartElement(ModelXmlNames.Properties);
        foreach (var slot in node.Concept.Slots)
        {
            if (slot is PropertySlot property && node.GetOrNull(property.Name) is { } value)
            {
                writer.WriteStartElement(property.Name);
                writer.WriteString(value);
                writer.WriteEndElement();
            }
        }

        writer.WriteEndElement();
    }

    private static void WriteReferences(XmlWriter writer, Node node)
    {
        foreach (var slot in node.Concept.Slots)
        {
            if (slot is ReferenceSlot reference && node.GetReference(reference.Name) is { } value)
            {
                writer.WriteStartElement(ModelXmlNames.References);
                writer.WriteAttributeString(ModelXmlNames.Role, reference.Name);
                writer.WriteAttributeString(ModelXmlNames.Target, value.Target.ToString());

                // Kept even when the target resolves, because it is what a reader falls back to
                // once the id is gone.
                if (value.ResolveInfo is { } resolveInfo)
                {
                    writer.WriteAttributeString(ModelXmlNames.ResolveInfo, resolveInfo);
                }

                writer.WriteEndElement();
            }
        }
    }

    private static void WriteChildren(XmlWriter writer, Node node)
    {
        foreach (var slot in node.Concept.Slots)
        {
            if (slot is not ChildSlot child)
            {
                continue;
            }

            var children = node.GetChildren(child.Name);
            if (children.Count == 0)
            {
                continue;
            }

            writer.WriteStartElement(ModelXmlNames.Children);
            writer.WriteAttributeString(ModelXmlNames.Role, child.Name);
            foreach (var each in children)
            {
                WriteNode(writer, each);
            }

            writer.WriteEndElement();
        }
    }

    private static bool HasProperties(Node node)
    {
        foreach (var slot in node.Concept.Slots)
        {
            if (slot is PropertySlot property && node.GetOrNull(property.Name) is not null)
            {
                return true;
            }
        }

        return false;
    }
}
