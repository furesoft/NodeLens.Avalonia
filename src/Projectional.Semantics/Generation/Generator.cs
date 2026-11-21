using System.Text;

using Projectional.Core;
using Projectional.Language;

namespace Projectional.Semantics;

/// <summary>Turns a model into text through one generator's templates, and records where each stretch came from.</summary>
/// <remarks>
/// <para>
/// The engine owns what a template cannot know: the current indentation, how deep the expansion is, and the
/// traceability map. A template only says what to emit, which is what keeps a generator readable as data and makes a
/// concept's rule reviewable on its own.
/// </para>
/// <para>
/// Generation is pure by construction: nothing here reads the file system, the environment, the clock or a culture.
/// Two runs over one model produce the same bytes, which is the property a host's build and a byte-comparison test
/// both depend on.
/// </para>
/// <para>
/// A run is independent of the previous one, so one generator serves a session that generates many documents:
/// <see cref="Generate"/> clears the text, the map and the depth first.
/// </para>
/// </remarks>
public sealed class Generator
{
    /// <summary>How many spaces one indentation level is unless the host says otherwise.</summary>
    /// <remarks>
    /// Two, matching the framework's own writer: a generated file that indented differently from the model file next
    /// to it would look like a second convention rather than a second artefact.
    /// </remarks>
    public const int DefaultIndentWidth = 2;

    /// <summary>How deep an expansion may nest before the engine calls it a self-recursion.</summary>
    /// <remarks>
    /// A template can name itself through <see cref="SelfPart"/>; without a limit that is a stack overflow instead of
    /// an answer, and a stack overflow cannot be reported to the user at all. The depth a real generator reaches is
    /// bounded by its model, so this is a guard against a mistake, not a budget.
    /// </remarks>
    public const int MaxDepth = 200;

    private StringBuilder _text = new();
    private List<GeneratedRegion> _regions = [];

    private GenerationFailure? _failure;
    private int _depth;
    private int _indent;

    /// <summary>Creates a generator that indents by <paramref name="indentWidth"/> spaces per level.</summary>
    /// <param name="indentWidth">The indentation width, which must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="indentWidth"/> is not positive.</exception>
    public Generator(int indentWidth = DefaultIndentWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(indentWidth);

        IndentWidth = indentWidth;
    }

    /// <summary>How many spaces one indentation level is.</summary>
    public int IndentWidth { get; }

    /// <summary>Generates <paramref name="node"/> with <paramref name="generator"/>'s templates.</summary>
    /// <param name="node">The node to generate, usually the document's root.</param>
    /// <param name="generator">The templates to use.</param>
    /// <returns>The text, its traceability map, and why it stopped if it did.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public GeneratedText Generate(Node node, TextGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(generator);

        _text.Clear();
        _regions.Clear();
        _failure = null;
        _depth = 0;
        _indent = 0;

        try
        {
            EmitNode(node, generator);
        }
        catch (GenerationStoppedException)
        {
            // The failure is already recorded; the sentinel only unwinds the expansion.
        }

