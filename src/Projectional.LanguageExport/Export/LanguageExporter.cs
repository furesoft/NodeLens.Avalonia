using System.Text;

using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Structures;
using Projectional.TypingRules;

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
    /// <summary>The metadata key naming the language, as its root declares it.</summary>
    public const string LanguageKey = "Projectional.Language";

    /// <summary>The metadata key holding the version the root declares.</summary>
    public const string VersionKey = "Projectional.LanguageVersion";

    /// <summary>The metadata key naming one language it builds on, repeated once per dependency.</summary>
    public const string DependsOnKey = "Projectional.DependsOn";

    /// <summary>The metadata key naming one language whose behaviour it inherits, repeated once per extension.</summary>
    public const string ExtendsKey = "Projectional.Extends";

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
            language.HasScopes,
            language.HasIntentions,
            language.DeclaredTypingRules,
            language.HasDeclaredImporters,
            output,
            Metadata(language));
    }

    /// <summary>What the root says about the language, in the shape the writer states it with.</summary>
    private static LanguageMetadata Metadata(LanguageProject language) =>
        new(
            language.Version,
            [.. language.Dependencies.Select(Dependency)],
            [.. language.Extends.Select(Dependency)]);

    /// <summary>One declared language as a metadata value: <c>Units</c>, or <c>Units 1.0.0</c> when it asked for one.</summary>
    private static string Dependency(LanguageDependency dependency) =>
        dependency.Version is null ? dependency.Name : $"{dependency.Name} {dependency.Version}";

    /// <summary>Builds the generated file and its README, in that order, and compiles the source on request.</summary>
    /// <param name="name">The language's name, which decides the class name and the file name.</param>
    /// <param name="declarations">What the structure documents declared, in declaration order.</param>
    /// <param name="notations">The notation they are drawn with, in registration order.</param>
    /// <param name="namespaceName">The namespace to declare, or <c>null</c> to declare none.</param>
    /// <param name="hasTypes">Whether the language declares type rules this export cannot write.</param>
    /// <param name="hasGenerators">Whether it declares text generators this export cannot write.</param>
    /// <param name="hasConstraints">Whether it declares constraints this export cannot write.</param>
    /// <param name="hasScopes">Whether it declares scope rules this export cannot write.</param>
    /// <param name="hasIntentions">Whether it declares intentions this export cannot write.</param>
    /// <param name="typingRules">The typing rules it declares, which this export writes out in full.</param>
    /// <param name="hasImport">Whether it declares an importer this export cannot write.</param>
    /// <param name="output">Whether to compile the generated source into an assembly as well.</param>
    /// <param name="metadata">
    /// What the language's root says about it - its version and the languages it stands on - written into the source as
    /// assembly attributes, or <see langword="null"/> to write none.
    /// </param>
    /// <exception cref="ExportCompilationException">The generated source does not compile.</exception>
    public static ExportResult Export(
        string name,
        IReadOnlyList<ConceptDeclaration> declarations,
        IReadOnlyList<Notation> notations,
        string? namespaceName = null,
        bool hasTypes = false,
        bool hasGenerators = false,
        bool hasConstraints = false,
        bool hasScopes = false,
        bool hasIntentions = false,
        IReadOnlyList<DeclaredTypingRule>? typingRules = null,
        bool hasImport = false,
        ExportOutput output = ExportOutput.Source,
        LanguageMetadata? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(notations);

        var typing = typingRules ?? [];
        var className = ClassName(name);
        var omitted = Omissions(hasTypes, hasGenerators, hasConstraints, hasScopes, hasIntentions, hasImport);
        var source = Source(name, className, namespaceName, declarations, notations, typing, omitted, metadata);

        return new ExportResult(
            [
                new GeneratedFile($"{className}.cs", source),
                new GeneratedFile("README.md", Readme(name, className, notations.Count > 0, typing.Count > 0, omitted, metadata)),
            ],
            output == ExportOutput.Assembly ? AssemblyCompiler.Compile(className, source) : null);
    }

    private static List<string> Omissions(
        bool hasTypes,
        bool hasGenerators,
        bool hasConstraints,
        bool hasScopes,
        bool hasIntentions,
        bool hasImport)
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

        if (hasScopes)
        {
            omitted.Add("scopes");
        }

        if (hasIntentions)
        {
            omitted.Add("intentions");
        }

        if (hasImport)
        {
            omitted.Add("import");
        }

        return omitted;
    }

    private static string Source(
        string name,
        string className,
        string? namespaceName,
        IReadOnlyList<ConceptDeclaration> declarations,
        IReadOnlyList<Notation> notations,
        IReadOnlyList<DeclaredTypingRule> typingRules,
        List<string> omitted,
        LanguageMetadata? metadata)
    {
        var source = new SourceWriter();
        var stubs = omitted.Contains("types") || omitted.Contains("generator");
        var typing = typingRules.Count > 0;

        source.Line("// <auto-generated>");
        source.Line($"//     Generated from the declared language '{name}'.");
        source.Line("//     Edit the definitions, not this file: the next export replaces it.");
        source.Line("// </auto-generated>");
        source.Blank();

        if (stubs || TypingWriter.NeedsSystem(typingRules))
        {
            source.Line("using System;");
        }

        source.Line("using System.Collections.Generic;");

        if (TypingWriter.NeedsLinq(typingRules))
        {
            source.Line("using System.Linq;");
        }

        source.Line();
        source.Line("using Projectional.Core;");

        if (notations.Count > 0 || stubs || typing)
        {
            source.Line("using Projectional.Language;");
        }

        source.Blank();

        if (metadata is not null)
        {
            AssemblyMetadata(source, name, metadata);
            source.Blank();
        }

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

            if (typingRules.Count > 0)
            {
                source.Blank();
                TypingWriter.Write(source, typingRules);
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

    /// <summary>States the language's own identity, which travels with the assembly the source compiles to.</summary>
    /// <remarks>
    /// <c>AssemblyMetadata</c> rather than <c>AssemblyTitle</c> and friends: what a root declares has no standard
    /// attribute of its own, and a key/value pair is what a host can read without knowing this exporter. The one
    /// exception is the version, which is also written as the informational version - the single free-form version
    /// field an assembly has. The assembly version is left alone, because it is four numbers and nothing here parses a
    /// version a user typed.
    /// </remarks>
    private static void AssemblyMetadata(SourceWriter source, string name, LanguageMetadata metadata)
    {
        MetadataAttribute(source, LanguageKey, name);

        if (metadata.Version is { Length: > 0 } version)
        {
            MetadataAttribute(source, VersionKey, version);
            source.Line($"[assembly: System.Reflection.AssemblyInformationalVersion({SourceWriter.Literal(version)})]");
        }

        foreach (var dependency in metadata.DependsOn)
        {
            MetadataAttribute(source, DependsOnKey, dependency);
        }

        foreach (var inherited in metadata.Extends)
        {
            MetadataAttribute(source, ExtendsKey, inherited);
        }
    }

    private static void MetadataAttribute(SourceWriter source, string key, string value) =>
        source.Line(
            $"[assembly: System.Reflection.AssemblyMetadata({SourceWriter.Literal(key)}, {SourceWriter.Literal(value)})]");

    private static void Notations(SourceWriter source, IReadOnlyList<Notation> notations)    {
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
                $"throw new NotSupportedException(\"The {what} declared in '{folder}' have no export spelling yet; write them in this project's `Checks/` or `Intentions/` folder.\");");
        }
    }

    private static string Readme(string name, string className, bool hasNotations, bool hasTyping, List<string> omitted, LanguageMetadata? metadata)
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
            $"written as the C# a language package is made of{(hasTyping ? ", with the typing rules it declares" : string.Empty)}.",
            string.Empty,
            $"`{className}.ConceptDeclarations` is what the structure documents declared, in their",
            "order - that order is the language's answer to what a user most likely wants in an",
            "empty slot, so it is not sorted.",
        };

        if (metadata is not null)
        {
            lines.Add(string.Empty);
            lines.Add("## What the assembly says");
            lines.Add(string.Empty);
            lines.Add("The language's own identity - what `language.mps` declares - travels with the binary as assembly");
            lines.Add("metadata, so a host can read what a package is without opening the language:");
            lines.Add(string.Empty);
            lines.Add("| Key | Value |");
            lines.Add("| --- | --- |");
            lines.Add($"| `{LanguageKey}` | `{name}` |");

            if (metadata.Version is { Length: > 0 } version)
            {
                lines.Add($"| `{VersionKey}` | `{version}` |");
            }

            foreach (var dependency in metadata.DependsOn)
            {
                lines.Add($"| `{DependsOnKey}` | `{dependency}` |");
            }

            foreach (var inherited in metadata.Extends)
            {
                lines.Add($"| `{ExtendsKey}` | `{inherited}` |");
            }

            if (metadata.Version is { Length: > 0 })
            {
                lines.Add(string.Empty);
                lines.Add("A declared version is a label: it becomes the assembly's informational version, the one");
                lines.Add("free-form version field there is, and never its assembly version - that field is four");
                lines.Add("numbers, and nothing in the framework parses a version an author typed.");
            }
        }

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

        if (hasTyping)
        {
            lines.Add(string.Empty);
            lines.Add($"`{className}.CreateTypingRules` says which node a burst of typing into an empty slot");
            lines.Add("creates. It is written out in full rather than named below, because typing is one of the");
            lines.Add("aspects this export can spell: the registry a rule builds lives in `Projectional.Language`,");
            lines.Add("inside the toolkit a generated package references, and the four claims the declared");
            lines.Add("language offers are exactly the predicates a hand-written language writes.");
            lines.Add(string.Empty);
            lines.Add("```csharp");
            lines.Add($"var typing = {className}.CreateTypingRules();");
            lines.Add("```");
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
                .Where(folder => folder is "types" or "generator")
                .Select(folder => folder == "types" ? "`Types`" : "`Generators`")
                .ToList();

            if (stubbed.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add($"{string.Join(" and ", stubbed)} throw `NotSupportedException` rather than answer an");
                lines.Add("empty registry, so nothing reads a half-exported aspect by accident. Write what they");
                lines.Add("would answer in a folder of this project - `Checks/` or `Intentions/`, the shape");
                lines.Add("a language package uses: one project per language, with its behaviour inside it.");
            }

            var unstubbable = omitted.Where(folder => folder is "constraints" or "scopes" or "intentions" or "import").ToList();

            if (unstubbable.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("These cannot be stubbed: they live in an assembly the toolkit deliberately does");
                lines.Add("not carry - constraints and scopes in `Projectional.Semantics`, intentions in");
                lines.Add("`Projectional.Editing`, importers in `Projectional.ImportRules` - so write them in");
                lines.Add("this project's `Checks/` or `Intentions/` folder, or keep this part declared.");
            }
        }

        return string.Join('\n', lines) + "\n";
    }

    private static string ReasonFor(string folder) => folder switch
    {
        "types" => "a type rule answers one question per node - behaviour, not data",
        "generator" => "a template is behaviour, not data",
        "scopes" => "a scope rule says where names come from - behaviour beside the editor",
        "intentions" => "an intention produces an edit - behaviour beside the editor",
        "import" => "an importer is a rule set written in `Projectional.ImportRules`, outside the toolkit",
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
