using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using LondoSpark.Func.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

public sealed class ResultHasNoDefaultValueAnalyzerTests
{
    private const string AnalyzerDiagnosticId = ResultHasNoDefaultValueAnalyzer.DiagnosticId;

    [Test]
    public async Task AnalyzerReportsDefaultExpression()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static Result<int, string> Value() => default(Result<int, string>);
            }
            """);

        await AssertSingleDiagnostic(diagnostics, "no zero value");
    }

    [Test]
    public async Task AnalyzerReportsTargetTypedDefaultLiteral()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static readonly Result<int, string> Field = default;
            }
            """);

        await AssertSingleDiagnostic(diagnostics, "no zero value");
    }

    [Test]
    public async Task AnalyzerReportsEmptyObjectCreation()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static Result<int, string> Create() => new Result<int, string>();
            }
            """);

        await AssertSingleDiagnostic(diagnostics, "no zero value");
    }

    [Test]
    public async Task AnalyzerReportsTargetTypedObjectCreation()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static Result<int, string> Create()
                {
                    Result<int, string> result = new();
                    return result;
                }
            }
            """);

        await AssertSingleDiagnostic(diagnostics, "no zero value");
    }

    [Test]
    public async Task AnalyzerReportsArrayCreation()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static Result<int, string>[] Create() => new Result<int, string>[4];
            }
            """);

        await AssertSingleDiagnostic(diagnostics, "no zero value");
    }

    [Test]
    public async Task AnalyzerIgnoresConstructedResults()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static Result<int, string> Successful() => Result.Return<int, string>(1);

                public static Result<int, string> Failed() => Result.Failure<int, string>("invalid");

                public static Result<int, string> Constructed() => new Result<int, string>(new Result<int, string>.Success(1));
            }
            """);

        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == AnalyzerDiagnosticId)).IsFalse();
    }

    [Test]
    public async Task AnalyzerIgnoresDefaultOptions()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static Option<int> Empty() => default;
            }
            """);

        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == AnalyzerDiagnosticId)).IsFalse();
    }

    [Test]
    public async Task AnalyzerIgnoresInitializedAndEmptyArrays()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static Result<int, string>[] Implicit(Result<int, string> first, Result<int, string> second) =>
                    new[] { first, second };

                public static Result<int, string>[] Explicit(Result<int, string> first) =>
                    new Result<int, string>[] { first };

                public static Result<int, string>[] Sized(Result<int, string> first) =>
                    new Result<int, string>[1] { first };

                public static Result<int, string>[] Empty() => new Result<int, string>[0];
            }
            """);

        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == AnalyzerDiagnosticId)).IsFalse();
    }

    [Test]
    public async Task AnalyzerReportsDefaultValidation()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static Validation<int, string> Value() => default;
            }
            """);

        await AssertSingleDiagnostic(diagnostics, "'Validation.Return' or 'Validation.Failure'");
    }

    [Test]
    public async Task AnalyzerReportsEmptyErrorResult()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static ErrorResult<int, string> Value() => new ErrorResult<int, string>();

                public static ErrorResult<int, string> Wrapped() => Result.Failure<int, string>("x").AsErrorResult();
            }
            """);

        await AssertSingleDiagnostic(diagnostics, "'ErrorResult.Return' or 'AsErrorResult()'");
    }

    [Test]
    public async Task AnalyzerReportsUninitializedValidationArrays()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using LondoSpark.Func;

            public static class Sample
            {
                public static Validation<int, string>[] Create(int length) => new Validation<int, string>[length];
            }
            """);

        await AssertSingleDiagnostic(diagnostics, "no zero value");
    }

    private static async Task AssertSingleDiagnostic(ImmutableArray<Diagnostic> diagnostics, string expectedMessage)
    {
        var matching = diagnostics
            .Where(diagnostic => diagnostic.Id == AnalyzerDiagnosticId)
            .ToArray();

        await Assert.That(matching.Length).IsEqualTo(1);
        await Assert.That(matching[0].GetMessage(CultureInfo.InvariantCulture)).Contains(expectedMessage);
        await Assert.That(matching[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(Result<,>).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        return await compilation
            .WithAnalyzers([new ResultHasNoDefaultValueAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
    }
}
