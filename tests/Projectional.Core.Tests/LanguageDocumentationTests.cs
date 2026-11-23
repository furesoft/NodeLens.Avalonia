using System.IO.Compression;
using System.Text.RegularExpressions;

using Projectional.Core;
using Projectional.CSharp;
using Projectional.CSharp.Checks;
using Projectional.CSharp.Generation;
using Projectional.CSharp.Intentions;
using Projectional.CSharp.Notation;
using Projectional.Expressions;
using Projectional.Expressions.Intentions;
using Projectional.LanguageDocs;
using Projectional.LanguageExport;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Structures;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The interactive documentation: a folder of static pages, one per concept, covering the language and everything it
/// stands on.
/// </summary>
/// <remarks>
/// The tests are written as a reader's checks rather than a renderer's: every concept has a page, every link on every
/// page points at a file that exists, and the dependency the sample declares is named on the root page and reachable
/// from the concept that uses it. A page that passes those is a page a reader can walk.
/// </remarks>
public sealed class LanguageDocumentationTests
{
    [Fact]
    public void EveryConceptOfTheChainHasItsOwnPage()
    {
        var language = Atlas();
        var pages = LanguageDocumentation.Generate(language);

        foreach (var each in language.Resolved.Append(language))
        {
            var folder = DocumentationChain.Folder(each);

            Assert.Contains(pages, page => page.Path == $"{folder}/index.html");
            Assert.Contains(pages, page => page.Path == $"{folder}/aspects.html");

            foreach (var declaration in each.Declarations)
            {
                Assert.Contains(pages, page => page.Path == $"{folder}/concepts/{declaration.Key}.html");
            }
        }

        // What a folder documents is what its own `structure/` declares, not the vocabulary it can see: `Units.Quantity`
        // is in Atlas' registry because Atlas stands on Units, and documenting it in both folders would be two pages
        // that can disagree.
        Assert.DoesNotContain(pages, page => page.Path == "Atlas/concepts/Units.Quantity.html");
        Assert.Contains(pages, page => page.Path == "Units/concepts/Units.Quantity.html");
    }

    [Fact]
    public void EveryLinkPointsAtAFileTheFolderHolds()
    {
        var language = Atlas();

        using var temporary = new TemporaryDirectory();

        _ = LanguageDocumentation.Write(language, temporary.Root);

        var pages = LanguageDocumentation.Generate(language);

        // A link is only worth writing if the tree has the file it names, and a relative link is the one thing a
        // static folder cannot check at run time - so the test does it once, for every link on every page.
        foreach (var page in pages)
        {
            var folder = Path.GetDirectoryName(Path.Combine(temporary.Root, page.Path.Replace('/', Path.DirectorySeparatorChar)))!;

            foreach (Match link in LinkPattern().Matches(page.Text))
            {
                var target = link.Groups[1].Value;

                Assert.False(Path.IsPathRooted(target), $"{page.Path} links to an absolute path: {target}");

                var resolved = Path.GetFullPath(Path.Combine(folder, target));

                Assert.True(File.Exists(resolved), $"{page.Path} links to '{target}', which the folder does not hold.");
            }
        }
    }

    [Fact]
    public void TwoRunsWriteTheSameBytes()
    {
        var language = Atlas();

        using var first = new TemporaryDirectory();
        using var second = new TemporaryDirectory();

        _ = LanguageDocumentation.Write(language, first.Root);
        _ = LanguageDocumentation.Write(language, second.Root);

        foreach (var page in LanguageDocumentation.Generate(language))
        {
            var path = page.Path.Replace('/', Path.DirectorySeparatorChar);

            Assert.Equal(
                File.ReadAllBytes(Path.Combine(first.Root, path)),
                File.ReadAllBytes(Path.Combine(second.Root, path)));
        }

        // Byte-stable also means no byte order mark and no CRLF, so the folder can be committed and diffed.
        var root = File.ReadAllBytes(Path.Combine(first.Root, "index.html"));

        Assert.NotEqual(0xEF, root[0]);
        Assert.DoesNotContain((byte)'\r', root);
    }

