using System.Text;

using Projectional.Behaviours;
using Projectional.Core;
using Projectional.CSharp;
using Projectional.CSharp.Generation;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Semantics;
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
/// An aspect the declared path has but this export cannot spell - constraints, scopes, intentions, import - is named in
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

    /// <summary>The metadata key holding what the root says the language is for.</summary>
    public const string DescriptionKey = "Projectional.LanguageDescription";

    /// <summary>The metadata key naming one language it builds on, repeated once per dependency.</summary>
    public const string DependsOnKey = "Projectional.DependsOn";

    /// <summary>The metadata key naming one language whose behaviour it inherits, repeated once per extension.</summary>
    public const string ExtendsKey = "Projectional.Extends";

    /// <summary>Exports a language project, asking it which aspects it declares, and everything it stands on.</summary>
    /// <param name="language">The language a user opened.</param>
    /// <param name="namespaceName">The namespace to declare for it, or <c>null</c> to declare none.</param>
    /// <param name="output">Whether to compile the generated sources into assemblies as well.</param>
    /// <returns>
    /// One source per language - the ones it stands on first, base before dependent - and the README of
    /// <paramref name="language"/> last; the same languages as assemblies when <paramref name="output"/> asks for them.
    /// </returns>
    /// <exception cref="ExportCompilationException">The generated source of one language does not compile.</exception>
    /// <remarks>
    /// A language is shipped as one package per language, so the languages it was resolved against are exported too:
    /// <c>declared-invoice</c> extends <c>Units</c>, and a folder holding only the dependent's file would be a package
    /// whose base the caller has to find elsewhere. A language that ships with the framework is skipped - it already
    /// is a package, and it has no folder of a user's to write - which is the same line the shell draws when it offers
    /// export for the languages opened from a folder.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="language"/> is <see langword="null"/>.</exception>
    public static ExportResult Export(
        LanguageProject language,
        string? namespaceName = null,
        ExportOutput output = ExportOutput.Source,
        AssemblyLayout layout = AssemblyLayout.PerLanguage)
    {
        ArgumentNullException.ThrowIfNull(language);

        var files = new List<GeneratedFile>();
        var beside = new List<string>();
        var sources = new List<Built>();

        foreach (var basis in language.Resolved)
        {
            if (basis.Directory.Length == 0)
            {
                continue;
            }

            var exported = Build(
                basis.Name,
                basis.Declarations,
                Bodies(basis),
                basis.Notations.Notations,
                NamespaceFor(basis.Name),
                basis.Types,
                basis.Generators,
                basis.HasConstraints,
                basis.HasScopes,
                basis.HasIntentions,
                basis.DeclaredTypingRules,
                basis.HasDeclaredImporters,
                Metadata(basis),

                // One assembly can hold one informational version, and it is the exported language's: a dependency's
                // own version still travels as AssemblyMetadata, which allows the repetition.
                informationalVersion: layout == AssemblyLayout.PerLanguage,
                readme: false,
                beside: null,
                nameMethods: NameMethods(basis),
                behaviours: Behaviours(basis));

            files.AddRange(exported.Files);
            beside.Add(exported.Stem);
            sources.Add(exported);
        }

        var own = Build(
            language.Name,
            language.Declarations,
            Bodies(language),
            language.Notations.Notations,
            namespaceName,
            language.Types,
            language.Generators,
            language.HasConstraints,
            language.HasScopes,
            language.HasIntentions,
            language.DeclaredTypingRules,
            language.HasDeclaredImporters,
            Metadata(language),
            informationalVersion: true,
            readme: true,
            beside,
            nameMethods: NameMethods(language),
            behaviours: Behaviours(language));

        files.AddRange(own.Files);
        sources.Add(own);

        var assemblies = output != ExportOutput.Assembly
            ? []
            : layout == AssemblyLayout.Combined
                ? [AssemblyCompiler.Compile(own.Stem, [.. sources.SelectMany(source => source.Sources)])]
                : sources.Select(source => AssemblyCompiler.Compile(source.Stem, source.Sources)).ToList();

        return new ExportResult(files, assemblies);
    }

    /// <summary>The name body each concept of <paramref name="language"/> declares, by concept key.</summary>
    /// <returns>The bodies, or <see langword="null"/> when the language declares no behaviour at all.</returns>
    /// <remarks>
    /// Lowered again from the documents rather than read off the declarations, because a declaration carries the
    /// delegate and a delegate cannot be written out: the vocabulary record is the one form that both runs and can be
    /// spelled as C#. Re-lowering is also what the folder itself does, so a language whose behaviour does not lower is
    /// already refused long before an export reaches it.
    /// </remarks>
    private static IReadOnlyDictionary<string, NameBody>? Bodies(LanguageProject language) =>
        language.BehaviourDocuments.Count == 0
            ? null
            : BehaviourLowering.Bodies(language.BehaviourDocuments.SelectMany(model => model.Roots), language.Concepts);

    /// <summary>The C# method that names each concept of <paramref name="language"/>, by concept key.</summary>
    /// <returns>The methods, or <see langword="null"/> when no concept is named in C#.</returns>
    /// <remarks>
    /// Read off the language rather than re-derived, because it *is* a name and not a body: the folder bound the class
    /// to the concept when it loaded, and re-walking the models would be a second answer to the same question.
    /// </remarks>
    private static IReadOnlyDictionary<string, string>? NameMethods(LanguageProject language) =>
        language.CSharpNames.Count == 0 ? null : language.CSharpNames;

    /// <summary>
    /// The C# a language writes its behaviours in, generated as the package's own source: the documents are models of
    /// the C# base language, so the base generator turns them back into the code a compiler reads.
    /// </summary>
    /// <remarks>
    /// These are the files the emitted <c>nameOf: …</c> refers to. They are generated rather than copied because the
    /// model is the truth: a host that edits the behaviour in the editor and copies the old text into the package would
    /// be shipping a second answer.
    /// </remarks>
    private static IReadOnlyList<string> Behaviours(LanguageProject language)
    {
        if (language.CSharpDocuments.Count == 0)
        {
            return [];
        }

        var generator = new Generator();

        return
        [
            .. language.CSharpDocuments.Select(document =>
                string.Concat(document.Roots.Select(root => generator.Generate(root, CSharpTextGenerators.Generator).Text))),
        ];
    }

    /// <summary>What the root says about the language, in the shape the writer states it with.</summary>
    private static LanguageMetadata Metadata(LanguageProject language) =>        new(
            language.Version,
            language.Description,
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
    /// <param name="types">The type rules and edges the language declares, written out in full.</param>
    /// <param name="generators">The text generators it declares, which this export writes out in full.</param>
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
        TypeRegistry? types = null,
        IReadOnlyList<TextGenerator>? generators = null,
        bool hasConstraints = false,
        bool hasScopes = false,
        bool hasIntentions = false,
        IReadOnlyList<DeclaredTypingRule>? typingRules = null,
        bool hasImport = false,
        ExportOutput output = ExportOutput.Source,
        LanguageMetadata? metadata = null,
        IReadOnlyDictionary<string, NameBody>? names = null)
    {
        var built = Build(
            name,
            declarations,
            names,
            notations,
            namespaceName,
            types ?? TypeRegistry.Empty,
            generators,
            hasConstraints,
            hasScopes,
            hasIntentions,
            typingRules,
            hasImport,
            metadata,
            informationalVersion: true,
            readme: true,
            beside: null);

        return new ExportResult(
            built.Files,
            output == ExportOutput.Assembly ? [AssemblyCompiler.Compile(built.Stem, built.Sources)] : []);
    }

    /// <summary>One language as files, as source text, and under the name its files carry.</summary>
    /// <param name="Files">The source files and, when asked for, the README.</param>
    /// <param name="Stem">The file name without extension: the class name and the declared version.</param>
    /// <param name="Sources">
    /// The generated source, which an assembly layout may compile together with another's - the declaration file first,
    /// then one per C# behaviour document, because they are one compilation and a file is a unit with its own usings.
    /// </param>
    private sealed record Built(IReadOnlyList<GeneratedFile> Files, string Stem, IReadOnlyList<string> Sources);

    /// <summary>Builds one language's source and, on request, its README.</summary>
    /// <param name="informationalVersion">
    /// Whether the source states the declared version as the assembly's informational version. One assembly can hold
    /// one such attribute, so a combined layout writes it for the exported language alone.
    /// </param>
    /// <param name="readme">
    /// Whether this language's README is part of the answer. A language the exported one stands on is a package of its
    /// own, but only the exported one documents the folder, so its neighbours are named in one README rather than one
    /// each - two <c>README.md</c> in one directory would be one file anyway.
    /// </param>
    /// <param name="beside">
    /// The file names, without extension, exported beside this one - each a language and its declared version - named in
    /// the README, or <see langword="null"/> when this file is alone.
    /// </param>
    private static Built Build(
        string name,
        IReadOnlyList<ConceptDeclaration> declarations,
        IReadOnlyDictionary<string, NameBody>? names,
        IReadOnlyList<Notation> notations,
        string? namespaceName,
        TypeRegistry types,
        IReadOnlyList<TextGenerator>? generators,
        bool hasConstraints,
        bool hasScopes,
        bool hasIntentions,
        IReadOnlyList<DeclaredTypingRule>? typingRules,
        bool hasImport,
        LanguageMetadata? metadata,
        bool informationalVersion,
        bool readme,
        IReadOnlyList<string>? beside,
        IReadOnlyDictionary<string, string>? nameMethods = null,
        IReadOnlyList<string>? behaviours = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(notations);

        var typing = typingRules ?? [];
        var className = ClassName(name);
        var stem = className + VersionSuffix(metadata?.Version);
        var omitted = Omissions(hasConstraints, hasScopes, hasIntentions, hasImport);
        var source = Source(name, className, namespaceName, declarations, names, notations, generators ?? [], typing, types, omitted, metadata, informationalVersion, nameMethods);

        var files = new List<GeneratedFile>(3) { new($"{stem}.cs", source) };

        // The C# a language writes its behaviours in: beside the declaration file and compiled with it, because the
        // declaration names the methods it holds. One document is the ordinary case and takes the plain name; a second
        // can only be told apart by a number, and the order is the folder's, so the names are stable between exports.
        if (behaviours is { Count: > 0 })
        {
            for (var index = 0; index < behaviours.Count; index++)
            {
                var file = behaviours.Count == 1
                    ? $"{stem}.Behaviour.cs"
                    : $"{stem}.Behaviour.{index + 1}.cs";

                files.Add(new GeneratedFile(file, behaviours[index]));
            }
        }

        if (readme)
        {
            files.Add(new GeneratedFile(
                "README.md",
                Readme(name, className, notations.Count > 0, typing.Count > 0, omitted, metadata, beside)));
        }

        return new Built(files, stem, [source, .. behaviours ?? []]);
    }

    /// <summary>The declared version as a file-name suffix: <c>-1.0.0</c>, or nothing when none is declared.</summary>
    /// <param name="version">The version the root spelled, or <see langword="null"/>.</param>
    /// <returns>
    /// The suffix, including its leading dash, or the empty string. A version is free text, so whatever a file name
    /// cannot hold becomes a dash - the same answer the writer gives a document whose name holds a slash.
    /// </returns>
    /// <remarks>
    /// The suffix goes on the file and on the assembly, never on the class: a package's identity in C# is its type
    /// name, and a type that changed name with every release would be a package nobody could upgrade.
    /// </remarks>
    public static string VersionSuffix(string? version)
    {
        if (version is not { Length: > 0 } declared)
        {
            return string.Empty;
        }

        var safe = new StringBuilder(declared.Length + 1).Append('-');

        foreach (var character in declared)
        {
            safe.Append(char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '-');
        }

        return safe.ToString();
    }

    private static List<string> Omissions(
        bool hasConstraints,
        bool hasScopes,
        bool hasIntentions,
        bool hasImport)
    {
        var omitted = new List<string>();


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
        IReadOnlyDictionary<string, NameBody>? names,
        IReadOnlyList<Notation> notations,
        IReadOnlyList<TextGenerator> generators,
        IReadOnlyList<DeclaredTypingRule> typingRules,
        TypeRegistry types,
        List<string> omitted,
        LanguageMetadata? metadata,
        bool informationalVersion,
        IReadOnlyDictionary<string, string>? nameMethods = null)
    {
        var source = new SourceWriter();
        var typing = typingRules.Count > 0;

        source.Line("// <auto-generated>");
        source.Line($"//     Generated from the declared language '{name}'.");
        source.Line("//     Edit the definitions, not this file: the next export replaces it.");
        source.Line("// </auto-generated>");
        source.Blank();

        if (TypingWriter.NeedsSystem(typingRules))
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

        if (notations.Count > 0 || types.Count > 0 || typing || names is { Count: > 0 })
        {
            source.Line("using Projectional.Language;");
        }

        source.Blank();

        if (metadata is not null)
        {
            AssemblyMetadata(source, name, metadata, informationalVersion);
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
            DeclarationWriter.Write(source, declarations, names, nameMethods);

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

            if (generators.Count > 0)
            {
                source.Blank();
                Generators(source, generators);
            }

            if (types.Count > 0)
            {
                source.Blank();
                TypeWriter.Write(source, types);
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
    private static void AssemblyMetadata(SourceWriter source, string name, LanguageMetadata metadata, bool informationalVersion)
    {
        MetadataAttribute(source, LanguageKey, name);

        if (metadata.Description is { Length: > 0 } description)
        {
            MetadataAttribute(source, DescriptionKey, description);
        }

        if (metadata.Version is { Length: > 0 } version)
        {
            MetadataAttribute(source, VersionKey, version);

            if (informationalVersion)
            {
                source.Line($"[assembly: System.Reflection.AssemblyInformationalVersion({SourceWriter.Literal(version)})]");
            }
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

    /// <summary>Writes the generators a language declares as the data they are.</summary>
    /// <remarks>
    /// A generator is a name and a template per concept, and every part a template holds is a record in
    /// <c>Projectional.Language</c> - which is why this aspect is the one behavioural aspect an export can write as
    /// code rather than describe: nothing here is a lambda. The templates are written in key order so two exports of
    /// one language are the same bytes.
    /// </remarks>
    private static void Generators(SourceWriter source, IReadOnlyList<TextGenerator> generators)
    {
        source.Line("/// <summary>How those models become text: one generator per target.</summary>");
        source.Line("public static IReadOnlyList<TextGenerator> Generators { get; } =");
        source.Line("[");

        using (source.Indent())
        {
            foreach (var generator in generators)
            {
                source.Line($"new TextGenerator({SourceWriter.Literal(generator.Name)}, TemplateRegistry.Empty");

                using (source.Indent())
                {
                    foreach (var template in generator.Templates.Templates.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                    {
                        source.Line($".Add({SourceWriter.Literal(template.Key)}, {TemplateText(template.Value)})");
                    }
                }

                source.Line("),");
            }
        }

        source.Line("];");
    }

    /// <summary>A template as one C# expression, nested parts included.</summary>
    private static string TemplateText(TextTemplate template) =>
        template.Parts.Count == 0
            ? "TextTemplate.Empty"
            : $"new TextTemplate([{string.Join(", ", template.Parts.Select(PartText))}])";

    /// <summary>One part as a C# expression.</summary>
    /// <exception cref="ExportCompilationException">The part has no export spelling.</exception>
    private static string PartText(TemplatePart part) => part switch
    {
        TextPart text => $"new TextPart({SourceWriter.Literal(text.Text)})",
        PropertyPart property => $"new PropertyPart({SourceWriter.Literal(property.Slot)})",
        ChildPart child => $"new ChildPart({SourceWriter.Literal(child.Slot)})",
        ParentPart parent => $"new ParentPart({SourceWriter.Literal(parent.Slot)})",
        SelfPart => "SelfPart.Instance",
        LineBreakPart => "LineBreakPart.Instance",
        ReferencePart reference =>
            $"new ReferencePart({SourceWriter.Literal(reference.Slot)}, {SourceWriter.Literal(reference.UnsetText)}, {SourceWriter.Literal(reference.UnresolvedText)})",
        ChildrenPart children => ChildrenText(children),
        LoopPart loop => $"new LoopPart({SourceWriter.Literal(loop.Slot)}, {TemplateText(loop.Body)}{Optional(loop.Separator)})",
        IndentPart indent => $"new IndentPart({TemplateText(indent.Body)})",
        ConditionalPart conditional =>
            $"new ConditionalPart({SourceWriter.Literal(conditional.Slot)}, {SourceWriter.Literal(conditional.Value)}, {TemplateText(conditional.Then)}{Optional(conditional.Otherwise)})",
        WhenFilledPart filled =>
            $"new WhenFilledPart({SourceWriter.Literal(filled.Slot)}, {TemplateText(filled.Then)}{Optional(filled.Otherwise)})",

        // A part the template language gains later must be a refusal, not a silently missing piece of a generated
        // file: a generator that dropped a part would print something other than what the language says.
        _ => throw new ExportCompilationException(
            $"'{part.GetType().Name}' has no export spelling yet, so the generator cannot be written as C#.",
            [$"The template part '{part.GetType().Name}' is not known to the export."]),
    };

    /// <summary>A separator or else-branch as a trailing argument, or nothing when it is absent.</summary>
    private static string Optional(TextTemplate? template) =>
        template is null || template.IsEmpty ? string.Empty : ", " + TemplateText(template);

    private static string ChildrenText(ChildrenPart children)
    {
        var arguments = new List<string> { SourceWriter.Literal(children.Slot) };

        if (children.Gaps != ListGap.None || children.TightConcepts.Count > 0)
        {
            arguments.Add(children.Separator.IsEmpty ? "null" : TemplateText(children.Separator));
        }

        if (children.Gaps != ListGap.None || children.TightConcepts.Count > 0)
        {
            arguments.Add($"ListGap.{children.Gaps}");
        }

        if (children.TightConcepts.Count > 0)
        {
            arguments.Add(
                $"new HashSet<string> {{ {string.Join(", ", children.TightConcepts.OrderBy(key => key, StringComparer.Ordinal).Select(SourceWriter.Literal))} }}");
        }

        return $"new ChildrenPart({string.Join(", ", arguments)})";
    }

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

    private static string Readme(
        string name,
        string className,
        bool hasNotations,
        bool hasTyping,
        List<string> omitted,
        LanguageMetadata? metadata,
        IReadOnlyList<string>? beside)
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

            if (metadata.Description is { Length: > 0 } description)
            {
                lines.Add($"| `{DescriptionKey}` | {description} |");
            }

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

        if (beside is { Count: > 0 })
        {
            lines.Add(string.Empty);
            lines.Add("## What else is in this folder");
            lines.Add(string.Empty);
            lines.Add("A language ships as one package per language, so the languages this one stands on are");
            lines.Add("exported beside it, base first, each with its own source and - when the export compiled one -");
            lines.Add("its own assembly:");
            lines.Add(string.Empty);

            foreach (var file in beside)
            {
                lines.Add($"- `{file}.cs` and, when the export compiled one, `{file}.dll`");
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

            // Nothing is stubbed any more: types and generators are written as code, and what is left - constraints,
            // scopes, intentions, import - lives in an assembly the toolkit deliberately does not carry, so a stub
            // could not even name its type.
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
