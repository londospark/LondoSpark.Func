# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Because it is pre-1.0, a minor version may contain breaking changes.

## [Unreleased]

### Added

- `ValidationError`, a `(Field, Message)` error that renders as `field: message`, so
  validation failures can be reported per input.
- `LondoSpark.Func.AspNetCore`: one `ToHttpResult` verb maps unions to responses.
  `Option` becomes `200`/`404`, `Result` becomes `200` or the result your failure
  handler returns, and `Validation` becomes `200` or a field-keyed `400`. Any
  validation error type works by projecting it onto `ValidationError`, which needs no
  projection. These are shorthands: a flow with steps reads better as a switch
  expression, which the compiler keeps exhaustive.
- `NonEmptyList<ValidationError>.ToValidationProblem()` builds the field-keyed `400`
  payload on its own, so a switch expression can handle the failure case directly.
- `Validation<TSuccess, TValidationError>.ToResult()` converts the accumulated errors
  into a single failure value, so a validation can feed a `Result` pipeline once
  gathering finishes.
- `NonEmptyList<T>.ToSeparatedString(separator)` for turning accumulated errors into
  one message.
- `Result<TSuccess, TFailure>.ToExitCode()` maps a result to `0` or `1` for CLIs.

### Changed

- The examples model a registration failure as one `CustomerError` union, so the
  endpoint and the CLI both compose `Validate(...).Bind(Register)` as a pipeline and
  map the domain error once, instead of nesting a switch per union.

## [0.2.0-prealpha.2] - 2026-11-24

### Added

- `Option<TSome>` and `Result<TSuccess, TFailure>` implement `IEquatable<T>` and expose
  `==`/`!=`. Equality compares the active case and its payload, so the inactive payload
  is ignored and comparing two options or results no longer falls back to
  reflection-based `ValueType.Equals`; `GetHashCode` hashes only the active case.
- `ToString()` on both unions renders the active case (`Some(42)`, `None`,
  `Success(42)`, `Failure(error)`, `Result(default)`), so logs are readable.
- `LondoSpark.Func.Json`: `System.Text.Json` converters with factories, so one
  registration covers every closed generic type. `Option` is `null` for `None` and
  `{"some": value}` for `Some`; a result is `{"success": value}` or
  `{"failure": error}`. Reading is streaming and allocates no document. The package
  adds its own implicit using, controlled by `LondoSparkFuncImplicitUsings`.
- `examples/`: a domain library, a console pipeline, an ASP.NET Core minimal API, a
  JSON round-trip, and an F# consumer. They build with the solution and record the
  helpers they had to write themselves.

## [0.2.0-prealpha.1] - 2026-11-24

First release containing the inline union representation, the analyzer, and the F#
interop package. `0.1.0-prealpha.1` and `0.1.0-prealpha.2` were repository-only
iterations that were never tagged or published; their changes are folded in here.

### Added

- `LondoSpark.Func.Analyzers`, bundled into the core package: `LSF0001` reports
  hand-written zero values of `Result<TSuccess, TFailure>` as an **error** by default.
- A project-wide `global using LondoSpark.Func;` for C# consumers, shipped as
  `buildTransitive` assets and controlled by `LondoSparkFuncImplicitUsings`.
- `Option<TSome>.HasValue` and typed `TryGetValue` accessors on both `Option<TSome>`
  and `Result<TSuccess, TFailure>`, so case switches match without boxing.
- `LondoSpark.Func.FSharp` package with conversions between the core types and F#
  `option`/`Result`, preserving nullable payloads.
- `docs/package-spec.md`, a README, and a GitHub Actions pipeline that builds, tests,
  and packs both packages as artifacts.

### Changed

- `Option<TSome>` and `Result<TSuccess, TFailure>` are now hand-written readonly
  structs that implement the C# union pattern with inline storage. Case switches keep
  exhaustive matching and no longer box the active case; `Some<T>` / `None` and
  `Success<T>` / `Failure<T>` patterns are unchanged.
- `default(Option<TSome>)` is `None`, and every option contains a case, so `HasValue`
  is always `true` and `Value` never returns `null`.
- `Option<TSome>.Value` and `Result<TSuccess, TFailure>.Value` are non-nullable
  `object`; reading `Value` on a default `Result` throws `InvalidOperationException`.
- `LondoSpark.Func.FSharp` targets `net11.0` and depends on the matching core package.

### Breaking changes

- `default(Result<TSuccess, TFailure>)` and `new Result<TSuccess, TFailure>()` contain
  no case: case patterns do not match and `Value` throws. Construct results with
  `Result.Return` or `Result.Failure`.
- The zero value of `Option<TSome>` changed from a valueless union to `None`, and the
  union struct layout changed, so already-compiled consumers must recompile.
- `LSF0001` is an error by default, and the global using is on by default. Both can be
  disabled by consumers (`.editorconfig` severity, or
  `<LondoSparkFuncImplicitUsings>false</LondoSparkFuncImplicitUsings>`).
- Packages target `net11.0` and require a C# 15 compiler.

## [0.0.1-prealpha.2] - 2026-11-23

### Added

- Lawful composition and law-based tests for `Option`, `Result`, and `Validation`.
- `Validation<TSuccess, TFailure>` error accumulation and BenchmarkDotNet benchmarks.

## [0.0.1-prealpha.1] - 2026-11-23

### Added

- `Option<TSome>`, `Result<TSuccess, TFailure>`, `ErrorResult<TSuccess, TFailure>`,
  `Unit`, LINQ query operators, and `Option`/`Result` conversions.

[Unreleased]: https://github.com/londospark/LondoSpark.Func/compare/v0.2.0-prealpha.2...HEAD
[0.2.0-prealpha.2]: https://github.com/londospark/LondoSpark.Func/compare/v0.2.0-prealpha.1...v0.2.0-prealpha.2
[0.2.0-prealpha.1]: https://github.com/londospark/LondoSpark.Func/compare/v0.0.1-prealpha.2...v0.2.0-prealpha.1
[0.0.1-prealpha.2]: https://github.com/londospark/LondoSpark.Func/compare/v0.0.1-prealpha.1...v0.0.1-prealpha.2
[0.0.1-prealpha.1]: https://github.com/londospark/LondoSpark.Func/releases/tag/v0.0.1-prealpha.1
