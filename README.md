# LondoSpark.Func

Small, dependency-light functional primitives for explicit absence and expected
failure in C# 15.

The project is early in development; its API and package layout may change.

## Projects

- `src/LondoSpark.Func` — core `Option<T>` and `Result<TSuccess, TFailure>` types,
  LINQ query operators, and conversions between `Option<T>` and `Result<T, Unit>`.
- `src/LondoSpark.Func.Analyzers` — Roslyn analyzers, bundled into the core package.
- `src/LondoSpark.Func.Json` — `System.Text.Json` converters for `Option<T>` and
  `Result<TSuccess, TFailure>`.
- `src/LondoSpark.Func.AspNetCore` — maps options, results, and validations to HTTP
  results and field-keyed validation problems.
- `src/LondoSpark.Func.FSharp` — typed conversions to and from F# `option` and
  `Result`, including values whose payload types are nullable.
- `tests/LondoSpark.Func.Tests` — TUnit tests and FsCheck monad-law properties.
- `tests/LondoSpark.Func.FSharp.Compatibility` — an F# consumer smoke test for
  the C# interop package.
- `docs/package-spec.md` — design and delivery plan.

The F# adapter is optional; consumers of the core package do not acquire
FSharp.Core unless they reference `LondoSpark.Func.FSharp`.

## Build and test

Requires the .NET 11 SDK.

```sh
dotnet build LondoSpark.Func.slnx
dotnet test --project tests/LondoSpark.Func.Tests/LondoSpark.Func.Tests.csproj
dotnet run --project tests/LondoSpark.Func.FSharp.Compatibility/LondoSpark.Func.FSharp.Compatibility.fsproj
```

The core library has no runtime dependencies; the F# adapter depends on
FSharp.Core. The test project uses TUnit and TUnit.FsCheck on Microsoft Testing
Platform. The F# compatibility project verifies that both adapters preserve
option/result cases and payloads.

Nullable reference annotations guide C# callers. `Option<T>` and
`Result<TSuccess, TFailure>` preserve nullable payloads in their explicit cases:
`Some(null)` remains distinct from `None`, and a null success remains distinct
from a null failure. In C#, use nullable type arguments such as `string?` when
a payload may be nullable. With nullable checking enabled, F# has corresponding
nullness types, such as `string | null`, and warns when a nullable value is used
where a non-null value is expected.

`Option<T>` and `Result<TSuccess, TFailure>` store their active case inline and
implement the C# union pattern, so `Some<T>` / `None` and `Success<T>` /
`Failure<T>` switch expressions keep exhaustive matching without boxing the
case. Reading the object-typed `Value` protocol property directly boxes the
case; ordinary case switches use the typed accessors instead.

A struct always has a zero value, so both types define what `default` means.
`default(Option<T>)` is `None`, and every option contains a case. `Result` has
no natural zero value, so `default(Result<TSuccess, TFailure>)` contains no
case: `HasValue` is `false`, the case patterns do not match, and `Value` throws
`InvalidOperationException` rather than reporting a fabricated success or
failure.

These annotations are compile-time guidance, not runtime validation. The core
does not add defensive null checks: explicitly nullable values can be carried
through `Option` and `Result`, but dereferencing or invoking a null payload
without handling it can fail at runtime. Suppressing warnings or passing
unannotated values across interop boundaries does not make them non-null.
`ToOption()` is the boundary helper that treats nullable input as absence;
directly constructing a `Some` or a `Success`/`Failure` preserves that case and
payload.

`Option<TSome>` and `Result<TSuccess, TFailure>` compare structurally: values are
equal when they hold the same case with equal payloads. They implement
`IEquatable<T>` and expose `==` and `!=`. `ToString()` renders the active case
(`Some(42)`, `None`, `Success(42)`, `Failure(error)`), so log output is readable.

