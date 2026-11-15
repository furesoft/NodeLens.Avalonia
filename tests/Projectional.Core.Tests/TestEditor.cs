using Projectional.Core;
using Projectional.Editing;

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
    public static EditorSession Open(Node document, ITextClipboard clipboard) =>
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

    /// <summary>A concept the test notation does not project, which is how a missing notation looks.</summary>
    public static Node BareTag()
    {
        var tag = TestLanguage.NewModel().NewNode(TestLanguage.Tag);
        tag.Model.AddRoot(tag);

        return tag;
    }
}
