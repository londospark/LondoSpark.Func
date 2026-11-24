using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using LondoSpark.Func.Analyzers;
using LondoSpark.Func.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

/// <summary>LSF0002 and the code fix that imports a union's nested case types.</summary>
public sealed class ImportUnionCasesTests
{
    private const string ImportResult = "using static LondoSpark.Func.Result<int, string>;";

    [Test]
    public async Task MissingCaseInASwitchOffersTheStaticImport()
    {
        var fixedSource = await ApplyFixAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static int Read(Result<int, string> result) =>
                    result switch
                    {
                        Success(var value) => value,
                        Failure => -1
                    };
            }
            """,
            "CS0246",
            "Add 'using static");

        await Assert.That(fixedSource).Contains(ImportResult);
        await Assert.That(fixedSource).Contains("Success(var value) => value,");
    }

    [Test]
    public async Task MissingCaseInAReturnedConstructionOffersTheStaticImport()
    {
        var fixedSource = await ApplyFixAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static Result<int, string> Parse(string text)
                {
                    return new Success(text.Length);
                }
            }
            """,
            "CS0246",
            "Add 'using static");

        await Assert.That(fixedSource).Contains(ImportResult);
        await Assert.That(fixedSource).Contains("return new Success(text.Length);");
    }

    [Test]
    public async Task ImportNamesTypeArgumentsWithTheirNamespaces()
    {
        var fixedSource = await ApplyFixAsync(
            """
            using LondoSpark.Func;

            namespace Shop
            {
                public sealed record Customer(string Name);

                public static class Sample
                {
                    public static string Name(Option<Customer> customer) =>
                        customer switch
                        {
                            Some(var found) => found.Name,
                            None => "unknown"
                        };
                }
            }
            """,
            "CS0246",
            "Add 'using static");

        await Assert.That(fixedSource).Contains("using static LondoSpark.Func.Option<Shop.Customer>;");
    }

    [Test]
    public async Task CaseNestedInAnotherCasesPayloadImportsThePayloadUnion()
    {
        var fixedSource = await ApplyFixAsync(
            """
            using LondoSpark.Func;
            using static LondoSpark.Func.Option<LondoSpark.Func.Result<int, string>>;

            public static class Sample
            {
                public static bool IsPresentSuccess(Option<Result<int, string>> value) =>
                    value is Some(Success(var number)) && number > 0;
            }
            """,
            "CS0246",
            "Add 'using static");

        await Assert.That(fixedSource).Contains(ImportResult);
    }

    [Test]
    public async Task ConflictingImportOffersOnlyQualification()
    {
        const string source =
            """
            using LondoSpark.Func;
            using static LondoSpark.Func.Validation<int, string>;

            public static class Sample
            {
                public static int Read(Result<int, string> result) =>
                    result switch
                    {
                        Success(var value) => value,
                        _ => -1
                    };
            }
            """;

        var titles = await FixTitlesAsync(source, "CS8121");
        var fixedSource = await ApplyFixAsync(source, "CS8121", "Use '");

        await Assert.That(titles.Any(static title => title.StartsWith("Add 'using static", StringComparison.Ordinal))).IsFalse();
        await Assert.That(fixedSource).Contains("Result<int, string>.Success(var value) => value,");
        await Assert.That(fixedSource).DoesNotContain(ImportResult);
    }

    [Test]
    public async Task UnionsOverTypeParametersOfferOnlyQualification()
    {
        const string source =
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static bool Succeeded<T>(Result<T, string> result) =>
                    result is Success;
            }
            """;

        var titles = await FixTitlesAsync(source, "CS0246");
        var fixedSource = await ApplyFixAsync(source, "CS0246", "Use '");

        await Assert.That(titles.Any(static title => title.StartsWith("Add 'using static", StringComparison.Ordinal))).IsFalse();
        await Assert.That(fixedSource).Contains("result is Result<T, string>.Success;");
    }

    [Test]
    public async Task QualifiedCasesAreReportedAsImportable()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;
            using Registration = LondoSpark.Func.Result<long, string>;

            public static class Sample
            {
                public static int Read(Result<int, string> result) =>
                    result switch
                    {
                        Result<int, string>.Success(var value) => value,
                        Result<int, string>.Failure => -1
                    };

                public static bool Aliased(Registration result) => result is Registration.Success;

                public static bool Generic<T>(Result<T, string> result) => result is Result<T, string>.Success;
            }
            """);

        await Assert.That(diagnostics.Length).IsEqualTo(2);
        await Assert.That(diagnostics.All(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Info)).IsTrue();
        await Assert.That(diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture))
            .IsEqualTo("Import 'LondoSpark.Func.Result<int, string>' to write 'Success' without its union type");
    }

    [Test]
    public async Task ImportingQualifiedCasesShortensEveryCaseOfThatUnion()
    {
        var fixedSource = await ApplyFixAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static int Read(Result<int, string> result) =>
                    result switch
                    {
                        Result<int, string>.Success(var value) => value,
                        Result<int, string>.Failure => -1
                    };

                public static Result<int, string> Make() => new Result<int, string>.Success(1);

                public static bool Other(Result<long, string> result) => result is Result<long, string>.Success;
            }
            """,
            UnionCasesCanBeImportedAnalyzer.DiagnosticId,
            "Add 'using static LondoSpark.Func.Result<int, string>;'");

        await Assert.That(fixedSource).Contains(ImportResult);
        await Assert.That(fixedSource).Contains("Success(var value) => value,");
        await Assert.That(fixedSource).Contains("Failure => -1");
        await Assert.That(fixedSource).Contains("=> new Success(1);");
        await Assert.That(fixedSource).Contains("result is Result<long, string>.Success;");
        await Assert.That(fixedSource).DoesNotContain("Result<long, string>;");
    }

    [Test]
    public async Task UnrelatedMissingTypesAreLeftAlone()
    {
        var titles = await FixTitlesAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static bool Check(object value) => value is Success;
            }
            """,
            "CS0246");

        await Assert.That(titles.Count).IsEqualTo(0);
    }

    private static IEnumerable<MetadataReference> References() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(Result<,>).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            "ImportTest",
            [CSharpSyntaxTree.ParseText(source)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = await compilation
            .WithAnalyzers([new UnionCasesCanBeImportedAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();

        return [.. diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)];
    }

    private static async Task<List<string>> FixTitlesAsync(string source, string diagnosticId) =>
        [.. (await CollectActionsAsync(source, diagnosticId)).Actions.Select(static action => action.Title)];

    private static async Task<string> ApplyFixAsync(string source, string diagnosticId, string titlePrefix)
    {
        var (document, actions) = await CollectActionsAsync(source, diagnosticId);
        var action = actions.FirstOrDefault(action => action.Title.StartsWith(titlePrefix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"No fix starting '{titlePrefix}'; offered: {string.Join(", ", actions.Select(static action => action.Title))}");

        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        return (await changed.GetDocument(document.Id)!.GetTextAsync()).ToString();
    }

    private static async Task<(Document Document, List<CodeAction> Actions)> CollectActionsAsync(string source, string diagnosticId)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "ImportTest", "ImportTest", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(projectId, References())
            .AddDocument(documentId, "Sample.cs", source);

        var document = solution.GetDocument(documentId)!;
        var compilation = (await document.Project.GetCompilationAsync())!;
        var diagnostics = diagnosticId == UnionCasesCanBeImportedAnalyzer.DiagnosticId
            ? await compilation.WithAnalyzers([new UnionCasesCanBeImportedAnalyzer()]).GetAnalyzerDiagnosticsAsync()
            : compilation.GetDiagnostics();

        var actions = new List<CodeAction>();
        var provider = new ImportUnionCasesCodeFixProvider();
        foreach (var diagnostic in diagnostics.Where(diagnostic => diagnostic.Id == diagnosticId))
        {
            var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
            await provider.RegisterCodeFixesAsync(context);
        }

        return (document, actions);
    }
}
