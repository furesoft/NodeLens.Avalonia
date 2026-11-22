using System.Text;

using Projectional.Language;

namespace Projectional.LanguageDocs;

/// <summary>A generator's template written out as the text it produces, with its slots marked.</summary>
/// <remarks>
/// <para>
/// A template is a tree of parts, not a string, so there is nothing to print: this walks it and writes what the parts
/// would emit - literal text as itself, a property as <c>{slot}</c>, a child as <c>{slot}</c>, a list as
/// <c>{slot...}</c>, and a conditional in brackets, because a generator's shape is the thing a reader of a language
/// wants to see before trusting it with their model.
/// </para>
/// <para>
/// It is the generator's counterpart to <see cref="NotationSpelling"/> and is read the same way: from the parts the
/// generator holds, never from a generated file, so a language whose last generation is stale still documents the
/// template it has.
/// </para>
/// </remarks>
internal static class TemplateSpelling
{
    /// <summary>Writes <paramref name="template"/> out as text, trimmed.</summary>
    internal static string Of(TextTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return Raw(template).Trim();
    }

    /// <summary>Writes a nested template out without trimming it.</summary>
    /// <remarks>
    /// Trimming is for the whole template, where the whitespace is the author's padding around a block. Inside a
    /// bracket or after a separator it is content: a list joined by a line break would otherwise print as an empty
    /// separator, which says the opposite of what the template does.
    /// </remarks>
    private static string Raw(TextTemplate template)
    {
        var text = new StringBuilder();
        var state = new State();

        Write(template, state, text);

        return text.ToString();
    }

    private static void Write(TextTemplate template, State state, StringBuilder text)
    {
        foreach (var part in template.Parts)
        {
            Write(part, state, text);
        }
    }

    private static void Write(TemplatePart part, State state, StringBuilder text)
    {
        switch (part)
        {
            case TextPart literal:
                Emit(text, state, literal.Text);
                break;

            case PropertyPart property:
                Emit(text, state, "{" + property.Slot + "}");
                break;

            case ChildPart child:
                Emit(text, state, "{" + child.Slot + "}");
                break;

            case ChildrenPart children:
                // The separator is what a list is joined with, which is the one thing a reader cannot guess from the slot.
                Emit(
                    text,
                    state,
                    children.Separator.IsEmpty
                        ? "{" + children.Slot + "...}"
                        : "{" + children.Slot + "... separated by \"" + Visible(Raw(children.Separator)) + "\"}");
                break;

            case ReferencePart reference:
                Emit(text, state, "->" + reference.Slot);
                break;

            case SelfPart:
                Emit(text, state, "{self}");
                break;

            case ParentPart parent:
                Emit(text, state, "{parent." + parent.Slot + "}");
                break;

            case LineBreakPart:
                Emit(text, state, "\n");
                break;

            case IndentPart indent:
                state.Level++;
                Write(indent.Body, state, text);
                state.Level--;
                break;

            case LoopPart loop:
                Emit(text, state, "{loop " + loop.Slot + "}");
                state.Level++;
                Write(loop.Body, state, text);
                state.Level--;
                break;

            case WhenFilledPart whenFilled:
                Emit(text, state, Bracket("if " + whenFilled.Slot, Raw(whenFilled.Then), isFirst: true));
                Emit(text, state, Bracket("else", Raw(whenFilled.Otherwise), isFirst: false));
                break;

            case ConditionalPart conditional:
                Emit(text, state, Bracket($"if {conditional.Slot} = \"{conditional.Value}\"", Raw(conditional.Then), isFirst: true));
                Emit(text, state, Bracket("else", Raw(conditional.Otherwise), isFirst: false));
                break;

            default:
                Emit(text, state, "{" + part + "}");
                break;
        }
    }

    /// <summary>A branch of a conditional, in brackets, or nothing when there is no branch to show.</summary>
    private static string Bracket(string condition, string content, bool isFirst)
    {
        var inside = Visible(content);

        if (inside.Length == 0)
        {
            return string.Empty;
        }

        var lead = isFirst ? string.Empty : " ";

        return $"{lead}[{condition}: {inside}]";
    }

    /// <summary>Text as one line: a line break becomes <c>\n</c> so it cannot reshape the line it sits in.</summary>
    private static string Visible(string text) =>
        text.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Trim();

    /// <summary>Appends <paramref name="text"/>, indenting after every line break.</summary>
    private static void Emit(StringBuilder text, State state, string value)
    {
        foreach (var character in value)
        {
            text.Append(character);

            if (character == '\n')
            {
                text.Append(' ', state.Level * 2);
            }
        }
    }

    /// <summary>How deep the walk is, which is the indentation a line break continues with.</summary>
    private sealed class State
    {
        internal int Level { get; set; }
    }
}