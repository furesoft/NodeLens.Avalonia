using Projectional.Expressions;
using Projectional.Language;

namespace Projectional.Samples;

/// <summary>The sample language's text generators, written in C#: the same rule table as JSON and as a C# fragment.</summary>
/// <remarks>
/// <para>
/// Two generators for one language, because that is the case worth proving: a language does not have "a" generator,
/// it has as many as it declares, and the host picks one by name. Neither is a second notation - both read the model
/// and emit text, which is why neither knows anything about cells, carets or a palette.
/// </para>
/// <para>
/// These are the C# twins of what a declared <c>generator.mps</c> lowers to
/// (<see cref="Projectional.TextGen"/>), the same relation the type rules and the notation have: the code path stays
/// because it is the oracle the declared path is tested against, and because a language package ships one without a
/// model.
/// </para>
/// <para>
/// A binary expression is always parenthesised. A template cannot compute precedence - there is no part that asks
/// what an operand's operator binds like - so a generator that omitted the parentheses would be right only for the
/// operand it happened to see. Parenthesising is the answer that is correct for every tree, and
/// <c>docs/architecture/csharp.md</c> puts the pretty version where it belongs: in the base language's generator.
/// </para>
/// </remarks>
public static class RulesTextGenerators
{
    /// <summary>The name of the JSON generator.</summary>
    public const string JsonName = "Json";

    /// <summary>The name of the C# generator.</summary>
    public const string CSharpName = "C Sharp";

    /// <summary>The rule table as JSON, which is what a host that consumes the decisions would want.</summary>
    /// <remarks>
    /// The strings are emitted verbatim, so a value that itself contains a quote is not escaped: escaping is a
    /// per-target rule and the framework has no part that applies one. It arrives with the JSON reference language
    /// (<c>docs/architecture/import.md</c>), which is where escaping is a property of the text rather than of a
    /// template.
    /// </remarks>
    public static TextGenerator Json { get; } = new(JsonName, Expressions(TemplateRegistry.Empty)
        .Add(RulesConceptKeys.Ruleset, TextTemplate.Of(
            new TextPart("{"),
            new IndentPart(TextTemplate.Of(
                LineBreakPart.Instance,
                new TextPart("\"name\": \""),
                new PropertyPart("name"),
                new TextPart("\","),
                LineBreakPart.Instance,
                new TextPart("\"rules\": ["),
                new IndentPart(TextTemplate.Of(
                    LineBreakPart.Instance,
                    new ChildrenPart(
                        RulesLanguage.RulesSlot,
                        TextTemplate.Of(new TextPart(","), LineBreakPart.Instance)))),
                LineBreakPart.Instance,
                new TextPart("]"))),
            LineBreakPart.Instance,
            new TextPart("}")))

        .Add(RulesConceptKeys.Rule, TextTemplate.Of(
            new TextPart("{ \"rule\": \""),
            new PropertyPart("name"),
            new TextPart("\", \"when\": \""),
            new ChildPart("condition"),
            new TextPart("\", \"outcome\": \""),
            new PropertyPart("outcome"),
            new TextPart("\" }"))));

    /// <summary>The rule table as the body of a C# method, one <c>if</c> per rule.</summary>
    /// <remarks>
    /// <para>
    /// A fragment, not a compilable class: the outcome and the operands are whatever the document says, and only the
    /// C# base language (<c>docs/architecture/csharp.md</c>) knows how a rule becomes a method with a signature. What
    /// it proves is the shape - a keyword, a child expansion, a property and the indentation of a block.
    /// </para>
    /// <para>
    /// The <c>if</c> supplies the parentheses C# requires around a condition, because a template cannot ask what shape
    /// its child has: without them a condition that is not a comparison - <c>!flag</c> - generated <c>if !flag</c>,
    /// which is not C#. A comparison therefore reads with two pairs, one from the <c>if</c> and one the binary brings
    /// for precedence, which is the same price the expression templates already pay. The parity gate
    /// (<c>RulesGenerationParityTests</c>) found this, which is what it is for.
    /// </para>
    /// </remarks>
    public static TextGenerator CSharp { get; } = new(CSharpName, Expressions(TemplateRegistry.Empty)
        .Add(RulesConceptKeys.Ruleset, TextTemplate.Of(
            new TextPart("// "),
            new PropertyPart("name"),
            new IndentPart(TextTemplate.Of(
                LineBreakPart.Instance,
                new ChildrenPart(
                    RulesLanguage.RulesSlot,
                    TextTemplate.Of(LineBreakPart.Instance))))))

        .Add(RulesConceptKeys.Rule, TextTemplate.Of(
            new TextPart("if ("),
            new ChildPart("condition"),
            new TextPart(") { return \""),
            new PropertyPart("outcome"),
            new TextPart("\"; }"))));