        return new GeneratedText(_text.ToString(), [.. _regions], _failure);
    }

    /// <summary>Appends <paramref name="text"/> for <paramref name="node"/>, and records where it went.</summary>
    /// <remarks>
    /// An empty text records nothing: a region is a claim that these characters came from that node, and a claim about
    /// zero characters would make "what is at this offset" answer with a hole.
    /// </remarks>
    private void Emit(Node node, string? slot, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        var start = _text.Length;

        _text.Append(text);
        _regions.Add(new GeneratedRegion(node.Id, slot, start, text.Length));
    }

    /// <summary>Starts a line at the current indentation.</summary>
    private void BreakLine()
    {
        _text.Append('\n');

        if (_indent > 0)
        {
            _text.Append(' ', _indent * IndentWidth);
        }
    }

    private void EmitNode(Node node, TextGenerator generator)
    {
        if (_depth >= MaxDepth)
        {
            throw Fail(node, $"The template of '{node.Concept.Key}' expands itself more than {MaxDepth} level(s) deep.");
        }

        var template = generator.Templates.TemplateFor(node.Concept)
            ?? throw Fail(node, $"Concept '{node.Concept.Key}' has no template in generator '{generator.Name}'.");

        _depth++;

        try
        {
            EmitTemplate(template, node, generator);
        }
        finally
        {
            _depth--;
        }
    }

    private void EmitTemplate(TextTemplate template, Node node, TextGenerator generator)
    {
        foreach (var part in template.Parts)
        {
            switch (part)
            {
                case TextPart literal:
                    Emit(node, null, literal.Text);
                    break;

                case PropertyPart property:
                    Emit(node, property.Slot, PropertyOf(node, property.Slot));
                    break;

                case ParentPart parent:
                    EmitParentProperty(node, parent);
                    break;

                case ReferencePart reference:
                    Emit(node, reference.Slot, ReferenceOf(node, reference));
                    break;

                case ChildPart child:
                    EmitNode(SingleChildOf(node, child.Slot), generator);
                    break;

                case ChildrenPart children:
                    EmitChildren(
                        node,
                        children.Slot,
                        children.Separator,
                        generator,
                        children.Gaps,
                        children.TightConcepts);
                    break;

                case LoopPart loop:
                    EmitLoop(node, loop, generator);
                    break;

                case IndentPart indent:
                    _indent++;

                    try
                    {
                        EmitTemplate(indent.Body, node, generator);
                    }
                    finally
                    {
                        _indent--;
                    }

                    break;

                case LineBreakPart:
                    BreakLine();
                    break;

                case ConditionalPart conditional:
                    EmitTemplate(
                        string.Equals(PropertyOf(node, conditional.Slot), conditional.Value, StringComparison.Ordinal)
                            ? conditional.Then
                            : conditional.Otherwise,
                        node,
                        generator);
                    break;

                case WhenFilledPart whenFilled:
                    EmitTemplate(
                        IsFilled(node, whenFilled.Slot) ? whenFilled.Then : whenFilled.Otherwise,
                        node,
                        generator);
                    break;

                case SelfPart:
                    EmitNode(node, generator);
                    break;

                default:
                    throw Fail(node, $"A template part of kind '{part.GetType().Name}' is not one this generator runs.");
            }
        }
    }

    /// <summary>Expands a list, leaving out the entries that produce no text at all.</summary>
    /// <param name="owner">The node the list slot belongs to.</param>
    /// <param name="slot">The many-valued child slot to expand.</param>
    /// <param name="separator">What to emit between two entries, and only there.</param>
    /// <param name="generator">The generator being run.</param>
    /// <param name="emit">How one entry is written: as its own template, or as a loop body applied to it.</param>
    /// <remarks>
    /// <para>
    /// Each entry is written into a buffer of its own first, because whether it produced anything is only known
    /// afterwards - and a separator that was already written cannot be taken back. This is what makes "this concept is
    /// ignored" mean the same thing inside a list as outside one: without it, an ignored member would leave its comma
    /// behind and the output would be a syntax error rather than the file the author meant.
    /// </para>
    /// <para>
    /// The buffer is also why the traceability regions are shifted: a region's start is an offset into the generated
    /// text, so a stretch written into a buffer has to learn where the buffer will sit before it can claim a place.
    /// </para>
    /// </remarks>
    private void EmitEntries(
        Node owner,
        string slot,
        TextTemplate separator,
        TextGenerator generator,
        Action<Node> emit,
        ListGap gaps = ListGap.None,
        IReadOnlySet<string>? tightConcepts = null)
    {
        var entries = ChildrenOf(owner, slot);
        var written = 0;
        var previousConcept = string.Empty;

        foreach (var entry in entries)
        {
            var text = new StringBuilder();
            var regions = new List<GeneratedRegion>();
            var (outerText, outerRegions) = (_text, _regions);

            _text = text;
            _regions = regions;

            try
            {
                emit(entry);
            }
            finally
            {
                (_text, _regions) = (outerText, outerRegions);
            }

            if (text.Length == 0)
            {
                continue;
            }

            if (written++ > 0)
            {
                // A gap is one empty line, and it is a bare newline rather than a second separator: the separator
                // indents the line it starts, so two of them would leave the row's indentation standing on an
                // otherwise empty line.
                if (gaps.HasFlag(ListGap.BetweenGroups)
                    && ChildGroups.IsBoundary(previousConcept, entry.Concept.Key, tightConcepts ?? ChildGroups.Empty))
                {
                    _text.Append('\n');
                }

                EmitTemplate(separator, owner, generator);
            }
            else if (gaps.HasFlag(ListGap.BeforeFirst))
            {
                // The same empty line, in the position no boundary can describe: above the first child, where
                // there is no neighbour to separate it from - only the row the notation put it under.
                _text.Append('\n');
                EmitTemplate(separator, owner, generator);
            }

            previousConcept = entry.Concept.Key;

            var start = _text.Length;
            _text.Append(text);

            foreach (var region in regions)
            {
                _regions.Add(region with { Start = region.Start + start });
            }
        }

        // The gap below the last child. It is only reached when something was written, because a list that emitted
        // nothing must not leave a blank line behind: an ignored concept is not a row.
        if (written > 0 && gaps.HasFlag(ListGap.AfterLast))
        {
            _text.Append('\n');
            EmitTemplate(separator, owner, generator);
        }
    }

    /// <summary>Expands <paramref name="slot"/> as one entry per child, separated by <paramref name="separator"/>.</summary>
    private void EmitChildren(
        Node node,
        string slot,
        TextTemplate separator,
        TextGenerator generator,
        ListGap gaps,
        IReadOnlySet<string> tightConcepts) =>
        EmitEntries(
            node,
            slot,
            separator,
            generator,
            child => EmitNode(child, generator),
            gaps,
            tightConcepts);

    /// <summary>Expands a loop body per child, with the body applied to the child rather than to the list's owner.</summary>
    /// <remarks>
    /// The same omission as <see cref="EmitChildren"/>: a child the body cannot say anything about is not in the output,
    /// so it takes no separator with it.
    /// </remarks>
    private void EmitLoop(Node node, LoopPart loop, TextGenerator generator) =>
        EmitEntries(node, loop.Slot, loop.Separator, generator, child => EmitTemplate(loop.Body, child, generator));

    /// <summary>The value of <paramref name="slot"/>, or empty when the node holds none.</summary>
    /// <remarks>
    /// An unset property emits nothing rather than a placeholder: a template that wanted one writes it as a literal,
    /// and a framework-chosen <c>&lt;unset&gt;</c> would appear in generated code where nothing can remove it.
    /// </remarks>
    private string PropertyOf(Node node, string slot) =>
        node.Concept.FindProperty(slot) is not null
            ? node.GetOrNull(slot) ?? string.Empty
            : throw Fail(node, $"'{slot}' is not a property of {node.Concept.Key}.");

    /// <summary>The value of <paramref name="slot"/> of the node that holds <paramref name="node"/>.</summary>
    /// <remarks>
    /// <para>
    /// Empty for the outermost node: nothing holds it, so there is no property to read - not a mistake to refuse. The
    /// text is recorded as a region of the <em>holding</em> node, because that is where the characters came from and
    /// therefore where "go to source" belongs.
    /// </para>
    /// <para>
    /// A holding concept that does not declare <paramref name="part"/>'s slot stops the run naming both concepts: the
    /// template names a slot the parent's concept must have, and the author has to be able to see which one is wrong.
    /// </para>
    /// </remarks>
    private void EmitParentProperty(Node node, ParentPart part)
    {
        if (node.Parent is not { } holder)
        {
            return;
        }

        if (holder.Concept.FindProperty(part.Slot) is null)
        {
            throw Fail(
                node,
                $"'{part.Slot}' is not a property of {holder.Concept.Key}, which holds '{node.Concept.Key}'.");
        }

        Emit(holder, part.Slot, holder.GetOrNull(part.Slot) ?? string.Empty);
    }

    /// <summary>What <paramref name="part"/>'s reference says: its target's name, or its stand-in text.</summary>
    /// <remarks>
    /// Read from the model, never from a projection: generation runs on a document nobody is drawing, which is the
    /// reason the three states are answered here rather than taken from a cell. A target with no name is emitted as
    /// its concept in angle brackets, exactly as a reference cell draws it.
    /// </remarks>
    private string ReferenceOf(Node node, ReferencePart part)
    {
        if (node.Concept.FindReference(part.Slot) is null)
        {
            throw Fail(node, $"'{part.Slot}' is not a reference of {node.Concept.Key}.");
        }

        if (node.GetReference(part.Slot) is null)
        {
            return part.UnsetText;
        }

        if (node.GetReferenceTarget(part.Slot) is { } target)
        {
            return target.Name ?? $"<{target.Concept.Key}>";
        }

        // The same rule the drawing follows: the text the reference kept is what it was written as, so an unresolved
        // reference emits it rather than a placeholder - otherwise generated code would lose a name the model holds.
        return node.GetReference(part.Slot)?.ResolveInfo is { Length: > 0 } text ? text : part.UnresolvedText;
    }

    private Node SingleChildOf(Node node, string slot)
    {
        var children = ChildrenOf(node, slot);

        return children.Count == 1
            ? children[0]
            : throw Fail(node, $"'{slot}' of {node.Concept.Key} holds {children.Count} child(ren), but one was needed.");
    }

    private IReadOnlyList<Node> ChildrenOf(Node node, string slot) =>
        node.Concept.FindChild(slot) is not null
            ? node.GetChildren(slot)
            : throw Fail(node, $"'{slot}' is not a child slot of {node.Concept.Key}.");

    /// <summary>Whether <paramref name="slot"/> holds anything, whichever kind of slot it is.</summary>
    /// <remarks>
    /// The four kinds answer for themselves rather than through a second notion of "empty": a property is filled when
    /// it holds text (a default counts, because the model materialised it), a reference when it was ever written -
    /// linked or dangling, exactly the two states a reference cell draws as something other than unset - and a child
    /// or list slot when it has a child. A slot name that belongs to no kind is a failure naming the concept and the
    /// slot, because a template cannot ask about something the language did not declare.
    /// </remarks>
    private bool IsFilled(Node node, string slot)
    {
        if (node.Concept.FindProperty(slot) is not null)
        {
            return !string.IsNullOrEmpty(node.GetOrNull(slot));
        }

        if (node.Concept.FindReference(slot) is not null)
        {
            return node.GetReference(slot) is not null;
        }

        return node.Concept.FindChild(slot) is not null
            ? node.GetChildren(slot).Count > 0
            : throw Fail(node, $"'{slot}' is not a slot of {node.Concept.Key}.");
    }

    private GenerationStoppedException Fail(Node node, string message)
    {
        _failure ??= new GenerationFailure(message, node.Id);

        return new GenerationStoppedException();
    }
}