`LondoSpark.Func.Json` supplies the converters when these values cross a JSON
boundary: an option is `null` for `None` or `{"some": value}` for `Some`, and a
result is `{"success": value}` or `{"failure": error}`. The object wrapper keeps
`Some(null)` distinct from `None`.

```csharp
var options = new JsonSerializerOptions { WriteIndented = true };
options.AddLondoSparkFuncConverters();
```

`LondoSpark.Func.AspNetCore` maps the same values to endpoints. `ValidationError`
carries a field name and a message, so a validation problem is grouped the way a
form expects:

```csharp
app.MapGet("/customers/{id:int}", (int id) => directory.Find(id).ToHttpResult());

app.MapPost("/customers", (CustomerRequest request) =>
    CustomerValidation.Validate(request) switch
    {
        Success<NewCustomer> validated => directory.Register(validated.Value) switch
        {
            Success<Customer> customer =>
                TypedResults.Created($"/customers/{customer.Value.Id}", customer.Value),
            Failure<ValidationError> failure => TypedResults.Conflict(failure.Value.Message)
        },
        Failure<NonEmptyList<ValidationError>> errors => errors.Value.ToValidationProblem()
    });
```

`ToHttpResult` is the shorthand when a value maps in one call: an option is `200`
or `404`, a result is `200` or the result your failure handler returns, and a
validation is `200` or a field-keyed `400`. When the flow has steps of its own, a
switch expression reads better and the compiler keeps it exhaustive. Any
validation error type works by projecting it onto `ValidationError`;
`Validation<TSuccess, ValidationError>` needs no projection, and
`NonEmptyList<ValidationError>.ToValidationProblem()` builds the `400` payload for a
case handled on its own.
`NonEmptyList<T>.ToSeparatedString` joins messages, and `Result.ToExitCode()` maps a
CLI result to a process exit code.

The local BenchmarkDotNet project is deliberately outside the solution and is
not run by CI. It uses BenchmarkDotNet 0.16.0-preview.2 for .NET 11 support.
Run a short comparison of the boxed and inline Option and Result
representations with:

```sh
dotnet run --project benchmarks/LondoSpark.Func.Benchmarks/LondoSpark.Func.Benchmarks.csproj --configuration Release -- --filter '*RepresentationBenchmarks*' --job Short
```

Short-run timings are directional; the memory diagnoser is useful for checking
whether case matching allocates. Use **Ctrl+F5** in VS Code to run without
attaching the debugger, which can distort timing.

## Examples

[`examples/`](examples) contains small projects that use the library the way an
application would: a domain library, a console pipeline, an ASP.NET Core minimal
API, `System.Text.Json` converters, and an F# consumer. They build with the
solution, and their README records the helpers the examples had to write
themselves.

## Analyzers

The core package ships a Roslyn analyzer for C# consumers, so no extra reference
is needed. `Result<TSuccess, TFailure>` is a struct, so `default`, `new()`, and
empty arrays can produce a value with no case; `Option<TSome>` needs no rule
because its zero value is `None`.

| Rule | Default | Detects |
| ---- | ------- | ------- |
| `LSF0001` | Error | `default(Result<...>)`, `default` literals, `new Result<...>()`, and `new Result<...>[n]` |

The library never creates such a value, so the rule only fires on hand-written
zero values. Zeroing the analyzer cannot see — unassigned fields, array elements
zeroed elsewhere, and generic `default` — is still rejected at runtime by
`HasValue` and `Value`. If an error is too strict while migrating, lower it:

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.LSF0001.severity = warning
```

## Implicit usings

The package adds a project-wide `global using LondoSpark.Func;` by default, so
`Option<T>` and `Result<TSuccess, TFailure>` are available without a per-file
`using`. It is a `Using` item shipped in the package's `buildTransitive` assets —
the same mechanism TUnit uses for `TUnit.Core`.

Because `Option`, `Result`, `Some`, `None`, `Success`, and `Failure` are common
names, opt out when they collide with another library:

```xml
<PropertyGroup>
  <LondoSparkFuncImplicitUsings>false</LondoSparkFuncImplicitUsings>
