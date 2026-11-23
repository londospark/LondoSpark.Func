# LondoSpark.Func

Small, dependency-light functional primitives for explicit absence and expected
failure in C# 15.

The project is early in development; its API and package layout may change.

## Projects

- `src/LondoSpark.Func` — core `Option<T>` and `Result<TSuccess, TFailure>` types,
  LINQ query operators, and conversions between `Option<T>` and `Result<T, Unit>`.
- `Tests/LondoSpark.Func.Tests` — TUnit tests and FsCheck monad-law properties.
- `docs/package-spec.md` — design and delivery plan.

The planned `LondoSpark.Func.FSharp` project will provide optional conversions to
and from F#'s standard `option` and `Result` types. It is not implemented yet.
When added, it will have its own project directory under `src/` and a matching
test project under `Tests/`.

## Build and test

Requires the .NET 11 SDK.

```sh
dotnet build LondoSpark.Func.slnx
dotnet test --project Tests/LondoSpark.Func.Tests/LondoSpark.Func.Tests.csproj
```

The library has no runtime dependencies. The test project uses TUnit and
TUnit.FsCheck on Microsoft Testing Platform.
