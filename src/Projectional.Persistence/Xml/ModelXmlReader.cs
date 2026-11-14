using Projectional.Core;

using System.Globalization;
using System.Xml;

namespace Projectional.Persistence;

/// <summary>
/// Rebuilds a model tree from the element tree the format prescribes, keeping the identities it
/// finds instead of minting new ones.
/// </summary>
/// <remarks>
/// Reading is the mirror image of writing and nothing more: it does not reorder anything, does not
/// fill in what a file left out, and does not decide that a model is valid — the language does that
/// while the nodes are being built. A model that cannot be built at all leaves nothing behind; the
/// half-built one is taken out of the project again.
/// </remarks>
internal sealed class ModelXmlReader(Project project, XmlReader reader)
{
    private readonly Project _project = project;
    private readonly XmlReader _reader = reader;
    private Model? _model;

    internal static Model Read(Project project, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(stream);

        var settings = new XmlReaderSettings
        {
            // A model file is data, never a document with a type definition of its own.
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = false,
            ConformanceLevel = ConformanceLevel.Document,
        };

        using var reader = XmlReader.Create(stream, settings);
        return new ModelXmlReader(project, reader).ReadModel();
    }

    private Model ReadModel()
    {
        try
        {
            _reader.MoveToContent();
            if (_reader.NodeType != XmlNodeType.Element || _reader.LocalName != ModelXmlNames.Model)
            {
                throw new ModelFormatException($"A model file must start with a <{ModelXmlNames.Model}> element.");
            }

            EnsureSupportedFormatVersion();

            var idText = _reader.GetAttribute(ModelXmlNames.Id)
                ?? throw new ModelFormatException(
                    $"The <{ModelXmlNames.Model}> element has no '{ModelXmlNames.Id}' attribute.");

            if (!ModelId.TryParse(idText, out var id))
            {
                throw new ModelFormatException($"'{idText}' is not a model id.");
            }

            var model = _model = _project.NewModel(id, _reader.GetAttribute(ModelXmlNames.Name));

            ReadContainer(() =>
            {
                switch (_reader.LocalName)
                {
                    case ModelXmlNames.Nodes:
                        ReadNodes(model);
                        break;
                    default:
                        throw new ModelFormatException(
                            $"Unexpected element <{_reader.LocalName}> in <{ModelXmlNames.Model}>.");
                }
            });

            return model;
        }
        catch (ModelFormatException)
        {
            Discard();
            throw;
        }
        catch (Exception ex) when (ex is XmlException or ModelInvariantException or FormatException)
        {
            Discard();
            throw new ModelFormatException($"The model could not be read: {ex.Message}", ex);
        }
    }

    private void ReadNodes(Model model)
    {
        ReadContainer(() =>
        {
            if (_reader.LocalName != ModelXmlNames.Node)
            {
                throw new ModelFormatException(
                    $"Unexpected element <{_reader.LocalName}> in <{ModelXmlNames.Nodes}>.");
            }

            ReadNode(model, parent: null, role: null);
        });
    }

    private Node ReadNode(Model model, Node? parent, string? role)
    {
        var id = ReadNodeId();
        var conceptKey = _reader.GetAttribute(ModelXmlNames.Concept)
            ?? throw new ModelFormatException($"Node {id} has no '{ModelXmlNames.Concept}' attribute.");

        if (!model.Concepts.TryGet(conceptKey, out var concept))
        {
            throw new ModelFormatException($"Node {id} has concept '{conceptKey}', which the language does not declare.");
        }

        // The identity comes from the file, so a reference read before its target still lands.
        var node = model.CreateDetached(id, concept);
        if (parent is null)
        {
            model.AddRoot(node);
        }
        else
        {
            parent.AddChild(role!, node);
        }

        ReadContainer(() =>
        {
            switch (_reader.LocalName)
            {
                case ModelXmlNames.Properties:
                    ReadProperties(node);
                    break;
                case ModelXmlNames.References:
                    ReadReference(node);
                    break;
                case ModelXmlNames.Children:
                    ReadChildren(node);
                    break;
                default:
                    throw new ModelFormatException($"Unexpected element <{_reader.LocalName}> in node {id}.");
            }
        });

        return node;
    }