    /// <summary>Every generator the sample language declares, in the order a menu should list them.</summary>
    /// <remarks>
    /// After the two generators, not before them: a static initialiser that read them earlier would read
    /// <see langword="null"/>, because static fields are initialised in declaration order.
    /// </remarks>
    public static IReadOnlyList<TextGenerator> All { get; } = [Json, CSharp];

    /// <summary>The expression templates both generators share, because an expression reads the same either way.</summary>
    private static TemplateRegistry Expressions(TemplateRegistry templates) => templates
        .Add(ExpressionsConceptKeys.Name, TextTemplate.Of(new PropertyPart("name")))
        .Add(ExpressionsConceptKeys.IntLiteral, TextTemplate.Of(new PropertyPart("value")))
        .Add(ExpressionsConceptKeys.BoolLiteral, TextTemplate.Of(new PropertyPart("value")))
        .Add(ExpressionsConceptKeys.StringLiteral, TextTemplate.Of(
            new TextPart("\""),
            new PropertyPart("value"),
            new TextPart("\"")))
        // `^` is a power in this vocabulary and XOR in C#, so it is the one symbol whose generated *shape* differs from
        // the way it is written. A conditional part is the choice the declared generator language also has, so a
        // declared C# generator can say the same thing and the two authoring paths stay one language (ADR 0014). The
        // cast is checked, so a result beyond the whole numbers fails the way the interpreter fails; a negative
        // exponent is the residual - the interpreter refuses it and `Math.Pow` answers a fraction, which truncates to
        // zero (recorded in `docs/architecture/csharp.md` under Risks).
        .Add(ExpressionsConceptKeys.Binary, TextTemplate.Of(
            new ConditionalPart(
                "operator",
                "^",
                TextTemplate.Of(
                    new TextPart("(checked((int)System.Math.Pow("),
                    new ChildPart("left"),
                    new TextPart(", "),
                    new ChildPart("right"),
                    new TextPart(")))")),
                TextTemplate.Of(
                    new TextPart("("),
                    new ChildPart("left"),
                    new TextPart(" "),
                    new PropertyPart("operator"),
                    new TextPart(" "),
                    new ChildPart("right"),
                    new TextPart(")")))))
        .Add(ExpressionsConceptKeys.Unary, TextTemplate.Of(
            new PropertyPart("operator"),
            new ChildPart("operand")))
        .Add(ExpressionsConceptKeys.Parenthesized, TextTemplate.Of(
            new TextPart("("),
            new ChildPart("inner"),
            new TextPart(")")))
        .Add(ExpressionsConceptKeys.If, TextTemplate.Of(
            new TextPart("("),
            new ChildPart("condition"),
            new TextPart(" ? "),
            new ChildPart("then"),
            new TextPart(" : "),
            new ChildPart("else"),
            new TextPart(")")))

        // A function is a document, and what it contributes to the text is its body: the language declares no return
        // type, so the host writes the signature - the same division of labour the ruleset's method body already has.
        .Add(ExpressionsConceptKeys.Function, TextTemplate.Of(
            new ChildPart("body")))

        // The callee is a reference, so the call writes the name the editor draws and the two cannot disagree.
        .Add(ExpressionsConceptKeys.Call, TextTemplate.Of(
            new ReferencePart("callee"),
            new TextPart("("),
            new ChildrenPart(ExpressionsLanguage.ArgumentsSlot, TextTemplate.Of(new TextPart(", "))),
            new TextPart(")")));
}
