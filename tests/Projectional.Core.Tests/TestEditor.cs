using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;

namespace Projectional.Core.Tests;

/// <summary>
/// Opens documents for the editing tests.
/// </summary>
/// <remarks>
/// Every document here is built from <see cref="TestLanguage"/> and shown through
/// <see cref="TestNotations"/>, so an editing test says which node it edits and nothing else. The
/// nodes are added as roots, because a root is what a real editor opens and because the actions that
/// add siblings ask for the parent of what they are looking at.
/// </remarks>
internal static class TestEditor
{
    /// <summary>Opens <paramref name="document"/> for editing, measured in monospace columns.</summary>
    public static EditorSession Open(Node document) => new(document, TestNotations.Registry);

    /// <summary>
    /// Opens <paramref name="document"/> for editing with <paramref name="clipboard"/> as its clipboard.
    /// </summary>
    /// <remarks>
    /// A clipboard test needs a clipboard it can read back. Passing one in also proves the session uses
    /// the adapter it was given rather than quietly falling back to its own memory.
    /// </remarks>
    public static EditorSession Open(Node document, IEditorClipboard clipboard) =>
        new(document, TestNotations.Registry, clipboard: clipboard);

    /// <summary>A bare rule: the name still holds its default, the body and extends slots are empty.</summary>
    public static Node BareRule()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        rule.Model.AddRoot(rule);

        return rule;
    }

    /// <summary>A rule that already has its body, which is the smallest document with a nested line.</summary>
    public static (Node Rule, Node Body) RuleWithBody()
    {
        var rule = BareRule();

        return (rule, rule.Model.NewChild(rule, "body", TestLanguage.Body));
    }

    /// <summary>A rule whose body holds one step, which is the smallest document with a nested line.</summary>
    public static (Node Rule, Node Body, Node Step) RuleWithStep()
    {
        var (rule, body) = RuleWithBody();

        return (rule, body, body.Model.NewChild(body, "steps", TestLanguage.Step));
    }

    /// <summary>A body holding one step per entry of <paramref name="texts"/>, in that order.</summary>
    public static (Node Body, IReadOnlyList<Node> Steps) BodyWithSteps(params string[] texts)
    {
        var (_, body) = RuleWithBody();

        return (body, [.. texts.Select(text =>
        {
            var step = body.Model.NewChild(body, "steps", TestLanguage.Step);
            step.Set("text", text);

            return step;
        })]);
    }

    /// <summary>A bare step, optionally already holding <paramref name="text"/>.</summary>
    public static Node BareStep(string? text = null)
    {
        var step = TestLanguage.NewModel().NewNode(TestLanguage.Step);

        if (text is not null)
        {
            step.Set("text", text);
        }

        step.Model.AddRoot(step);

        return step;
    }

    /// <summary>Selects everything <paramref name="node"/> shows, the way the second press of Ctrl+A does.</summary>
    /// <param name="session">The session whose selection is set.</param>
    /// <param name="node">The node to select.</param>
    public static void SelectWhole(EditorSession session, Node node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);

        session.Select(Selection.At(new CellAddress(node.Id)));
        session.Apply(KeyRouter.For(EditorKey.SelectAll));
    }

    /// <summary>
    /// Selects everything from the first cell of <paramref name="first"/> to the last cell of
    /// <paramref name="last"/>, which covers both nodes and every place between them.
    /// </summary>
    /// <param name="session">The session whose selection is set.</param>
    /// <param name="first">The first node the range covers.</param>
    /// <param name="last">The last node the range covers.</param>
    /// <remarks>
    /// A range over several nodes cannot be produced by the keyboard ladder, which widens from one value to one
    /// node and then to the whole document. It is what the user drags, so a test draws it the same way: one end
    /// in the first cell of the first node, the other at the end of the last cell of the last one.
    /// </remarks>
    public static void SelectRange(EditorSession session, Node first, Node last)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(last);

        var places = CaretPlaces.Of(session.Tree);
        var start = places.First(place => place.Address.Owner == first.Id);
        var end = places.Last(place => place.Address.Owner == last.Id);

        session.Select(new Selection(CaretPosition.At(start, 0), CaretPosition.At(end, end.Length)));
    }

    /// <summary>A concept the test notation does not project, which is how a missing notation looks.</summary>
    public static Node BareTag()
    {
        var tag = TestLanguage.NewModel().NewNode(TestLanguage.Tag);
        tag.Model.AddRoot(tag);

        return tag;
    }
}