</PropertyGroup>
```

Without the global using, add `using LondoSpark.Func;` per file, or add it
yourself to a `GlobalUsings.cs`.

Both packages deliver the analyzer and the global using. `LondoSpark.Func.FSharp`
depends on the whole of `LondoSpark.Func`, so referencing the F# package alone is
enough for C# code in the same project. `LondoSpark.Func.Json` adds
`global using LondoSpark.Func.Json;` under the same switch.

## Pre-release risks

This is a pre-release. The following behaviours are deliberate and can break a
consumer on upgrade:

- **`LSF0001` is an error by default.** Code that zero-initialises a
  `Result<TSuccess, TFailure>` stops compiling. Lower it with
  `dotnet_diagnostic.LSF0001.severity = warning` in `.editorconfig`.
- **The global using is on by default.** Projects that also import another library
  defining `Option`, `Result`, `Some`, `None`, `Success`, `Failure`, or `Validation`
  can hit CS0104 ambiguity; set
  `<LondoSparkFuncImplicitUsings>false</LondoSparkFuncImplicitUsings>`.
- **The zero-value contract changed.** `default(Option<T>)` is now `None`;
  `default(Result<TSuccess, TFailure>)` contains no case and throws.
- **`net11.0` and C# 15 only.** Consumers must target .NET 11 with a compiler that
  supports union types.
- **No compatibility baseline.** Nothing has been published to NuGet.org, so package
  validation and public-API baselines are not enforced yet; a pre-1.0 minor version
  may contain breaking changes.

## Using the library

### Fluent composition

Use `Option<T>` when a missing value is an expected outcome. Convert nullable
data at the boundary, then use `Bind` to compose steps that can also produce no
value. `None` skips the remaining steps:

```csharp
static Option<int> ParseInteger(string text) =>
    int.TryParse(text, out int value) ? Option.Return(value) : new None();

static Option<int> Divide(int numerator, int denominator) =>
    denominator == 0 ? new None() : Option.Return(numerator / denominator);

Option<int> quotient = ParseInteger("4")
    .Bind(divisor => Divide(100, divisor));
```

Use `Result<TSuccess, TFailure>` when callers need to know why an expected
operation failed. `Bind` composes success-dependent work and short-circuits on
the first failure. `Select` transforms a success; `MapError` adapts its failure
type:

```csharp
static Result<int, string> ParseInteger(string text) =>
    int.TryParse(text, out int value)
        ? Result.Return<int, string>(value)
        : Result.Failure<int, string>("Expected an integer.");

static Result<int, string> RequirePositive(int value) =>
    value > 0
        ? Result.Return<int, string>(value)
        : Result.Failure<int, string>("Expected a positive number.");

Result<int, string> result = ParseInteger("42")
    .Bind(RequirePositive)
    .Select(value => value * 2)
    .MapError(error => $"Invalid input: {error}");
```

Use `Apply` when the function and its input are independent computations.
`Option` short-circuits on absence, and `Result` keeps its fail-fast behavior:

```csharp
Result<Func<int, string>, string> format =
    Result.Return<Func<int, string>, string>(id => $"Product {id}");
Result<int, string> productId = Result.Return<int, string>(42);

Result<string, string> label = format.Apply(productId);
```

For independent validations, `Validation<T, TError>` accumulates errors in
order instead of stopping at the first one. This is intentionally Applicative,
not monadic: a later check can run even when an independent earlier check fails.

```csharp
var makeFullName = Validation.Return<Func<string, Func<string, string>>, string>(
    first => last => $"{first} {last}");
var firstName = Validation.Failure<string, string>("First name is required.");
var lastName = Validation.Failure<string, string>("Last name is required.");

Validation<string, string> fullName =
    makeFullName.Apply(firstName).Apply(lastName);
