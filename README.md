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

## Using the library

`Option<TSome>` represents either a non-null value or `None`. Convert nullable
references and nullable value types with `ToOption`, then use `Select` or
`SelectMany` to map and compose computations:

```csharp
Option<string> name = Environment.GetEnvironmentVariable("USER").ToOption();
Option<string> greeting = name.Select(value => $"Hello, {value}");
```

`Result<TSuccess, TFailure>` represents either a success or a failure.
`Select` and `SelectMany` operate on successes and preserve failures; `MapError`
and `BindError` operate on failures and preserve successes. The `SelectMany`
overloads enable LINQ query syntax:

```csharp
static Result<double, string> Divide(double numerator, double denominator) =>
    denominator == 0 ? "division by zero" : numerator / denominator;

var calculation =
    from first in Divide(10, 2)
    from second in Divide(10, first)
    select first + second;
```

Use `AsErrorResult()` when you want LINQ queries to bind on failures instead of
successes. Convert between `Option<T>` and `Result<T, Unit>` with `ToResult()`
and `ToOption()`.

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