    [Fact]
    public void ALanguagePageNamesEveryDependencyWithTheVersionItAskedFor()
    {
        var language = Atlas();

        var page = Assert.Single(LanguageDocumentation.Generate(language), each => each.Path == "Atlas/index.html");

        Assert.Contains("<td>Units</td>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<td>depends on</td>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<td>1.0.0</td>", page.Text, StringComparison.Ordinal);
        Assert.Contains("href=\"../Units/index.html\"", page.Text, StringComparison.Ordinal);

        // Nothing is marked, because the host answered exactly the version the language asked for.
        Assert.DoesNotContain("mismatch", page.Text, StringComparison.Ordinal);

        // The front door of the set lists the languages instead: a site documents many, and what one of them declares
        // it stands on belongs on that one's page.
        var root = Assert.Single(LanguageDocumentation.Generate(language), each => each.Path == "index.html");

        Assert.Contains("href=\"Units/index.html\"", root.Text, StringComparison.Ordinal);
        Assert.Contains("href=\"Atlas/index.html\"", root.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAnswerThatIsNotTheVersionAskedForIsMarked()
    {
        // The loader refuses a language whose dependency nothing answered, so "declared but unresolved" cannot appear on
        // a page. What can appear, and used to be silent, is a resolver that answered a different version: the host is
        // asked by name only.
        using var temporary = new TemporaryDirectory();

        Write(temporary.Root, "Mismatched", declaredVersion: "9.9.9");

        var units = Assert.IsType<LanguageProject>(
            LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Units"), out var unitsError) ?? Fail(unitsError));

        var language = Assert.IsType<LanguageProject>(
            LanguageProject.Open(temporary.Root, new ResolvedLanguages(units), out var error) ?? Fail(error));

        var page = Assert.Single(LanguageDocumentation.Generate(language), each => each.Path == "Mismatched/index.html");

        Assert.Contains("class=\"mismatch\"", page.Text, StringComparison.Ordinal);
        Assert.Contains("9.9.9", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDocumentationOfTheSampleCarriesTheLanguageItStandsOn()
    {
        var language = Atlas();

        // Atlas holds a child slot over `Units.Quantity`, which this folder never declares: the link has to leave the
        // language and land in the base's pages, which is the whole reason a chain is documented instead of a language.
        var place = Assert.Single(
            LanguageDocumentation.Generate(language),
            page => page.Path == "Atlas/concepts/Atlas.Place.html");

        Assert.Contains("href=\"../../Units/concepts/Units.Quantity.html\"", place.Text, StringComparison.Ordinal);

        var units = Assert.Single(LanguageDocumentation.Generate(language), page => page.Path == "Units/index.html");

        Assert.Contains("Units.Quantity", units.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AConceptPageStatesEverySlotOfTheConcept()
    {
        var concept = Atlas().Concepts.Get("Atlas.Stop");

        var page = Assert.Single(
            LanguageDocumentation.Generate(Atlas()),
            each => each.Path == "Atlas/concepts/Atlas.Stop.html");

        foreach (var slot in concept.Slots)
        {
            Assert.Contains($"<code>{slot.Name}</code>", page.Text, StringComparison.Ordinal);
        }

        // A reference slot links to the concept it is declared over, so the page a reader needs is one click away.
        Assert.Contains("href=\"../../Atlas/concepts/Atlas.Place.html\"", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AConceptPageWritesOutTheNotationItIsDrawnWith()
    {
        var language = Atlas();

        var place = Assert.Single(
            LanguageDocumentation.Generate(language),
            page => page.Path == "Atlas/concepts/Atlas.Place.html");

        // The notation is a tree of cells, so the page writes what the cells spell: a keyword as itself and a slot as
        // its name in angle brackets. Escaped, because a page shows the brackets rather than interpreting them.
        Assert.Contains("<pre class=\"notation\">place &lt;name&gt; at &lt;at&gt;</pre>", place.Text, StringComparison.Ordinal);

        var map = Assert.Single(
            LanguageDocumentation.Generate(language),
            page => page.Path == "Atlas/concepts/Atlas.Map.html");

        // A concept whose notation has an indented body shows the body under it, two spaces per level - which is what
        // makes the page answer "what does one of these look like" rather than "which cells exist".
        Assert.Contains("map &lt;name&gt; at &lt;entry&gt;\n  &lt;places...&gt;\n  &lt;routes...&gt;</pre>", map.Text, StringComparison.Ordinal);

        // The legend says where a slot text comes from, because what a child owns it draws itself: a notation whose
        // child ends in its own punctuation shows it once, and a reader who does not know that reads a defect into it.
        Assert.Contains("punctuation the child owns", place.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheForHeaderDrawsBothOfItsSeparators()
    {
        var pages = LanguageDocumentation.Generate(CSharp());

        var loop = Assert.Single(pages, page => page.Path == "CSharp/concepts/CSharp.For.html");

        // Both semicolons of a header belong to the `for` itself - `for (;;)` is legal C# - so the shape spells both.
        // While the initializer was a statement it drew its own `;` as the first separator, and the shape showed one
        // semicolon although the drawn line has two; a reader read a defect into the language.
        Assert.Contains(
            "for (&lt;initializer&gt;; &lt;condition&gt;; &lt;increment&gt;)",
            loop.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AConceptPageNamesTheConceptsThatExtendAndImplementIt()
    {
        var pages = LanguageDocumentation.Generate(CSharp());

        // The reverse relation, which is the one a reader on a base concept needs: `CSharp.Member` is where "what may a
        // class hold" is answered, and the answer is the concepts that extend it. A page that only looked up would make
        // the reader open every concept of the language to find them.
        var member = Assert.Single(pages, page => page.Path == "CSharp/concepts/CSharp.Member.html");

        Assert.Contains("extended by", member.Text, StringComparison.Ordinal);
        Assert.Contains("CSharp.Method.html", member.Text, StringComparison.Ordinal);
        Assert.Contains("CSharp.Field.html", member.Text, StringComparison.Ordinal);

        // An interface is implemented rather than extended, and the page says which of the two it is.
        var named = Assert.Single(pages, page => page.Path == "CSharp/concepts/CSharp.NamedDeclaration.html");

        Assert.Contains("implemented by", named.Text, StringComparison.Ordinal);
        Assert.Contains("CSharp.Parameter.html", named.Text, StringComparison.Ordinal);

        // A concept nothing builds on says nothing about it rather than showing an empty direction.
        var literal = Assert.Single(pages, page => page.Path == "CSharp/concepts/CSharp.IntLiteral.html");

        Assert.DoesNotContain("extended by", literal.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("implemented by", literal.Text, StringComparison.Ordinal);
    }
    [Fact]
    public void TheLanguageARuleIsWrittenInIsPartOfTheSet()
    {
        // A language author meets these concepts the way they meet the type rule language, so its bodies are
        // documented beside the rest - and a documentation set that dropped them would hide half the authoring model.
        var language = LanguageProject.Shipped(
            Projectional.EvaluationRules.EvaluationRuleLanguage.Name,
            version: null,
            Projectional.EvaluationRules.EvaluationRuleLanguage.ConceptDeclarations,
            Projectional.EvaluationRules.EvaluationRuleLanguage.Concepts,
            Projectional.EvaluationRules.EvaluationRuleLanguage.CreateNotations(Projectional.EvaluationRules.EvaluationRuleLanguage.Concepts),
            Projectional.Semantics.ConstraintSet.Empty,
            Projectional.IntentionRules.IntentionLowering.CreateProvider(
                [],
                Projectional.EvaluationRules.EvaluationRuleLanguage.Concepts),
            []);

        var pages = LanguageDocumentation.Generate(language);

        Assert.Contains(pages, page => page.Path == "EvaluationRules/concepts/Evaluation.Operator.html");
        Assert.Contains(pages, page => page.Path == "EvaluationRules/notations.html");
    }

    [Fact]
    public void AnEvaluationPageStatesWhatEachConceptMeans()
    {
        var pages = LanguageDocumentation.Generate(ExpressionsProject());
        var page = Assert.Single(pages, each => each.Path == "Expressions/evaluation.html");

        // The sentence is the rule own, so the page cannot describe a meaning the interpreter does not have.
        Assert.Contains("Its operator applied to the values of its two operands.", page.Text, StringComparison.Ordinal);
        Assert.Contains("Expressions.Call.html", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEvaluationPageDrawsTheRulesAsTheyAreWritten()
    {
        // A sentence says what a rule computes and a drawing says how it says so, which is the half a language author copies.
        // The notations are the rule language's own: drawing a rule with the language it describes would find no notation.
        var page = Assert.Single(LanguageDocumentation.Generate(Units()), each => each.Path == "Units/evaluation.html");

        // The file name is the heading, and the rule reads as the sentence it is - `rule for Units.Quantity`, then the record
        // body with one field per line, which is what the drawing showed when the notation first flowed them onto one.
        Assert.Contains("<h2>quantity</h2>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<pre class=\"example\">", page.Text, StringComparison.Ordinal);
        Assert.Contains("<span class=\"s-keyword\">rule for</span>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<span class=\"s-text\">Units.Quantity</span>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<span class=\"s-keyword\">record</span>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<span class=\"s-text\">value</span><span class=\"s-keyword\">:</span>", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEvaluationPageDrawsNothingForALanguageWhoseRulesItDoesNotHold()
    {
        // A shipped language hands over a registry and no documents, so the page states the sentences and has nothing to draw -
        // which is not a failure: the rules of `Expressions` live in its assembly, not in a file a page could show.
        var page = Assert.Single(LanguageDocumentation.Generate(ExpressionsProject()), each => each.Path == "Expressions/evaluation.html");

        Assert.DoesNotContain("<pre class=\"example\">", page.Text, StringComparison.Ordinal);
        Assert.Contains("Its operator applied to the values of its two operands.", page.Text, StringComparison.Ordinal);
    }
    [Fact]
    public void AnEvaluationPageStatesWhatALanguageWithoutMeaningHas()
    {
        var page = Assert.Single(
            LanguageDocumentation.Generate(CSharp()),
            each => each.Path == "CSharp/evaluation.html");

        // A language that states no meaning is drawn and generated but not run, and the page says that rather than showing
        // an empty table. `CSharp` rather than `Atlas` or `Units`: both declare aspects completely enough to state a meaning,
        // and this page is about the language that has none.
        Assert.Contains("can be drawn and not run", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<th>Evaluates to</th>", page.Text, StringComparison.Ordinal);
    }
    [Fact]
    public void AChecksPageNamesEveryCheckTheLanguageBrings()
    {
        var pages = LanguageDocumentation.Generate(CSharp());
        var checks = Assert.Single(pages, page => page.Path == "CSharp/checks.html");

        // Every check says which code its findings carry and what it looks for, which is what makes the page an answer to
        // "what will be reported about my document" rather than a list of the framework's class names.
        Assert.Contains("<code>STD002</code>", checks.Text, StringComparison.Ordinal);
        Assert.Contains("<code>STD020</code>", checks.Text, StringComparison.Ordinal);
        Assert.Contains("Two children of one slot that answer to the same name.", checks.Text, StringComparison.Ordinal);

        // The aspect page says where the aspect comes from, and that it is nowhere in the folder of a language.
        var aspects = Assert.Single(pages, page => page.Path == "CSharp/aspects.html");

        Assert.Contains("no folder: a language takes them from the framework", aspects.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AChecksPageStatesWhatALanguageWithoutChecksHas()
    {
        var checks = Assert.Single(
            LanguageDocumentation.Generate(Atlas()),
            page => page.Path == "Atlas/checks.html");

        // A declared language states constraints and brings no checks, so the page says which rule applies instead of
        // showing an empty table - and it says it once, not a second time as the table's own empty row.
        Assert.Contains("brings no check of its own", checks.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<th>Code</th>", checks.Text, StringComparison.Ordinal);
    }
    [Fact]
    public void AConceptPageNamesTheSlotsThatCanHoldIt()
    {
        var pages = LanguageDocumentation.Generate(CSharp());

        // The reverse of the slot table: the slot that holds a member belongs to the class, so a reader on the concept
        // that a change would break is told which concept refers to it.
        var member = Assert.Single(pages, page => page.Path == "CSharp/concepts/CSharp.Member.html");

        Assert.Contains("Held by", member.Text, StringComparison.Ordinal);
        Assert.Contains("CSharp.Class.html", member.Text, StringComparison.Ordinal);
        Assert.Contains("<code>members</code>", member.Text, StringComparison.Ordinal);

        // A concept no slot names says so rather than showing an empty table.
        var file = Assert.Single(pages, page => page.Path == "CSharp/concepts/CSharp.File.html");

        Assert.Contains("No concept of this set declares a slot that can hold it.", file.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ALanguagePageNamesTheNamingConventionOfEveryConcept()
    {
        var page = Assert.Single(
            LanguageDocumentation.Generate(CSharp()),
            each => each.Path == "CSharp/index.html");

        // The convention is a column of the concept list, because "how do names read here" is asked of the list: a rule
        // shared by nine concepts is invisible in nine separate concept pages.
        Assert.Contains("<th>Naming</th>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<code>PascalCase</code>", page.Text, StringComparison.Ordinal);

        // And a concept that declares no rule says "any", which is the honest answer rather than an empty cell.
        Assert.Contains("<span class=\"muted\">any</span>", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AConceptPageNamesTheRuleBesideTheSlotItAppliesTo()
    {
        var page = Assert.Single(
            LanguageDocumentation.Generate(CSharp()),
            each => each.Path == "CSharp/concepts/CSharp.Class.html");

        // Beside the slot, not only in the facts above it: the rule is about that cell, and a reader scanning the table
        // should not have to match it up with a sentence higher on the page.
        Assert.Contains("<code>name</code> <span class=\"muted\">PascalCase</span>", page.Text, StringComparison.Ordinal);
    }
    [Fact]
    public void AConceptWithNoNotationOfItsOwnSaysSoInsteadOfDrawingNothing()
    {
        var language = Atlas();

        var spot = Assert.Single(
            LanguageDocumentation.Generate(language),
            page => page.Path == "Atlas/concepts/Atlas.Spot.html");

        // An abstract concept needs no notation, and a page that silently left the section out would look like a
        // concept nobody had finished rather than one that is declared not to draw itself.
        Assert.DoesNotContain("<pre class=\"notation\">", spot.Text, StringComparison.Ordinal);
        Assert.Contains("no notation of its own", spot.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExamplesPageDrawsEveryDocumentALanguageShips()
    {
        var language = Atlas();

        var page = Assert.Single(LanguageDocumentation.Generate(language), each => each.Path == "Atlas/examples.html");

        // The three documents: the accessory one the language declares and the two saved beside `language.mps`.
        Assert.Contains("<h2>known-places</h2>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<h2>map</h2>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<h2>route</h2>", page.Text, StringComparison.Ordinal);

        // Drawn, not described: the document's own names and its indentation are in the page.
        Assert.Contains("place", page.Text, StringComparison.Ordinal);
        Assert.Contains("summit", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExampleIsHighlightedWithTheKindsTheEditorPaints()
    {
        var page = Assert.Single(
            LanguageDocumentation.Generate(Atlas()),
            each => each.Path == "Atlas/examples.html");

        // The classification is the renderer's own (`CellStyler`), which is what a palette in the editor turns into a
        // colour: a keyword is a keyword, a resolved reference is a reference, and an empty slot is a placeholder.
        Assert.Contains("<span class=\"s-keyword\">place</span>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<span class=\"s-text\">summit</span>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<span class=\"s-ref\">lake</span>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<span class=\"s-placeholder\">&lt;none&gt;</span>", page.Text, StringComparison.Ordinal);

        // A page that coloured text by looking at it would need no spans at all; this one never decides, it only maps.
        Assert.DoesNotContain("<span class=\"s-keyword\">summit</span>", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ALanguageWithNoDocumentOfItsOwnGetsNoExamplesPage()
    {
        // A language with structure and nothing to look at: no accessory document and no document beside `language.mps`,
        // so a page saying "no documents" would be an empty room with a sign on the door.
        using var temporary = new TemporaryDirectory();

        // No dependency and no document: a language that owns nothing to draw. A base would matter, because an accessory
        // travels through the chain - a language that stands on Units inherits Units' library and therefore has examples.
        Write(temporary.Root, "Bare", declaredVersion: "1.0.0", dependency: null);

        var bare = Assert.IsType<LanguageProject>(
            LanguageProject.Open(temporary.Root, out var error) ?? Fail(error));

        var pages = LanguageDocumentation.Generate([bare, Atlas()]);

        Assert.DoesNotContain(pages, page => page.Path == "Bare/examples.html");

        // Asserted per language rather than per page: the tree carries every language of the set, so "Examples" appears on
        // the Bare page too - as Atlas' entry. What must not appear is an entry pointing at a page Bare does not have.
        Assert.DoesNotContain(
            "../Bare/examples.html",
            Assert.Single(pages, page => page.Path == "Bare/index.html").Text,
            StringComparison.Ordinal);

        Assert.Contains(
            "../Atlas/examples.html",
            Assert.Single(pages, page => page.Path == "Atlas/index.html").Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheAspectsPageCountsWhatTheLanguageDeclares()
    {
        var language = Atlas();

        var page = Assert.Single(LanguageDocumentation.Generate(language), each => each.Path == "Atlas/aspects.html");

        // Every folder the language has, and the one aspect that has no folder of its own because an enumeration is a
        // declaration in `structure/` rather than a folder.
        foreach (var folder in LanguageProject.AspectFolders.Where(folder => folder != LanguageProject.StructureFolder))
        {
            Assert.Contains($"<code>{folder}</code>", page.Text, StringComparison.Ordinal);
        }

        Assert.Contains("a declaration, not a folder of its own", page.Text, StringComparison.Ordinal);

        // Each aspect's own count, and the Concepts section's honest split between what the folder declares and what
        // came from the chain - two numbers a reader needs and would otherwise have to subtract themselves.
        Assert.Contains($"<td>{language.Notations.Notations.Count}</td>", page.Text, StringComparison.Ordinal);
        Assert.Contains("visible through the chain", page.Text, StringComparison.Ordinal);

        // The concepts by kind, which is what the structure folder holds: an abstract set, its concrete kinds and the
        // roots a document can start with.
        Assert.Contains("<td>abstract</td>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<td>concrete</td>", page.Text, StringComparison.Ordinal);
        Assert.Contains("<td>root</td>", page.Text, StringComparison.Ordinal);
        Assert.Contains("Atlas.Spot", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ASetDocumentsEachLanguageExactlyOnce()
    {
        // A site is the whole project: Atlas and Units both, with Atlas' chain reaching Units. Documenting them as one
        // set is what keeps Units to a single folder instead of one copy per language that stands on it.
        var units = Units();
        var atlas = Atlas(units);

        var pages = LanguageDocumentation.Generate([units, atlas]);

        Assert.Single(pages, page => page.Path == "Units/index.html");
        Assert.Single(pages, page => page.Path == "Atlas/index.html");
        Assert.Single(pages, page => page.Path == "Units/concepts/Units.Quantity.html");

        // The front door is the set's, so it names itself as a set rather than as one of the languages.
        var root = Assert.Single(pages, page => page.Path == "index.html");

        Assert.Contains("<h1>Language documentation</h1>", root.Text, StringComparison.Ordinal);
        Assert.Contains("2 languages, each documented once", root.Text, StringComparison.Ordinal);
        Assert.Contains("href=\"Units/index.html\"", root.Text, StringComparison.Ordinal);
        Assert.Contains("href=\"Atlas/index.html\"", root.Text, StringComparison.Ordinal);

        // One search over every concept of the set, which is the reason a reader arrives here rather than at a language.
        Assert.Contains("Atlas.Stop", root.Text, StringComparison.Ordinal);
        Assert.Contains("Units.Quantity", root.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryLinkOfASetResolvesAcrossItsLanguages()
    {
        var units = Units();
        var atlas = Atlas(units);

        using var temporary = new TemporaryDirectory();

        _ = LanguageDocumentation.Write([units, atlas], temporary.Root);

        foreach (var page in LanguageDocumentation.Generate([units, atlas]))
        {
            var folder = Path.GetDirectoryName(Path.Combine(temporary.Root, page.Path.Replace('/', Path.DirectorySeparatorChar)))!;

            foreach (Match link in LinkPattern().Matches(page.Text))
            {
                var target = link.Groups[1].Value;

                Assert.False(Path.IsPathRooted(target), $"{page.Path} links to an absolute path: {target}");

                Assert.True(
                    File.Exists(Path.GetFullPath(Path.Combine(folder, target))),
                    $"{page.Path} links to '{target}', which the set does not hold.");
            }
        }
    }

    [Fact]
    public void ASetRefusesTwoLanguagesOfTheSameName()
    {
        var units = Units();
        var atlas = Atlas(units);

        // Two different folders answering one name would share a folder and one would overwrite the other's pages, so it
        // is refused before anything is written instead of discovered in the browser.
        using var temporary = new TemporaryDirectory();

        Write(temporary.Root, "Units", declaredVersion: "1.0.0");

        var impostor = Assert.IsType<LanguageProject>(
            LanguageProject.Open(temporary.Root, new ResolvedLanguages(units), out var error) ?? Fail(error));

        var refusal = Assert.Throws<ArgumentException>(() => LanguageDocumentation.Generate([atlas, impostor]));

        Assert.Contains("Units", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPageCarriesTheHeaderAndTheTreeAndMarksItself()
    {
        var units = Units();
        var atlas = Atlas(units);

        // The two assets are pages of the tree as well, and they are the only ones with no chrome of their own.
        var pages = LanguageDocumentation.Generate([units, atlas]).Where(page => page.Path.EndsWith(".html", StringComparison.Ordinal));

        using var temporary = new TemporaryDirectory();

        _ = LanguageDocumentation.Write([units, atlas], temporary.Root);

        foreach (var page in pages)
        {
            // The three parts of the shape a reader navigates by: one search in the header, the tree beside the content,
            // and the entry for the page they are on marked rather than merely present.
            Assert.Contains("<header class=\"top\">", page.Text, StringComparison.Ordinal);
            Assert.Contains("<nav class=\"tree\" id=\"nav\"", page.Text, StringComparison.Ordinal);
            Assert.Equal(1, Count(page.Text, "<input type=\"search\""));

            if (page.Path == "index.html")
            {
                // The top of the tree has no entry of its own, so nothing is current - a page cannot link to itself.
                Assert.DoesNotContain("aria-current=\"page\"", page.Text, StringComparison.Ordinal);

                continue;
            }

            var current = Regex.Match(page.Text, "class=\"item current\" href=\"([^\"]+)\"");

            Assert.True(current.Success, $"{page.Path} is in the tree but no entry marks it as the current page.");

            var folder = Path.GetDirectoryName(Path.Combine(temporary.Root, page.Path.Replace('/', Path.DirectorySeparatorChar)))!;

            Assert.Equal(
                Path.GetFullPath(Path.Combine(temporary.Root, page.Path.Replace('/', Path.DirectorySeparatorChar))),
                Path.GetFullPath(Path.Combine(folder, current.Groups[1].Value)));
        }
    }

    [Fact]
    public void TheTreeOffersEveryConceptOfTheSetFromEveryPage()
    {
        var units = Units();
        var atlas = Atlas(units);

        // The tree is how a reader walks the set, so it has to hold the whole set - a language that appears only on the
        // front page is a language nobody finds from a concept page.
        foreach (var page in LanguageDocumentation.Generate([units, atlas]).Where(page => page.Path.EndsWith(".html", StringComparison.Ordinal)))
        {
            Assert.Contains(">Units.Quantity</a>", page.Text, StringComparison.Ordinal);
            Assert.Contains(">Atlas.Stop</a>", page.Text, StringComparison.Ordinal);
            Assert.Contains(">Atlas</summary>", page.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ThePagesTravelInsideAPackage()
    {
        // What the shell's File > Package does with them: the whole set, under docs/, beside the code. The prefix is the
        // packer's decision because the pages link to each other relatively and two languages cannot share one index.
        var language = Atlas();
        var pages = LanguageDocumentation.Generate(language);

        var files = pages
            .Select(page => new PackageFile($"docs/{page.Path}", System.Text.Encoding.UTF8.GetBytes(page.Text)))
            .ToList();

        var zip = LanguagePackage.Zip(
            LanguageExporter.Export(language, "Sample.Atlas"),
            PackageContent.Sources,
            files);

        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);

        var names = archive.Entries.Select(entry => entry.FullName).ToList();

        Assert.Contains("docs/index.html", names);
        Assert.Contains("docs/Atlas/index.html", names);
        Assert.Contains("docs/Atlas/concepts/Atlas.Map.html", names);
        Assert.Contains("docs/Atlas/examples.html", names);

        // The chain travels too, so a reader of the archive can follow a link into the language it stands on.
        Assert.Contains("docs/Units/index.html", names);

        // And the code is still there, for the language and the one it stands on: the pages are added beside the export,
        // never instead of it.
        Assert.Contains("AtlasLanguage-1.0.0.cs", names);
        Assert.Contains("UnitsLanguage-1.0.0.cs", names);

        using var page = archive.GetEntry("docs/Atlas/examples.html")!.Open();
        using var reader = new StreamReader(page);

        Assert.Contains("s-keyword", reader.ReadToEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryAspectHasAPageAndTheOverviewLinksIt()
    {
        var pages = LanguageDocumentation.Generate(Atlas());

        var aspects = new[] { "enumerations", "notations", "types", "typing", "constraints", "scopes", "intentions", "generators", "importers", "accessory" };

        // A reader looking for "which concepts generate text" wants the entry in the tree, not a heading to scroll to.
        foreach (var aspect in aspects)
        {
            Assert.Single(pages, page => page.Path == $"Atlas/{aspect}.html");
        }

        var overview = Assert.Single(pages, page => page.Path == "Atlas/aspects.html");

        foreach (var aspect in aspects)
        {
            Assert.Contains($"href=\"../Atlas/{aspect}.html\"", overview.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AnAspectPageHoldsWhatThatAspectDeclares()
    {
        var pages = LanguageDocumentation.Generate(Atlas());

        // The notation is written out where it is listed, so a reader sees the shape without opening six concept pages.
        var notations = Assert.Single(pages, page => page.Path == "Atlas/notations.html");

        Assert.Contains("map &lt;name&gt; at &lt;entry&gt;\n  &lt;places...&gt;", notations.Text, StringComparison.Ordinal);

        // A scope provider is asked per reference at scope time, so the page lists the concept's own reference slots
        // rather than claiming which ones the provider fills.
        var scopes = Assert.Single(pages, page => page.Path == "Atlas/scopes.html");

        Assert.Contains("Reference slots of the concept", scopes.Text, StringComparison.Ordinal);
        Assert.Contains("onMap, start", scopes.Text, StringComparison.Ordinal);

        // The rule objects hold a check method, so the page counts them instead of inventing a name.
        var constraints = Assert.Single(pages, page => page.Path == "Atlas/constraints.html");

        Assert.Contains("No rule applies everywhere.", constraints.Text, StringComparison.Ordinal);

        var intentions = Assert.Single(pages, page => page.Path == "Atlas/intentions.html");

        Assert.Contains("can say that much but not which offers it makes", intentions.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AGeneratorIsShownAsTheTemplateItEmits()
    {
        var page = Assert.Single(
            LanguageDocumentation.Generate(Atlas()),
            each => each.Path == "Atlas/generators.html");

        // The parts, read back: a slot as {name}, a conditional in brackets, and a list with the separator that joins it
        // - which is the one fact about a list a reader cannot guess from its name.
        Assert.Contains("place {name} at {at}", page.Text, StringComparison.Ordinal);
        Assert.Contains("[if entry: at -&gt;entry]", page.Text, StringComparison.Ordinal);
        Assert.Contains("{places... separated by &quot;\\n&quot;}", page.Text, StringComparison.Ordinal);

        // And the legend for a template, which needs it for the same reason: what a child contributes is its own
        // template text, so a header field that is a statement brings the semicolon it ends with.
        Assert.Contains("a field that reuses a statement prints that statement", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEnumerationsSectionStatesWhatALanguageWithoutOneHas()
    {
        // Lowering resolves an enumeration into the symbols a property may hold and keeps no key for it, so a page names
        // an enumeration by its symbols and the slots that hold it. None of the declared samples declares one - C# is
        // the language that does, and it is not a `LanguageProject` a test can build without the shell - so what is
        // pinned here is the section's honesty for a language that has none.
        var page = Assert.Single(
            LanguageDocumentation.Generate(Atlas()),
            each => each.Path == "Atlas/enumerations.html");

        // One sentence, not two: the summary already answered "this language has none", so the table that would have
        // said it again is not rendered at all. A page that led with the summary and then repeated it in the table's
        // empty row read as a mistake, which is what this pins.
        Assert.Contains("No property of this language is declared over an enumeration.", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("No property is declared over an enumeration.", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheImportersPageDocumentsWhatATemplateImporterReads()
    {
        // A generator's inverse is the importer with no document of its own: what it reads *is* the generator's
        // template, so that is what the page has to print. Naming its CLR type was all it said before, which tells a
        // reader nothing about whether their file will be read.
        var page = Assert.Single(
            LanguageDocumentation.Generate(Pipeline()),
            each => each.Path == "Pipeline/importers.html");

        Assert.Contains("<code>ShellScript</code>", page.Text, StringComparison.Ordinal);
        Assert.Contains("{stages... separated by", page.Text, StringComparison.Ordinal);
        Assert.Contains("A document starts with <code>Pipeline.Pipeline</code>.", page.Text, StringComparison.Ordinal);
        Assert.Contains("template.unexpected-text", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheImportersPageDocumentsTheRulesOfADeclaredImporter()
    {
        // The words are the notation's own - `line name`, `repeat stages`, `token Space is skipped as whitespace` - so
        // a reader who opens `import/ini.mps` in the editor meets the same sentences there.
        var page = Assert.Single(
            LanguageDocumentation.Generate(Pipeline()),
            each => each.Path == "Pipeline/importers.html");

        Assert.Contains("&quot;pipeline = &quot;", page.Text, StringComparison.Ordinal);
        Assert.Contains("line name", page.Text, StringComparison.Ordinal);
        Assert.Contains("repeat stages", page.Text, StringComparison.Ordinal);
        Assert.Contains("token Space is skipped as whitespace", page.Text, StringComparison.Ordinal);
        Assert.Contains("import.too-few", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExampleIsDrawnWithoutTheEditorsTypeAnnotations()
    {
        var page = Assert.Single(
            LanguageDocumentation.Generate(Expressions()),
            each => each.Path == "DeclaredExpressions/examples.html");

        // The annotation is the editor telling a language author what its type rules are missing, and these samples
        // declare none - so the cell is not built at all, and neither is the punctuation in front of it. A drawing that
        // said `42 : unknown` would be a picture of the editor's complaint rather than of the language.
        Assert.DoesNotContain("unknown", page.Text, StringComparison.Ordinal);
        Assert.Contains("42", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExampleDrawsTheConceptOfTheLanguageItStandsOn()
    {
        var units = Units();
        var invoice = Assert.IsType<LanguageProject>(
            LanguageProject.Open(
                Path.Combine(AppContext.BaseDirectory, "Invoice"),
                new ResolvedLanguages(units),
                out var error) ?? Fail(error));

        var page = Assert.Single(LanguageDocumentation.Generate(invoice), each => each.Path == "Invoice/examples.html");

        // The document holds a `Units.Quantity`, and its drawing lives in the language Invoice stands on - so the picture
        // is composed from the chain the way the shell composes one registry from every language it holds. Without that,
        // the page said `<no notation for Units.Quantity>` where the editor draws a quantity.
        Assert.DoesNotContain("no notation for", page.Text, StringComparison.Ordinal);
        Assert.Contains("kg", page.Text, StringComparison.Ordinal);
    }

    /// <summary>Every <c>href</c> and <c>src</c> a page writes, which is every path it asks a reader to follow.</summary>
    private static Regex LinkPattern() => new("(?:href|src)=\"([^\"]+)\"", RegexOptions.CultureInvariant);

    /// <summary>How often <paramref name="needle"/> appears in <paramref name="text"/>.</summary>
    private static int Count(string text, string needle)
    {
        var count = 0;

        for (var index = text.IndexOf(needle, StringComparison.Ordinal); index >= 0; index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static LanguageProject Fail(string? error) =>
        throw new InvalidOperationException(error ?? "the folder did not open, and said nothing about why");

    /// <summary>The shipped expression vocabulary as a project, the one language whose meaning is declared.</summary>
    private static LanguageProject ExpressionsProject() =>
        LanguageProject.Shipped(
            ExpressionsLanguage.Name,
            version: null,
            ExpressionsLanguage.ConceptDeclarations,
            ExpressionsLanguage.Concepts,
            ExpressionsLanguage.CreateNotations(ExpressionsLanguage.Concepts),
            ExpressionsLanguage.Constraints,
            ExpressionsIntentions.Provider,
            [],
            types: ExpressionsLanguage.Types,
            evaluation: ExpressionsEvaluation.Registry);
    /// <summary>The base language as a project, which is the fixture whose inheritance is worth reversing.</summary>
    private static LanguageProject CSharp() =>
        LanguageProject.Shipped(
            CSharpLanguage.Name,
            version: null,
            CSharpLanguage.ConceptDeclarations,
            CSharpLanguage.Concepts,
            CSharpNotations.CreateNotations(CSharpLanguage.Concepts),
            CSharpConstraints.All,
            CSharpIntentions.Provider,
            [CSharpTextGenerators.Generator],
            checks: CSharpChecks.All);
    private static LanguageProject Atlas() => Atlas(Units());

    private static LanguageProject Atlas(LanguageProject units) =>
        Assert.IsType<LanguageProject>(
            LanguageProject.Open(
                Path.Combine(AppContext.BaseDirectory, "Atlas"),
                new ResolvedLanguages(units),
                out var error) ?? Fail(error));

    private static LanguageProject Expressions() =>
        Assert.IsType<LanguageProject>(
            LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Expressions"), out var error) ?? Fail(error));

    private static LanguageProject Pipeline() =>
        Assert.IsType<LanguageProject>(
            LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error) ?? Fail(error));

    private static LanguageProject Units() =>
        Assert.IsType<LanguageProject>(
            LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Units"), out var error) ?? Fail(error));

    /// <summary>A language folder with one concept, used for the cases the sample cannot be bent into.</summary>
    private static void Write(string root, string name, string declaredVersion, string? dependency = "Units")
    {
        _ = Directory.CreateDirectory(Path.Combine(root, "structure"));

        var definitions = new DefinitionBuilder();

        definitions.Concept(
            "Mismatched.Item",
            slots: [definitions.Property("name", "string")],
            nameSlot: "name",
            description: "An item.");

        foreach (var document in definitions.Documents)
        {
            var key = document.Roots[0].GetOrNull(StructureLanguage.KeySlot)!;

            ModelFile.Save(document, Path.Combine(root, "structure", $"{key}.mps"));
        }

        var languageRoot = StructureLanguage.NewModel("language");
        var language = languageRoot.NewNode(StructureConceptKeys.Language);

        language.Set("name", name);
        language.Set(StructureLanguage.VersionSlot, "1.0.0");

        if (dependency is not null)
        {
            var declaration = languageRoot
                .NewChild(language, StructureLanguage.DependenciesSlot, StructureConceptKeys.Dependency);

            declaration.Set("name", dependency);
            declaration.Set(StructureLanguage.VersionSlot, declaredVersion);
        }

        ModelFile.Save(languageRoot, Path.Combine(root, LanguageProject.LanguageFile));
    }
}