```

`SelectMany` is also available for LINQ query syntax. This is useful when a
computation reads several values and a final projection combines them; it uses
the same short-circuiting behavior as fluent `Bind`:

```csharp
static Result<int, string> Divide(int numerator, int denominator) =>
    denominator == 0
        ? Result.Failure<int, string>("Division by zero.")
        : Result.Return<int, string>(numerator / denominator);

var calculation =
    from numerator in ParseInteger("100")
    from denominator in ParseInteger("4")
    from quotient in Divide(numerator, denominator)
    select quotient;
```

Use `AsErrorResult()` when a pipeline should compose on failures instead of
successes, for example to add context or recover. Convert between `Option<T>`
and `Result<T, Unit>` when absence is the only failure information required.

### ASP.NET Core minimal APIs

The core package deliberately does not choose HTTP status codes for your
domain. Map each domain failure to an HTTP response at the endpoint boundary:

```csharp
using LondoSpark.Func;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

var app = WebApplication.CreateBuilder(args).Build();
app.MapGet("/products/{id:int}", (int id) => ToHttpResult(FindProduct(id)));
app.Run();

static Result<Product, ProductNotFound> FindProduct(int id) =>
    id == 42
        ? Result.Return<Product, ProductNotFound>(new Product(id, "Answer"))
        : Result.Failure<Product, ProductNotFound>(new ProductNotFound());

static Results<Ok<Product>, NotFound> ToHttpResult(
    Result<Product, ProductNotFound> result) =>
    result switch
    {
        Success<Product> success => TypedResults.Ok(success.Value),
        Failure<ProductNotFound> => TypedResults.NotFound()
    };

public sealed record Product(int Id, string Name);
public readonly record struct ProductNotFound;
```

This keeps `Result` independent of ASP.NET Core and makes the domain-to-HTTP
policy visible. `LondoSpark.Func.AspNetCore` supplies the shorthand for the shapes
every API repeats — `ToHttpResult()` is an option's `200`/`404`, a result's `200`
plus your failure handler, and a validation's `200`/field-keyed `400` — without
choosing an error policy for you.

### Blazor components

Components can keep loading, success, and expected failure explicit using
`Option<Result<...>>`. This example assumes `ProductService.FindAsync` returns
`Task<Result<Product, ProductNotFound>>`:

```razor
@page "/products/{ProductId:int}"
@using LondoSpark.Func
@inject ProductService Products

<p role="status">@ProductStatus</p>

@code {
    [Parameter] public int ProductId { get; set; }

    private Option<Result<Product, ProductNotFound>> _productResult = new None();

    private string ProductStatus => _productResult switch
    {
        None => "Loading product...",
        Some<Result<Product, ProductNotFound>> some => some.Value switch
        {
            Success<Product> success => $"Product: {success.Value.Name}",
            Failure<ProductNotFound> => "Product not found."
        }
    };

    protected override async Task OnParametersSetAsync() =>
        _productResult = await Products.FindAsync(ProductId);
}
```

The component owns the user-facing presentation while the service communicates
success or an expected domain failure. These examples use the core API directly;
ASP.NET Core and Blazor adapters are not included in this package.

The public API is documented with XML comments. IDEs show these descriptions
for library symbols, and the generated XML documentation ships in the NuGet
package.

## Create a NuGet package

The project is licensed under the MIT License. See [LICENSE](LICENSE).

```sh
dotnet pack src/LondoSpark.Func/LondoSpark.Func.csproj --configuration Release
dotnet pack src/LondoSpark.Func.FSharp/LondoSpark.Func.FSharp.csproj --configuration Release
```

These create packages under each project's `bin/Release` directory. The GitHub
Actions workflow builds and tests the solution, then uploads both packages as a
workflow artifact; it does not publish to NuGet.org.

## Contributing

AI-assisted contributions are welcome. This is a personal project with no Code
of Conduct; the maintainer decides which pull requests to accept and may
decline contributions at their discretion.
