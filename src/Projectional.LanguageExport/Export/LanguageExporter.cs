using System.Text;

using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;

namespace Projectional.LanguageExport;

/// <summary>Turns a declared language into the files a C# language package is made of.</summary>
/// <remarks>
/// <para>
/// The answer is a list of files rather than a written directory, for the reason
/// <see cref="GeneratedFile"/> gives: what a language becomes is a value a test can read, and where it goes is the
/// host's decision. With <see cref="ExportOutput.Assembly"/> the same answer carries the assembly that source
/// compiles to, because a package is a binary and compiling it is not something the caller should have to do twice.
/// </para>
/// <para>
/// The exporter takes the pieces of a language instead of the loaded project, so it depends on nothing but the two
/// registries it writes out. A language project is one caller, not the definition of the input.
/// </para>
/// <para>
/// An aspect the declared path has but this export cannot spell - type rules, generators, constraints - is named in
/// the generated file and in the README rather than dropped. A generated language that looked complete and silently
/// answered <c>Unknown</c> for every type would be a language nobody could tell was half exported.
/// </para>
/// </remarks>
public static class LanguageExporter
{
    /// <summary>Exports a language project, asking it which aspects it declares.</summary>
    /// <param name="language">The language a user opened.</param>
    /// <param name="namespaceName">The namespace to declare, or <c>null</c> to declare none.</param>
    /// <param name="output">Whether to compile the generated source into an assembly as well.</param>
    /// <exception cref="ExportCompilationException">The generated source does not compile.</exception>
    public static ExportResult Export(
        LanguageProject language,
        string? namespaceName = null,
        ExportOutput output = ExportOutput.Source)
    {
        ArgumentNullException.ThrowIfNull(language);

        return Export(
            language.Name,
            language.Declarations,
            language.Notations.Notations,
            namespaceName,
            language.HasTypes,
            language.HasGenerators,
            language.HasConstraints,
            output);
    }

    /// <summary>Builds the generated file and its README, in that order, and compiles the source on request.</summary>
    /// <param name="name">The language's name, which decides the class name and the file name.</param>
    /// <param name="declarations">What the structure documents declared, in declaration order.</param>
    /// <param name="notations">The notation they are drawn with, in registration order.</param>
    /// <param name="namespaceName">The namespace to declare, or <c>null</c> to declare none.</param>
    /// <param name="hasTypes">Whether the language declares type rules this export cannot write.</param>
    /// <param name="hasGenerators">Whether it declares text generators this export cannot write.</param>
    /// <param name="hasConstraints">Whether it declares constraints this export cannot write.</param>
    /// <param name="output">Whether to compile the generated source into an assembly as well.</param>
    /// <exception cref="ExportCompilationException">The generated source does not compile.</exception>
    public static ExportResult Export(
        string name,
        IReadOnlyList<ConceptDeclaration> declarations,
        IReadOnlyList<Notation> notations,
        string? namespaceName = null,
        bool hasTypes = false,
        bool hasGenerators = false,
        bool hasConstraints = false,
        ExportOutput output = ExportOutput.Source)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(notations);

        var className = ClassName(name);
        var omitted = Omissions(hasTypes, hasGenerators, hasConstraints);
        var source = Source(name, className, namespaceName, declarations, notations, omitted);

