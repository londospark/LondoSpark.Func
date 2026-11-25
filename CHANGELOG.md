# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Because it is pre-1.0, a minor version may contain breaking changes.

## [Unreleased]

## [0.4.0-prealpha.1] - 2026-11-25

### Added

- `await` as do-notation. `async Option<T>` methods unwrap each awaited option and
  return `None` at the first absent one (`await none;` ends one explicitly). Results
  need a task-like type with one type parameter, so `[AsyncResult<TFailure>]` on a
  partial struct generates one, such as `Registration<T>` over
  `Result<T, CustomerError>`: a union of the result's cases that converts to and from
  it, where `await failure(error);` fails the method. Both run synchronously.
- `ShortCircuitMethodBuilder<>`, for `async ValueTask` methods that return an option,
  result, or generated result type: they await tasks as usual and short-circuit on an
  absent or failed await.
- A short-circuit unwinds with a `ShortCircuitException`, so `finally` blocks and
  `using` declarations run; methods that finish allocate nothing in Release builds.
  `LSF0003` reports an await whose method cannot return its absent or failed case,
  `LSF0004` a `catch` that would swallow the short-circuit, and `LSF0005` a
  synchronous method that awaits a task. `LSF0103` reports an `[AsyncResult]` type of
  the wrong shape, and `LSF0001` now covers generated result types.

- Examples: `AsyncCheckout` composes plain, `Task`, and `ValueTask` results in one
  query, and again with `await`; `Runbook` runs one release runbook as a dry run, against a simulated machine,
  and stepped by hand to an approval resumed both ways.

### Fixed

- An instruction named `Run` or `RunAsync` no longer trips CA1708 on its holder
  class, whose generated lowercase constructor differs from the interpreter only by
  case.

## [0.3.0-prealpha.1] - 2026-11-25

### Added

- `Program<TOp, T>`, the free (operational) monad over an instruction set you declare
  as a union. Query syntax writes programs; interpreters give instructions meaning, so
  one program runs against real effects, scripted test answers, or a tracer. A program
  is a union of `Done` and `Pending`, so it can be stepped by hand, and a continuation
  can be resumed more than once. Programs run in constant stack space, including
  recursive loops of millions of steps.
- An instruction-set source generator, bundled into the core package:
  `[Instructions<TOp>]` on a static partial class writes lowercase constructors per
  instruction (`ask(prompt)`), `pure`, typed `IHandler`/`IAsyncHandler` interfaces,
  and exhaustive `Run`/`RunAsync` interpreters. `LSF0100`–`LSF0102` report a holder
  class, instruction set, or instruction it cannot use.
- Query syntax over asynchronous options and results: `from` clauses mix plain values,
  `Task`, and `ValueTask`, and the query produces a `ValueTask` that stops at the first
  absent value or failure. Queries whose steps complete synchronously allocate nothing
  beyond their lambdas. A bundled suppressor clears the CA2012 false positive that
  query expressions over `ValueTask` would otherwise raise.
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
- `Option.Some(value)` and `Option.None<T>()`, reading as the cases they create, alongside
  `Option.Return` and `Option.Empty`.
- `LondoSpark.Func.Prelude`, the way to build values without constructors:
  `some(x)`, `none`, `success(x)`, and `failure(e)`, so
  `return taken ? failure(new AlreadyRegistered(email)) : success(customer);` needs no
  `new` and no type arguments. `none` is the shared `None` value. `success` and
  `failure` return small structs that convert implicitly to the result or validation
  the context expects, without allocating; a lambda passed to `Bind` names its return
  type, because C# does not infer through those conversions.
  `<LondoSparkFuncPrelude>true</LondoSparkFuncPrelude>` imports the prelude
  project-wide, and typing a prelude name without the import offers it as a code fix.
- The case-import code fix finds the target type through conditionals, switch arms,
  casts, and assignments as well as returns and declarations.
- An allocation suite holds every non-allocating operation on `Option`, `Result`,
  `ErrorResult`, `Validation`, `NonEmptyList`, the prelude, and the F# value
  conversions to zero bytes, for value-type and reference-type payloads, and bounds
  the deliberate allocations: boxing `Value`, new error lists, in-place appends, and
  interface enumeration. The JSON converters allocate no more than their payload.
  Asynchronous queries that complete synchronously, finished programs, and lifted
  instructions are held to zero bytes; program steps are held to a bound.
- Every `Option<T>` returns the same boxed `None` from `Value`, whatever `T` is.
- Code fixes that import a union's cases: typing `Success(var c)` without the import
  offers `using static LondoSpark.Func.Result<...>;`, or the qualified name when an
  import would clash. `LSF0002` suggests the import for cases written in full, and its
  fix shortens them across the file. The fixes ship in a new
  `LondoSpark.Func.CodeFixes` assembly inside the core package.
- `LondoSpark.Func.Json`: `AddOptionConverter<T>()` and
  `AddResultConverter<TSuccess, TFailure>()` register closed converters without
  reflection, for Native AOT and trimming.
