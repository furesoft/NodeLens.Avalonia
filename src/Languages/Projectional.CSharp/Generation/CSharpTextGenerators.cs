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
    /// <param name="slot">The list slot to expand.</param>
    /// <param name="gaps">
    /// Where the list breathes, as the .NET conventions ask of a class's members and of a namespace's types.
    /// </param>
    /// <param name="tightConcepts">The concepts whose neighbours share a group.</param>
    private static ChildrenPart Members(
        string slot,
        ListGap gaps = ListGap.None,
        IReadOnlySet<string>? tightConcepts = null) =>
        new(slot, TextTemplate.Of(LineBreakPart.Instance), gaps, tightConcepts);

    private static TemplateRegistry Templates() => TemplateRegistry.Empty
        .Add(CSharpConceptKeys.File, TextTemplate.Of(
            new WhenFilledPart(
                CSharpConceptKeys.UsingsSlot,
                TextTemplate.Of(
                    new ChildrenPart(CSharpConceptKeys.UsingsSlot, TextTemplate.Of(LineBreakPart.Instance)),
                    LineBreakPart.Instance)),
            new ChildPart(CSharpConceptKeys.NamespaceSlot),
            LineBreakPart.Instance))

        .Add(CSharpConceptKeys.Using, TextTemplate.Of(
            new TextPart("using "),
            new PropertyPart(CSharpLanguage.NameSlot),
            new TextPart(";")))

        .Add(CSharpConceptKeys.Namespace, TextTemplate.Of(
            new ConditionalPart(
                CSharpLanguage.IsFileScopedSlot,
                "true",
                TextTemplate.Of(
                    new TextPart("namespace "),
                    new PropertyPart(CSharpLanguage.NameSlot),
                    new TextPart(";"),
                    Members(
                        CSharpConceptKeys.ClassesSlot,
                        ListGap.BetweenGroups | ListGap.BeforeFirst)),
                TextTemplate.Of(
                    new TextPart("namespace "),
                    new PropertyPart(CSharpLanguage.NameSlot),
                    LineBreakPart.Instance,
                    new TextPart("{"),
                    new IndentPart(TextTemplate.Of(
                        LineBreakPart.Instance,
                        Members(CSharpConceptKeys.ClassesSlot, ListGap.BetweenGroups))),
                    LineBreakPart.Instance,
                    new TextPart("}")))))

        .Add(CSharpConceptKeys.Class, TextTemplate.Of(
            new PropertyPart(CSharpLanguage.VisibilitySlot),
            new TextPart(" "),
            new ConditionalPart(CSharpLanguage.IsStaticSlot, "true", TextTemplate.Of(new TextPart("static "))),
            new TextPart("class "),
            new PropertyPart(CSharpLanguage.NameSlot),
            new WhenFilledPart(
                CSharpConceptKeys.InheritanceSlot,
                TextTemplate.Of(new TextPart(" "), new ChildPart(CSharpConceptKeys.InheritanceSlot))),
            LineBreakPart.Instance,
            new TextPart("{"),
            new IndentPart(TextTemplate.Of(
                new WhenFilledPart(
                    CSharpConceptKeys.MembersSlot,
                    TextTemplate.Of(
                        LineBreakPart.Instance,
                        Members(
                            CSharpConceptKeys.MembersSlot,
                            ListGap.BetweenGroups,
                            CSharpMemberGroups.Tight))))),
            LineBreakPart.Instance,
            new TextPart("}")))

        .Add(CSharpConceptKeys.Inheritance, TextTemplate.Of(
            new TextPart(": "),
            new ChildrenPart(CSharpConceptKeys.EntriesSlot, TextTemplate.Of(new TextPart(", ")))))

        .Add(CSharpConceptKeys.InheritanceEntry, TextTemplate.Of(
            new ReferencePart(CSharpConceptKeys.TargetSlot, unsetText: "<type>")))
        .Add(CSharpConceptKeys.Interface, TextTemplate.Of(
            new PropertyPart(CSharpLanguage.VisibilitySlot),
            new TextPart(" interface "),
            new PropertyPart(CSharpLanguage.NameSlot),
            LineBreakPart.Instance,
            new TextPart("{"),
            new IndentPart(TextTemplate.Of(
                new WhenFilledPart(CSharpConceptKeys.MembersSlot, TextTemplate.Of(LineBreakPart.Instance, Members(CSharpConceptKeys.MembersSlot))))),
            LineBreakPart.Instance,
            new TextPart("}")))
        .Add(CSharpConceptKeys.Enum, TextTemplate.Of(
            new PropertyPart(CSharpLanguage.VisibilitySlot),
            new TextPart(" enum "),
            new PropertyPart(CSharpLanguage.NameSlot),
            LineBreakPart.Instance,
            new TextPart("{"),
            new IndentPart(TextTemplate.Of(
                new WhenFilledPart(
                    CSharpConceptKeys.MembersSlot,
                    TextTemplate.Of(
                        LineBreakPart.Instance,
                        new ChildrenPart(
                            CSharpConceptKeys.MembersSlot,
                            TextTemplate.Of(new TextPart(", "))))))),
            LineBreakPart.Instance,
            new TextPart("}")))

        .Add(CSharpConceptKeys.EnumMember, TextTemplate.Of(new PropertyPart(CSharpLanguage.NameSlot)))

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

        .Add(CSharpConceptKeys.Constructor, TextTemplate.Of(
            new PropertyPart(CSharpLanguage.VisibilitySlot),
            new TextPart(" "),
            new PropertyPart(CSharpLanguage.NameSlot),
            new TextPart("("),
            new ChildrenPart(CSharpConceptKeys.ParametersSlot, TextTemplate.Of(new TextPart(", "))),
            new TextPart(")"),
            LineBreakPart.Instance,
            new ChildPart(CSharpConceptKeys.BodySlot)))

        // A property in its two shapes, the same pair the notation draws: the auto-property stays on one line, and the
        // one with bodies takes the shape a method has. The break that ends `{` belongs inside the indent, or the
        // first accessor lands at the property's own indentation.
        .Add(CSharpConceptKeys.Property, TextTemplate.Of(
            new ConditionalPart(
                CSharpConceptKeys.HasBodiesSlot,
                "false",
                TextTemplate.Of(
                    new PropertyPart(CSharpLanguage.VisibilitySlot),
                    new TextPart(" "),
                    new ChildPart("type"),
                    new TextPart(" "),
                    new PropertyPart(CSharpLanguage.NameSlot),
                    new TextPart(" { get;"),
                    new ConditionalPart(CSharpConceptKeys.HasSetterSlot, "true", TextTemplate.Of(new TextPart(" set;"))),
                    new TextPart(" }"))),
            new ConditionalPart(
                CSharpConceptKeys.HasBodiesSlot,
                "true",
                TextTemplate.Of(
                    new PropertyPart(CSharpLanguage.VisibilitySlot),
                    new TextPart(" "),
                    new ChildPart("type"),
                    new TextPart(" "),
                    new PropertyPart(CSharpLanguage.NameSlot),
                    LineBreakPart.Instance,
                    new TextPart("{"),
                    new IndentPart(TextTemplate.Of(
                        LineBreakPart.Instance,
                        new TextPart("get"),
                        LineBreakPart.Instance,
                        new ChildPart(CSharpConceptKeys.GetterSlot),
                        new ConditionalPart(
                            CSharpConceptKeys.HasSetterSlot,
                            "true",
                            TextTemplate.Of(
                                LineBreakPart.Instance,
                                new TextPart("set"),
                                LineBreakPart.Instance,
                                new ChildPart(CSharpConceptKeys.SetterSlot))))),
                    LineBreakPart.Instance,
                    new TextPart("}")))))

        // An indexer: the same shape as a property with bodies, with the parameters in brackets where the name would
        // be. The setter is a WhenFilledPart, so a get-only indexer writes no `set` block at all.
        .Add(CSharpConceptKeys.Indexer, TextTemplate.Of(
            new PropertyPart(CSharpLanguage.VisibilitySlot),
            new TextPart(" "),
            new ChildPart("type"),
            new TextPart(" this["),
            new ChildrenPart(CSharpConceptKeys.ParametersSlot, TextTemplate.Of(new TextPart(", "))),
            new TextPart("]"),
            LineBreakPart.Instance,
            new TextPart("{"),
            new IndentPart(TextTemplate.Of(
                LineBreakPart.Instance,
                new TextPart("get"),
                LineBreakPart.Instance,
                new ChildPart(CSharpConceptKeys.GetterSlot),
                new WhenFilledPart(
                    CSharpConceptKeys.SetterSlot,
                    TextTemplate.Of(
                        LineBreakPart.Instance,
                        new TextPart("set"),
                        LineBreakPart.Instance,
                        new ChildPart(CSharpConceptKeys.SetterSlot))))),
            LineBreakPart.Instance,
            new TextPart("}")))
        .Add(CSharpConceptKeys.Signature, TextTemplate.Of(
            new PropertyPart(CSharpLanguage.VisibilitySlot),
            new TextPart(" "),
            new ChildPart("returnType"),
            new TextPart(" "),
            new PropertyPart(CSharpLanguage.NameSlot),
            new TextPart("("),
            new ChildrenPart(CSharpConceptKeys.ParametersSlot, TextTemplate.Of(new TextPart(", "))),
            new TextPart(");")))
        .Add(CSharpConceptKeys.Parameter, TextTemplate.Of(
            new ChildPart("type"),
            new TextPart(" "),
            new PropertyPart(CSharpLanguage.NameSlot)))

        .Add(CSharpConceptKeys.Block, TextTemplate.Of(
            new TextPart("{"),
            new IndentPart(TextTemplate.Of(
                new WhenFilledPart(CSharpConceptKeys.StatementsSlot, TextTemplate.Of(LineBreakPart.Instance, Members(CSharpConceptKeys.StatementsSlot))))),
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
            new TextPart(" "),
            new PropertyPart(CSharpLanguage.AssignmentOperatorSlot),
            new TextPart(" "),
            new ChildPart(CSharpConceptKeys.ValueSlot),
            new TextPart(";")))

        .Add(CSharpConceptKeys.Unary, TextTemplate.Of(
            new ConditionalPart(
                CSharpLanguage.IsPostfixSlot,
                "true",
                TextTemplate.Of(new ChildPart(CSharpConceptKeys.OperandSlot), new PropertyPart(CSharpLanguage.UnaryOperatorSlot)),
                TextTemplate.Of(new PropertyPart(CSharpLanguage.UnaryOperatorSlot), new ChildPart(CSharpConceptKeys.OperandSlot)))))

        .Add(CSharpConceptKeys.For, TextTemplate.Of(
            new TextPart("for ("),
            new ChildPart(CSharpConceptKeys.InitializerSlot),
            new TextPart(" "),
            new ChildPart(CSharpConceptKeys.ConditionSlot),
            new TextPart("; "),
            new ChildPart(CSharpConceptKeys.IncrementSlot),
            new TextPart(")"),
            LineBreakPart.Instance,
            new ChildPart(CSharpConceptKeys.BodySlot)))

        .Add(CSharpConceptKeys.Conditional, TextTemplate.Of(
            new ChildPart(CSharpConceptKeys.ConditionSlot),
            new TextPart(" ? "),
            new ChildPart(CSharpLanguage.WhenTrueSlot),
            new TextPart(" : "),
            new ChildPart(CSharpLanguage.WhenFalseSlot)))

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

        // A try: the block, then each handler separated by a line break and the final block when there is one. The
        // handler list is wrapped in WhenFilled so a try without a catch leaves no blank line - the same guard the
        // class's member list needs.
        .Add(CSharpConceptKeys.Try, TextTemplate.Of(
            new TextPart("try"),
            LineBreakPart.Instance,
            new ChildPart(CSharpConceptKeys.BodySlot),
            new WhenFilledPart(
                CSharpConceptKeys.CatchesSlot,
                TextTemplate.Of(LineBreakPart.Instance, Members(CSharpConceptKeys.CatchesSlot))),
            new WhenFilledPart(
                CSharpConceptKeys.FinallySlot,
                TextTemplate.Of(
                    LineBreakPart.Instance,
                    new TextPart("finally"),
                    LineBreakPart.Instance,
                    new ChildPart(CSharpConceptKeys.FinallySlot)))))

        .Add(CSharpConceptKeys.Catch, TextTemplate.Of(
            new TextPart("catch ("),
            new ChildPart("type"),
            new TextPart(" "),
            new PropertyPart(CSharpLanguage.NameSlot),
            new TextPart(")"),
            LineBreakPart.Instance,
            new ChildPart(CSharpConceptKeys.BodySlot)))

        // A switch: the value in parentheses, then the sections one per row between braces.
        .Add(CSharpConceptKeys.Switch, TextTemplate.Of(
            new TextPart("switch ("),
            new ChildPart(CSharpConceptKeys.SubjectSlot),
            new TextPart(")"),
            LineBreakPart.Instance,
            new TextPart("{"),
            new IndentPart(TextTemplate.Of(
                LineBreakPart.Instance,
                new ChildrenPart(CSharpConceptKeys.SectionsSlot, TextTemplate.Of(LineBreakPart.Instance)))),
            LineBreakPart.Instance,
            new TextPart("}")))

        // One section: `case value:` or `default:`, then what it runs indented beneath.
        .Add(CSharpConceptKeys.SwitchSection, TextTemplate.Of(
            new ConditionalPart(CSharpConceptKeys.IsDefaultSlot, "true", TextTemplate.Of(new TextPart("default:"))),
            new ConditionalPart(CSharpConceptKeys.IsDefaultSlot, "false", TextTemplate.Of(
                new TextPart("case "),
                new ChildPart(CSharpConceptKeys.LabelSlot),
                new TextPart(":"))),
            new IndentPart(TextTemplate.Of(
                LineBreakPart.Instance,
                new ChildrenPart(CSharpConceptKeys.StatementsSlot, TextTemplate.Of(LineBreakPart.Instance))))))

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
            new TextPart("]")))

        // The three type-directed expressions, spelled the way the notation draws them: the type cell is a child in
        // both, so neither the drawing nor the text has to know what the type means.
        .Add(CSharpConceptKeys.Cast, TextTemplate.Of(
            new TextPart("("),
            new ChildPart("type"),
            new TextPart(")"),
            new ChildPart(CSharpConceptKeys.OperandSlot)))
        .Add(CSharpConceptKeys.Is, TextTemplate.Of(
            new ChildPart(CSharpConceptKeys.OperandSlot),
            new TextPart(" is "),
            new ChildPart("type")))

        .Add(CSharpConceptKeys.As, TextTemplate.Of(
            new ChildPart(CSharpConceptKeys.OperandSlot),
            new TextPart(" as "),
            new ChildPart("type")))

        .Add(CSharpConceptKeys.NullLiteral, TextTemplate.Of(new TextPart("null")))
        .Add(CSharpConceptKeys.Index, TextTemplate.Of(
            new ChildPart(CSharpConceptKeys.TargetSlot),
            new TextPart("["),
            new ChildPart(CSharpConceptKeys.IndexSlot),
            new TextPart("]")));
}
