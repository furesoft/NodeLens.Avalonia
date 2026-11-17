using Projectional.Avalonia;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;

namespace Projectional.Screenshots;
/// <summary>What the pointer is resting on when a screenshot is taken.</summary>
internal enum HoverKind
{
    /// <summary>Nothing; the caret and the document are the whole picture.</summary>
    None,

    /// <summary>A validation squiggle, so the finding's tooltip is painted.</summary>
    Marker,

    /// <summary>A node's text, so the tooltip the slot or the concept declares appears after the hover delay.</summary>
    Text,
}

/// <summary>One image to produce: the document, the view settings and the state to drive first.</summary>
/// <param name="FileName">The name the PNG is written under, without the extension.</param>
/// <param name="Create">Builds the model the shot is taken of.</param>
/// <param name="Prepare">Changes the projection - a variant, a fold - before the image is sized.</param>
/// <param name="Arrange">Drives the session into the state the feature is about.</param>
/// <param name="Dark">Whether to use the dark theme.</param>
/// <param name="LineNumbers">Whether to show the gutter.</param>
/// <param name="FontSize">The font size, in pixels.</param>
/// <param name="ExtraWidth">Room to reserve right of the document, for a list or a tooltip.</param>
/// <param name="ExtraHeight">Room to reserve below the document, for a list or a tooltip.</param>
/// <param name="Hover">What the pointer rests on, if anything.</param>
/// <param name="HoverAddress">The cell the concept tooltip is asked about.</param>
/// <param name="HoverDelayMs">How long the pointer rests before the picture is taken.</param>
internal sealed record Shot(
    string FileName,
    Func<Model> Create,
    Action<ProjectionalEditorControl, EditorSession>? Prepare = null,
    Action<ProjectionalEditorControl, EditorSession>? Arrange = null,
    bool Dark = false,
    bool LineNumbers = false,
    double FontSize = 14,
    double ExtraWidth = 0,
    double ExtraHeight = 0,
    HoverKind Hover = HoverKind.None,
    Func<EditorSession, CellAddress?>? HoverAddress = null,
    int HoverDelayMs = 0);