- `LondoSpark.Func.FSharp`: `ToOption()` and `ToFSharpValueOption()` convert to and
  from F# `voption` without allocating.
- Allocation tests that fail if `Option`, `Result`, or `Validation` composition, or
  `NonEmptyList` enumeration, starts allocating; benchmarks for `Validation`,
  `NonEmptyList.Append`, and the JSON converters.

### Changed

- **Breaking:** union cases are nested types (`Option<T>.Some`,
  `Result<TSuccess, TFailure>.Success` and `.Failure`, `Validation<TSuccess,
  TError>.Success` and `.Failure`), replacing the top-level generic `Some<T>`,
  `Success<T>`, and `Failure<T>`. With
  `using static LondoSpark.Func.Result<Customer, CustomerError>;` a switch reads
  `Success(var customer) => ..., Failure(var error) => ...`: exhaustive, with no type
  arguments and no null arm, and `new Success(customer)` converts to the result.
  `Result<T, T>` keeps its cases distinct. `Validation`'s failure case exposes its
  list as `Errors`. `None` is unchanged and shared by every option.

- The examples model a registration failure as one `CustomerError` union, so the
  endpoint and the CLI both compose `Validate(...).Bind(Register)` as a pipeline and
  map the domain error once, instead of nesting a switch per union.
- The mapping helpers cover only the values the constructors produce, so
  `Result.ToExitCode()`, `ToHttpResult(...)`, and `Validation<...>.ToResult()` no longer
  map `default` to a case. A hand-written zero value fails with an
  `InvalidOperationException` naming the constructors instead of being mapped,
  matching `LSF0001` treating zero-initialisation as an error. Every operation on
  `Result`, `Validation`, and `ErrorResult` now throws that exception for a zero
  value, rather than an opaque `SwitchExpressionException`.
- **Breaking:** `Validation<TSuccess, TValidationError>` is a hand-written struct
  union that stores its case inline, so constructing, selecting, and applying a
  valid value no longer boxes. It now has `HasValue`, a `Value` that throws for a zero
  value, `==`/`!=`, and `ToString()` (`Success(1)`, `Failure(a, b)`). Failures
  compare their error list by reference, as before.
- **Breaking:** `NonEmptyList<T>` and `Validation.Failure` take
  `params ReadOnlySpan<T>`, so passing several errors no longer allocates a
  temporary array. `NonEmptyList<T>.Append` reuses spare capacity, so accumulating
  N errors through `Apply` is amortised linear instead of quadratic. Enumeration uses
  a struct enumerator and no longer allocates; `AsSpan()` exposes the items.
- **Breaking:** every `ToHttpResult` overload returns a typed
  `Results<TSuccess, TFailure>` instead of `IResult`, so minimal-API endpoints
  publish both responses to OpenAPI. Callbacks are generic over their result type;
  a callback whose branches return different types needs an explicit `IResult`
  return type.
- **Breaking:** `AddLondoSparkFuncConverters` and the converter factories are marked
  `RequiresDynamicCode` and `RequiresUnreferencedCode`. The JSON converters resolve
  payload type metadata once per serializer configuration instead of per call, and
  their property names follow `PropertyNamingPolicy` and
  `PropertyNameCaseInsensitive`.
- `LSF0001` no longer reports arrays created with an initializer or with length
  zero, and now also reports zero-initialised `Validation`, `ErrorResult`, and
  `Program` values.
- `Option<T>.Value` returns a shared `None` instance instead of boxing a new one,
  and `Some(null)` renders as `Some(null)` rather than `Some()`.
- A `ValidationError` without a field is grouped under the empty-string key, as
  MVC does for model-level errors, instead of throwing.
- The core, JSON, and ASP.NET Core packages set `IsAotCompatible`.
- Union case payloads are captured by the pattern (`Success(var value)`) rather
  than read from the wrapper, and a case that is only propagated is returned as
  itself instead of being unwrapped and rebuilt.

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

[Unreleased]: https://github.com/londospark/LondoSpark.Func/compare/v0.4.0-prealpha.1...HEAD
[0.4.0-prealpha.1]: https://github.com/londospark/LondoSpark.Func/compare/v0.3.0-prealpha.1...v0.4.0-prealpha.1
[0.3.0-prealpha.1]: https://github.com/londospark/LondoSpark.Func/compare/v0.2.0-prealpha.2...v0.3.0-prealpha.1
[0.2.0-prealpha.2]: https://github.com/londospark/LondoSpark.Func/compare/v0.2.0-prealpha.1...v0.2.0-prealpha.2
[0.2.0-prealpha.1]: https://github.com/londospark/LondoSpark.Func/compare/v0.0.1-prealpha.2...v0.2.0-prealpha.1
[0.0.1-prealpha.2]: https://github.com/londospark/LondoSpark.Func/compare/v0.0.1-prealpha.1...v0.0.1-prealpha.2
[0.0.1-prealpha.1]: https://github.com/londospark/LondoSpark.Func/releases/tag/v0.0.1-prealpha.1
