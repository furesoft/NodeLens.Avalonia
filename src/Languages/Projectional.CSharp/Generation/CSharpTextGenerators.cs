using Projectional.Language;

namespace Projectional.CSharp.Generation;

/// <summary>The templates that write a C# model back out as the code a compiler accepts.</summary>
/// <remarks>
/// <para>
/// This is the half that makes a DSL compilable: a language declares its slots over the base language's concepts, the
/// user writes the logic in the editor, and this generator turns the tree into text. Nothing here reads a file, the
/// clock or a culture, so two runs over one model produce the same bytes - which is what a host's build depends on.
/// </para>
/// <para>
/// Two line-break rules are what make the indentation right, and both are easy to get wrong:
/// </para>
/// <list type="bullet">
/// <item>
/// A member list is <em>separated</em> by a line break, never terminated by one. A trailing <c>Line</c> inside a
/// member's template would leave the closing brace at the member's indentation, because the engine writes the
/// indentation at the moment of the break.
/// </item>
/// <item>
/// The break that ends <c>{</c> belongs <em>inside</em> the indent, for the same reason: a break taken before the
/// indent started would leave the first member unindented.
/// </item>
/// </list>
/// </remarks>
public static class CSharpTextGenerators
{
    /// <summary>The name of the generator, which is what a host picks it by.</summary>
    public const string Name = "CSharp";

    /// <summary>The one generator the base language ships.</summary>
    public static TextGenerator Generator { get; } = new(Name, Templates());

    /// <summary>A member list: one entry per line, through the separator rather than a trailing break.</summary>
    private static ChildrenPart Members(string slot) =>
        new(slot, TextTemplate.Of(LineBreakPart.Instance));

    private static TemplateRegistry Templates() => TemplateRegistry.Empty
        .Add(CSharpConceptKeys.File, TextTemplate.Of(
            new ChildPart(CSharpConceptKeys.NamespaceSlot),
            LineBreakPart.Instance))

        .Add(CSharpConceptKeys.Namespace, TextTemplate.Of(
            new ConditionalPart(
                CSharpLanguage.IsFileScopedSlot,
                "true",
                TextTemplate.Of(
                    new TextPart("namespace "),
                    new PropertyPart(CSharpLanguage.NameSlot),
                    new TextPart(";"),
                    LineBreakPart.Instance,
                    Members(CSharpConceptKeys.ClassesSlot)),
                TextTemplate.Of(
                    new TextPart("namespace "),
                    new PropertyPart(CSharpLanguage.NameSlot),
                    LineBreakPart.Instance,
                    new TextPart("{"),
                    new IndentPart(TextTemplate.Of(
                        LineBreakPart.Instance,
                        Members(CSharpConceptKeys.ClassesSlot))),
                    LineBreakPart.Instance,
                    new TextPart("}")))))

        .Add(CSharpConceptKeys.Class, TextTemplate.Of(
            new PropertyPart(CSharpLanguage.VisibilitySlot),
            new TextPart(" "),
            new ConditionalPart(CSharpLanguage.IsStaticSlot, "true", TextTemplate.Of(new TextPart("static "))),
            new TextPart("class "),
            new PropertyPart(CSharpLanguage.NameSlot),
            LineBreakPart.Instance,
            new TextPart("{"),
            new IndentPart(TextTemplate.Of(
                LineBreakPart.Instance,
                Members(CSharpConceptKeys.MembersSlot))),
            LineBreakPart.Instance,
            new TextPart("}")))

        .Add(CSharpConceptKeys.Field, TextTemplate.Of(
            new PropertyPart(CSharpLanguage.VisibilitySlot),
            new TextPart(" "),
            new ChildPart("type"),
            new TextPart(" "),
            new PropertyPart(CSharpLanguage.NameSlot),
            new WhenFilledPart(
                CSharpConceptKeys.ValueSlot,
                TextTemplate.Of(new TextPart(" = "), new ChildPart(CSharpConceptKeys.ValueSlot))),
            new TextPart(";")))

        .Add(CSharpConceptKeys.Method, TextTemplate.Of(
            new PropertyPart(CSharpLanguage.VisibilitySlot),
            new TextPart(" "),
            new ConditionalPart(CSharpLanguage.IsStaticSlot, "true", TextTemplate.Of(new TextPart("static "))),
            new ChildPart("returnType"),
            new TextPart(" "),
            new PropertyPart(CSharpLanguage.NameSlot),
            new TextPart("("),
            new ChildrenPart(CSharpConceptKeys.ParametersSlot, TextTemplate.Of(new TextPart(", "))),
            new TextPart(")"),
            new WhenFilledPart(
                CSharpConceptKeys.BodySlot,
                TextTemplate.Of(LineBreakPart.Instance, new ChildPart(CSharpConceptKeys.BodySlot)),
                TextTemplate.Of(new TextPart(";")))))

        .Add(CSharpConceptKeys.Parameter, TextTemplate.Of(
            new ChildPart("type"),
            new TextPart(" "),
            new PropertyPart(CSharpLanguage.NameSlot)))

        .Add(CSharpConceptKeys.Block, TextTemplate.Of(
            new TextPart("{"),
            new IndentPart(TextTemplate.Of(
                LineBreakPart.Instance,
                Members(CSharpConceptKeys.StatementsSlot))),
            LineBreakPart.Instance,
            new TextPart("}")))