        return new ExportResult(
            [
                new GeneratedFile($"{className}.cs", source),
                new GeneratedFile("README.md", Readme(name, className, notations.Count > 0, omitted)),
            ],
            output == ExportOutput.Assembly ? AssemblyCompiler.Compile(className, source) : null);
    }

    private static List<string> Omissions(bool hasTypes, bool hasGenerators, bool hasConstraints)
    {
        var omitted = new List<string>();

        if (hasTypes)
        {
            omitted.Add("types");
        }

        if (hasGenerators)
        {
            omitted.Add("generator");
        }

        if (hasConstraints)
        {
            omitted.Add("constraints");
        }

        return omitted;
    }

    private static string Source(
        string name,
        string className,
        string? namespaceName,
        IReadOnlyList<ConceptDeclaration> declarations,
        IReadOnlyList<Notation> notations,
        List<string> omitted)
    {
        var source = new SourceWriter();
        var stubs = omitted.Contains("types") || omitted.Contains("generator");

        source.Line("// <auto-generated>");
        source.Line($"//     Generated from the declared language '{name}'.");
        source.Line("//     Edit the definitions, not this file: the next export replaces it.");
        source.Line("// </auto-generated>");
        source.Blank();

        if (stubs)
        {
            source.Line("using System;");
        }

        source.Line("using System.Collections.Generic;");
        source.Line();
        source.Line("using Projectional.Core;");

        if (notations.Count > 0 || stubs)
        {
            source.Line("using Projectional.Language;");
        }

        source.Blank();

        if (!string.IsNullOrWhiteSpace(namespaceName))
        {
            source.Line($"namespace {namespaceName};");
            source.Blank();
        }

        source.Line($"/// <summary>The language <c>{name}</c> declares, as C#.</summary>");
        source.Line($"public static class {className}");
        source.Line("{");

        using (source.Indent())
        {
            DeclarationWriter.Write(source, declarations);

            if (notations.Count > 0)
            {
                source.Blank();
                Notations(source, notations);
            }

            if (omitted.Contains("types"))
            {
                source.Blank();
                Stub(source, "TypeRegistry", "Types", "types", "type rules");
            }

            if (omitted.Contains("generator"))
            {
                source.Blank();
                Stub(source, "IReadOnlyList<TextGenerator>", "Generators", "generator", "text generators");
            }
        }

        source.Line("}");

        return source.ToString();
    }

    private static void Notations(SourceWriter source, IReadOnlyList<Notation> notations)
    {
        source.Line("/// <summary>How those concepts are drawn.</summary>");
        source.Line("public static NotationRegistry CreateNotations(ConceptRegistry concepts)");
        source.Line("{");

        using (source.Indent())
        {
            source.Line("var notations = new NotationRegistry(concepts);");
            source.Blank();
            NotationWriter.Write(source, notations);
            source.Blank();
            source.Line("return notations;");
        }

        source.Line("}");
    }

    /// <summary>Names an aspect this export cannot write, so a caller cannot mistake the language for complete.</summary>
    /// <remarks>
    /// The member throws instead of answering an empty registry: an empty <c>Types</c> is the honest state of a
    /// language that declared none, and a generated language that answered it after dropping its rules would be
    /// silently wrong. The message names the folder to look in.
    /// </remarks>
    private static void Stub(SourceWriter source, string type, string member, string folder, string what)
    {
        source.Line($"/// <summary>The declared {what}, which this export cannot write yet.</summary>");
        source.Line($"public static {type} {member} =>");

        using (source.Indent())
        {
            source.Line(
                $"throw new NotSupportedException(\"The {what} declared in '{folder}' have no export spelling yet; write them in a behaviour package beside this language.\");");
        }
    }

    private static string Readme(string name, string className, bool hasNotations, List<string> omitted)
    {
        var lines = new List<string>
        {
            $"# {name}",
            string.Empty,
            $"`{className}.cs` is **generated**. Edit the definitions, not the file: the next",
            "export replaces it.",
            string.Empty,
            "## What it is",
            string.Empty,
            $"The concepts of the declared language{(hasNotations ? " and its notation" : string.Empty)},",
            "written as the C# a language package is made of.",
            string.Empty,
            $"`{className}.ConceptDeclarations` is what the structure documents declared, in their",
            "order - that order is the language's answer to what a user most likely wants in an",
            "empty slot, so it is not sorted.",
        };

        if (hasNotations)
        {
            lines.Add(string.Empty);
            lines.Add($"`{className}.CreateNotations` is the notation for those concepts. It takes the");
            lines.Add("registry the declarations build, because a notation naming a concept that was never");
            lines.Add("declared is caught when the two halves first meet.");
            lines.Add(string.Empty);
            lines.Add("```csharp");
            lines.Add($"var concepts = ConceptRegistry.Create({className}.ConceptDeclarations);");
            lines.Add($"var notations = {className}.CreateNotations(concepts);");
            lines.Add("```");
            lines.Add(string.Empty);
            lines.Add("A host that already has a language composes the two by concatenating the declarations,");
            lines.Add($"`[.. Own.ConceptDeclarations, .. {className}.ConceptDeclarations]`, and asks the");
            lines.Add("notation registry it built for both. A concept key belongs to the language, not to");
            lines.Add("the document.");
        }
        else
        {
            lines.Add(string.Empty);
            lines.Add("The definitions carry no notation yet, so every node of this language draws as a");
            lines.Add("missing-notation cell. That is the state a language is in while it is being written,");
            lines.Add("not a defect in the export.");
        }

        lines.Add(string.Empty);
        lines.Add("## What the export does not write");
        lines.Add(string.Empty);

        if (omitted.Count == 0)
        {
            lines.Add("Nothing. Every aspect this language declares is in the generated file.");
        }
        else
        {
            lines.Add("The declared aspect language can say things this export has no spelling for. They are");
            lines.Add("left out rather than guessed:");
            lines.Add(string.Empty);
            lines.Add("| Folder | Why it is not in the file |");
            lines.Add("| --- | --- |");

            foreach (var folder in omitted)
            {
                lines.Add($"| `{folder}/` | {ReasonFor(folder)} |");
            }

            var stubbed = omitted
                .Where(folder => folder is not "constraints")
                .Select(folder => folder == "types" ? "`Types`" : "`Generators`")
                .ToList();

            if (stubbed.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add($"{string.Join(" and ", stubbed)} throw `NotSupportedException` rather than answer an");
                lines.Add("empty registry, so nothing reads a half-exported aspect by accident. Write what they");
                lines.Add("would answer in a package beside this language.");
            }

            if (omitted.Contains("constraints"))
            {
                lines.Add(string.Empty);
                lines.Add("A rule cannot be stubbed the same way: a `ConstraintSet` lives in");
                lines.Add("`Projectional.Semantics`, which the toolkit deliberately does not aggregate, so a");
                lines.Add("language that declares one needs a behaviour package beside it.");
            }
        }

        return string.Join('\n', lines) + "\n";
    }

    private static string ReasonFor(string folder) => folder switch
    {
        "types" => "a type rule answers one question per node - behaviour, not data",
        "generator" => "a template is behaviour, not data",
        _ => "a rule reports and never repairs, so it belongs beside the editor",
    };

    /// <summary>The namespace an exported language declares by default: its own name as one Pascal-cased word.</summary>
    /// <param name="name">The language's name, which is the name of its folder.</param>
    /// <remarks>
    /// A host has no way to ask a user for a namespace in a folder picker, and a generated class in the global
    /// namespace is awkward to reference from a package. The language's own name is the only identity an export has,
    /// so it is also the namespace: <c>declared-pipeline</c> becomes <c>DeclaredPipeline</c>.
    /// </remarks>
    public static string NamespaceFor(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return PascalCase(name);
    }

    /// <summary>The type name for a language: the same identifier, suffixed so it reads as a language.</summary>
    private static string ClassName(string name) => PascalCase(name) + "Language";

    /// <summary>An identifier for <paramref name="name"/>: one word per run of letters and digits, capitalised.</summary>
    private static string PascalCase(string name)
    {
        var builder = new StringBuilder(name.Length + 1);

        var capitalise = true;

        foreach (var character in name)
        {
            if (!char.IsLetterOrDigit(character))
            {
                capitalise = true;

                continue;
            }

            builder.Append(capitalise ? char.ToUpperInvariant(character) : character);
            capitalise = false;
        }

        if (builder.Length == 0)
        {
            builder.Append("Generated");
        }
        else if (char.IsDigit(builder[0]))
        {
            // A language folder may begin with a digit; an identifier may not.
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }
}
