# LondoSpark.Func

Small, dependency-light functional primitives for explicit absence and expected
failure in C# 15.

The project is early in development; its API and package layout may change.

## Projects

- `src/LondoSpark.Func` — core `Option<T>` and `Result<TSuccess, TFailure>` types,
  LINQ query operators, and conversions between `Option<T>` and `Result<T, Unit>`.
- `tests/LondoSpark.Func.Tests` — TUnit tests and FsCheck monad-law properties.
- `docs/package-spec.md` — design and delivery plan.

The planned `LondoSpark.Func.FSharp` project will provide optional conversions to
and from F#'s standard `option` and `Result` types. It is not implemented yet.
When added, it will have its own project directory under `src/` and a matching
test project under `tests/`.

## Build and test

Requires the .NET 11 SDK.

```sh
dotnet build LondoSpark.Func.slnx
dotnet test --project tests/LondoSpark.Func.Tests/LondoSpark.Func.Tests.csproj
```

The library has no runtime dependencies. The test project uses TUnit and
TUnit.FsCheck on Microsoft Testing Platform.

The local BenchmarkDotNet project is deliberately outside the solution and is
not run by CI. BenchmarkDotNet's latest stable release does not recognize the
.NET 11 runtime yet, so use its in-process toolchain to run the benchmarks:

```sh
dotnet run --project benchmarks/LondoSpark.Func.Benchmarks/LondoSpark.Func.Benchmarks.csproj --configuration Release -- --inProcess
```

The VS Code launch configuration uses the same workaround. Allocation
measurements are available, but in-process timings are less isolated; avoid
using them to compare very small performance differences. Use **Ctrl+F5** in
VS Code to run without attaching the debugger, which can further distort timing.

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
    int.TryParse(text, out int value) ? value : "Expected an integer.";

static Result<int, string> RequirePositive(int value) =>
    value > 0 ? value : "Expected a positive number.";

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
    denominator == 0 ? "Division by zero." : numerator / denominator;

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
    id == 42 ? new Product(id, "Answer") : new ProductNotFound();

static Results<Ok<Product>, NotFound> ToHttpResult(
    Result<Product, ProductNotFound> result) =>
    result switch
    {
        Product product => TypedResults.Ok(product),
        ProductNotFound => TypedResults.NotFound()
    };

public sealed record Product(int Id, string Name);
public readonly record struct ProductNotFound;
```

This keeps `Result` independent of ASP.NET Core and makes the domain-to-HTTP
policy visible. An optional web adapter can later reduce this mapping boilerplate
without imposing one error policy on every application.

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
        Result<Product, ProductNotFound> result => result switch
        {
            Product product => $"Product: {product.Name}",
            ProductNotFound => "Product not found."
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
```

This creates the package under `src/LondoSpark.Func/bin/Release`. The GitHub
Actions workflow builds and tests the solution, then uploads the package as a
workflow artifact; it does not publish to NuGet.org.

## Contributing

AI-assisted contributions are welcome. This is a personal project with no Code
of Conduct; the maintainer decides which pull requests to accept and may
decline contributions at their discretion.