        .Add(CSharpConceptKeys.ExpressionStatement, TextTemplate.Of(
            new ChildPart("expression"),
            new TextPart(";")))

        .Add(CSharpConceptKeys.LocalDeclaration, TextTemplate.Of(
            new ChildPart("type"),
            new TextPart(" "),
            new PropertyPart(CSharpLanguage.NameSlot),
            new WhenFilledPart(
                CSharpConceptKeys.ValueSlot,
                TextTemplate.Of(new TextPart(" = "), new ChildPart(CSharpConceptKeys.ValueSlot))),
            new TextPart(";")))

        .Add(CSharpConceptKeys.Assignment, TextTemplate.Of(
            new ChildPart("target"),
            new TextPart(" = "),
            new ChildPart(CSharpConceptKeys.ValueSlot),
            new TextPart(";")))

        .Add(CSharpConceptKeys.If, TextTemplate.Of(
            new TextPart("if ("),
            new ChildPart(CSharpConceptKeys.ConditionSlot),
            new TextPart(")"),
            LineBreakPart.Instance,
            new ChildPart(CSharpConceptKeys.ThenSlot),
            new WhenFilledPart(
                CSharpConceptKeys.OtherwiseSlot,
                TextTemplate.Of(
                    LineBreakPart.Instance,
                    new TextPart("else"),
                    LineBreakPart.Instance,
                    new ChildPart(CSharpConceptKeys.OtherwiseSlot)))))

        .Add(CSharpConceptKeys.While, TextTemplate.Of(
            new TextPart("while ("),
            new ChildPart(CSharpConceptKeys.ConditionSlot),
            new TextPart(")"),
            LineBreakPart.Instance,
            new ChildPart(CSharpConceptKeys.BodySlot)))

        .Add(CSharpConceptKeys.ForEach, TextTemplate.Of(
            new TextPart("foreach ("),
            new ChildPart("type"),
            new TextPart(" "),
            new PropertyPart(CSharpLanguage.NameSlot),
            new TextPart(" in "),
            new ChildPart(CSharpConceptKeys.SourceSlot),
            new TextPart(")"),
            LineBreakPart.Instance,
            new ChildPart(CSharpConceptKeys.BodySlot)))

        .Add(CSharpConceptKeys.Break, TextTemplate.Of(new TextPart("break;")))
        .Add(CSharpConceptKeys.Continue, TextTemplate.Of(new TextPart("continue;")))
        .Add(CSharpConceptKeys.Return, TextTemplate.Of(
            new TextPart("return"),
            new WhenFilledPart(
                CSharpConceptKeys.ValueSlot,
                TextTemplate.Of(new TextPart(" "), new ChildPart(CSharpConceptKeys.ValueSlot))),
            new TextPart(";")))

        .Add(CSharpConceptKeys.Throw, TextTemplate.Of(
            new TextPart("throw "),
            new ChildPart(CSharpConceptKeys.ValueSlot),
            new TextPart(";")))

        .Add(CSharpConceptKeys.TypeReference, TextTemplate.Of(new PropertyPart(CSharpLanguage.NameSlot)))
        .Add(CSharpConceptKeys.IntLiteral, TextTemplate.Of(new PropertyPart(CSharpLanguage.ValueProperty)))
        .Add(CSharpConceptKeys.BoolLiteral, TextTemplate.Of(new PropertyPart(CSharpLanguage.ValueProperty)))
        // The target's name, or the text the reference kept while it resolved to nothing.
        .Add(CSharpConceptKeys.Name, TextTemplate.Of(
            new ReferencePart(CSharpConceptKeys.TargetSlot, unsetText: "<name>", unresolvedText: "<name>")))

        .Add(CSharpConceptKeys.StringLiteral, TextTemplate.Of(
            new TextPart("\""),
            new PropertyPart(CSharpLanguage.ValueProperty),
            new TextPart("\"")))

        .Add(CSharpConceptKeys.MemberAccess, TextTemplate.Of(
            new ChildPart("target"),
            new TextPart("."),
            new PropertyPart(CSharpLanguage.MemberSlot)))

        .Add(CSharpConceptKeys.Call, TextTemplate.Of(
            new ChildPart("callee"),
            new TextPart("("),
            new ChildrenPart(CSharpConceptKeys.ArgumentsSlot, TextTemplate.Of(new TextPart(", "))),
            new TextPart(")")))

        .Add(CSharpConceptKeys.Binary, TextTemplate.Of(
            new ChildPart("left"),
            new TextPart(" "),
            new PropertyPart(CSharpLanguage.OperatorSlot),
            new TextPart(" "),
            new ChildPart("right")))

        .Add(CSharpConceptKeys.Parenthesized, TextTemplate.Of(
            new TextPart("("),
            new ChildPart("inner"),
            new TextPart(")")))

        .Add(CSharpConceptKeys.New, TextTemplate.Of(
            new TextPart("new "),
            new ChildPart("type"),
            new TextPart("("),
            new ChildrenPart(CSharpConceptKeys.ArgumentsSlot, TextTemplate.Of(new TextPart(", "))),
            new TextPart(")")))

        .Add(CSharpConceptKeys.CollectionExpression, TextTemplate.Of(
            new TextPart("["),
            new ChildrenPart(CSharpConceptKeys.ElementsSlot, TextTemplate.Of(new TextPart(", "))),
            new TextPart("]")));
}