    private void ReadProperties(Node node)
    {
        ReadContainer(() =>
        {
            var name = _reader.LocalName;
            if (node.Concept.FindProperty(name) is null)
            {
                throw new ModelFormatException(
                    $"Concept '{node.Concept.Key}' has no property '{name}', but node {node.Id} stores one.");
            }

            string value;
            try
            {
                value = _reader.ReadElementContentAsString();
            }
            catch (XmlException ex)
            {
                throw new ModelFormatException($"Property '{name}' of node {node.Id} is not readable text.", ex);
            }

            try
            {
                node.Set(name, value);
            }
            catch (Exception ex) when (ex is ModelInvariantException or ArgumentException)
            {
                throw new ModelFormatException(
                    $"'{value}' is not a valid value for property '{name}' of node {node.Id}.",
                    ex);
            }
        });
    }

    private void ReadReference(Node node)
    {
        var role = _reader.GetAttribute(ModelXmlNames.Role)
            ?? throw new ModelFormatException(
                $"A <{ModelXmlNames.References}> element in node {node.Id} has no '{ModelXmlNames.Role}' attribute.");

        if (node.Concept.FindReference(role) is null)
        {
            throw new ModelFormatException(
                $"Concept '{node.Concept.Key}' has no reference '{role}', but node {node.Id} stores one.");
        }

        var targetText = _reader.GetAttribute(ModelXmlNames.Target)
            ?? throw new ModelFormatException(
                $"Reference '{role}' of node {node.Id} has no '{ModelXmlNames.Target}' attribute.");

        if (!NodeId.TryParse(targetText, out var target))
        {
            throw new ModelFormatException($"'{targetText}' is not a node id (reference '{role}' of node {node.Id}).");
        }

        // A target outside this project is written as it stands: a reference is a promise about
        // the future, and a load that has not seen the target yet is not the place to break it.
        try
        {
            node.SetReference(role, target, _reader.GetAttribute(ModelXmlNames.ResolveInfo));
        }
        catch (Exception ex) when (ex is ModelInvariantException or ArgumentException)
        {
            throw new ModelFormatException($"Reference '{role}' of node {node.Id} cannot be restored.", ex);
        }

        _reader.Skip();
    }

    private void ReadChildren(Node node)
    {
        var role = _reader.GetAttribute(ModelXmlNames.Role)
            ?? throw new ModelFormatException(
                $"A <{ModelXmlNames.Children}> element in node {node.Id} has no '{ModelXmlNames.Role}' attribute.");

        if (node.Concept.FindChild(role) is null)
        {
            throw new ModelFormatException(
                $"Concept '{node.Concept.Key}' has no child slot '{role}', but node {node.Id} stores one.");
        }

        ReadContainer(() =>
        {
            if (_reader.LocalName != ModelXmlNames.Node)
            {
                throw new ModelFormatException(
                    $"Unexpected element <{_reader.LocalName}> in child slot '{role}' of node {node.Id}.");
            }

            ReadNode(node.Model, node, role);
        });
    }

    /// <summary>
    /// Walks the content of the element the reader stands on, calling <paramref name="onElement"/>
    /// for every child element and leaving the reader behind the closing tag.
    /// </summary>
    private void ReadContainer(Action onElement)
    {
        if (_reader.IsEmptyElement)
        {
            _reader.Read();
            return;
        }

        var depth = _reader.Depth;
        _reader.Read();
        while (_reader.Depth > depth)
        {
            if (_reader.NodeType == XmlNodeType.Element)
            {
                onElement();
            }
            else
            {
                _reader.Read();
            }
        }

        _reader.Read();
    }

    private NodeId ReadNodeId()
    {
        var text = _reader.GetAttribute(ModelXmlNames.Id)
            ?? throw new ModelFormatException($"A <{ModelXmlNames.Node}> element has no '{ModelXmlNames.Id}' attribute.");

        return NodeId.TryParse(text, out var id)
            ? id
            : throw new ModelFormatException($"'{text}' is not a node id.");
    }

    private void EnsureSupportedFormatVersion()
    {
        var text = _reader.GetAttribute(ModelXmlNames.FormatVersion);
        if (text is null)
        {
            // Before the attribute existed there was only one format; treating it as absent is
            // the same as reading the oldest version.
            return;
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            throw new ModelFormatException($"'{text}' is not a format version.");
        }

        if (version > ModelXmlNames.SupportedFormatVersion)
        {
            throw new ModelFormatException(
                $"The file is written in format version {version}; this build reads up to {ModelXmlNames.SupportedFormatVersion}.");
        }
    }

    /// <summary>Takes a half-built model out of the project, so a failed load leaves no trace.</summary>
    private void Discard()
    {
        if (_model is null)
        {
            return;
        }

        foreach (var root in _model.Roots.ToArray())
        {
            _model.DeleteSubtree(root, record: false);
        }

        _project.DiscardModel(_model);
        _model = null;
    }
}
