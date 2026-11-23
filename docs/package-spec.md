# LondoSpark.Func package specification

## 1. Product direction

LondoSpark.Func should be a small, C#-first functional library built around C# 15 union
types, with optional LINQ query syntax and F# interoperability. It may take inspiration
from broader functional libraries, but its initial scope should stay focused on value
types for composition and on making language boundaries safer.

The package is worth pursuing when it solves a recurring problem in mixed C#/F#
solutions: representing absence and expected failure explicitly, composing those
operations without nested conditionals, and converting values at language boundaries
without handwritten case switches.

It is not initially intended to be a replacement for a full effects, streaming, or
general-purpose functional programming ecosystem.

## 2. Package boundaries

### `LondoSpark.Func`

The dependency-light core:

- `Option<TSome>` with `Some<TSome>` and `None` cases.
- `Result<TSuccess, TFailure>` with `Success<TSuccess>` and `Failure<TFailure>` cases.
- `Return`, `Bind`, and mapping operations.
- `MapError` and `BindError` for failure-side transformations.
- `ToOption` conversions from nullable references and nullable value types.

The core should not depend on FSharp.Core or Roslyn.

`Validation<TSuccess, TFailure>` is an applicative for independent checks. It
accumulates failures in an ordered, non-empty list and intentionally does not
expose `Bind`.

### `LondoSpark.Func.Generators`

An optional Roslyn incremental source generator that emits repetitive, type-specific
LINQ operators from explicit monad metadata. Generated code is compile-time code; it
must not use runtime reflection or add runtime dispatch.

### `LondoSpark.Func.FSharp`

An optional interop package referencing FSharp.Core. It supplies explicit conversions
between the core types and standard F# `option` and `Result` types. Users who do not
use F# should not acquire this dependency.

### ASP.NET Core and Blazor integrations

Optional adapters may provide framework-specific helpers without adding ASP.NET Core
or Blazor dependencies to `LondoSpark.Func`. Keep HTTP response policy out of the core
types. Decide whether Blazor needs a separate package only when its dependencies or
supported targets differ from the ASP.NET Core integration.

Tests and samples should live in separate projects from the distributable library.
The C# test project uses TUnit and TUnit.FsCheck on Microsoft Testing Platform;
the F# compatibility project is an executable consumer check.

Keep each project in its own directory under `src/`. Put test projects under `tests/`
and name each after the project it tests, followed by `.Tests` (for example,
`tests/LondoSpark.Func.Tests`).

## 3. Core API and semantics

### Option

`Option<TSome>` represents either a present `TSome` or the payload-free `None` case.
When `TSome` is nullable, `Some(null)` remains distinct from `None`.
`Option.Return(value)` constructs the present case. `ToOption` maps nullable boundary
data to the present case when it has a value and to `None` otherwise.

Binding a present value invokes the binder. Binding `None` returns `None` without
invoking it. Mapping follows the same case behavior.

### Result

`Result<TSuccess, TFailure>` represents one success or failure value. Nullable
payloads retain their case identity through the explicit `Success<T>` and
`Failure<T>` wrappers.
Success-side `Bind` invokes the binder only for success and preserves failures.
`MapError` changes a failure value while preserving success. `BindError` invokes its
binder only for failure and preserves success.

`ErrorResult<TSuccess, TFailure>` is an optional error-focused view over `Result`, not
a second union. It supports LINQ query syntax that binds on failures and converts back
to `Result` without changing the represented case.

### LINQ operators

The basic query-syntax surface is:

- `Select`
- `SelectMany` with one selector
- `SelectMany` with a collection selector and result selector

The result-selector overload must use distinct intermediate and output type
parameters. For example, binding a `Result<double, E>` and projecting to a string
must produce `Result<string, E>`.

Do not generate `Where` by default. Filtering a `Result` requires an error value for a
failed predicate, and that policy cannot be inferred. Add `Where` only when a monad
provides an explicit filtering policy.

### Functor and applicative operations

`Select` transforms an active value without unwrapping its surrounding case.
`Apply` takes a wrapped function first and a wrapped value second so independent
computations can be combined.

- `Option.Apply` returns `None` when either input is absent.
- `Result.Apply` is fail-fast; if both inputs fail, the function-side failure
  takes precedence, matching left-to-right composition.
- `ErrorResult.Apply` is failure-focused; success cases pass through.
- `Validation.Apply` accumulates function-side then value-side errors in a
  non-empty list.

No general `Monoid<T>` interface is needed for ordered error-list accumulation.
Add one only if consumers need configurable combination policies.

## 4. Monad primitives and source generation

`Return` and `Bind` are the monadic primitives; `Apply` combines independent
computations. LINQ operators should be adapters over the monadic primitives:

- one-selector `SelectMany` delegates to `Bind`;
- `Select` is `Bind` followed by `Return`;
- result-selector `SelectMany` binds the first value, maps the inner value through
  the projector, and preserves the first value for that projection.
- `Apply` uses the function-container-first convention and preserves each type's
  documented failure semantics.

The generator should target tagged-union monads with one explicitly selected
type-parameter focus and one or more pass-through cases. Metadata must say which case
is bound and how inactive cases are preserved. A union's shape alone does not define
its monadic meaning.

The first generator version should:

- emit `Select`, `Bind`, `Apply`, and the LINQ operators above;
- preserve generic constraints and nullability annotations;
- support `Option`, success-focused `Result`, and the error-focused `Result` view;
- emit deterministic extension methods and useful diagnostics for unsupported
  declarations;
- have no runtime dependency.

The generator should not claim to support arbitrary monads. C# has no higher-kinded
types, so generated operators are concrete methods for each declared type. Start with
explicit metadata; inspect C# 15 union compiler symbols before deciding whether the
generator can reliably infer cases from union declarations.

## 5. F# interoperability

### Standard types

The first interop release should support:

- `FSharpOption<T>` to and from `Option<T>`;
- F# `Result<T, E>` to and from `Result<T, E>`.

The adapters should be explicit and typed, with no reflection. They should preserve
the case and payload exactly, including nullable payloads.

### Null policy

Nullable reference annotations guide C# callers; the core does not add runtime
null checks. `Option<T>` and `Result<TSuccess, TFailure>` keep null payloads inside
their explicit cases, so `Some null`, `Ok null`, and `Error null` remain distinct.
The F# adapters preserve those cases without reflection. At the CLR boundary,
F# `None` is represented by a null `FSharpOption<T>` reference; recognizing that
representation is case decoding, while nullable payloads remain inside their cases.

### Custom F# discriminated unions

Custom DU adapters are a later feature. They require explicit case and field mappings;
matching by case name or field position alone is not a safe general conversion rule.
If implemented, a generator should produce typed, reflection-free adapters from
explicit mapping declarations. Runtime reflection can be considered as a separate,
opt-in utility, not as the default path.

## 6. Law and behavior tests

FsCheck properties run under TUnit and should verify functor, applicative, and
monad laws using case-aware equality helpers rather than assuming every generated
union has a particular equality implementation.

For each functor, verify identity and composition. For each applicative, verify
identity, homomorphism, interchange, and composition. For each monad, verify:

For each supported focus:

1. **Left identity:** `Return(a).Bind(f)` is equivalent to `f(a)`.
2. **Right identity:** `m.Bind(Return)` is equivalent to `m`.
3. **Associativity:** `m.Bind(f).Bind(g)` is equivalent to
   `m.Bind(a => f(a).Bind(g))`.

Cover both active and pass-through cases:

- `Option`: present and `None`;
- `Result`: success and failure;
- error-focused `Result`: failure and success.

Also test the generated LINQ operators against equivalent `Bind`/`Map` expressions,
including a projector whose input and output types differ. Interop tests should cover
both conversion directions, every case, and nullable payload preservation.

Keep deterministic regression tests for edge cases alongside FsCheck properties. The
current unary `SelectMany` operation is the bind operation under test; when an explicit
`Bind` API is introduced, run the same laws against it.

## 7. How this becomes useful

- **C# applications consuming F# libraries:** convert F# option/result return values
  into C# unions at service or application boundaries, then compose them with C# LINQ
  query syntax.
- **F# applications consuming C# libraries:** convert C# option/result values into
  idiomatic F# values without changing the C# library's public API.
- **Incremental language migration:** keep a domain or validation layer in F# while
  moving APIs, UI, or infrastructure to C#, or the reverse.
- **Null-heavy .NET boundaries:** turn nullable framework results into `Option<T>`
  once, then keep the rest of the C# code explicit about absence.
- **Domain errors:** use `Result<TSuccess, TFailure>` for expected failures and compose
  transformations without treating exceptions as ordinary control flow.
- **Custom domain unions:** eventually generate adapters between explicitly mapped C#
  and F# DUs, reducing handwritten conversion switches in mixed-language systems.

## 8. Delivery plan

### Phase 1: stabilize core

- Make `Return` and `Bind` the named primitives for `Option` and `Result`.
- Keep `MapError` and `BindError` explicit.
- Preserve the optional error-focused wrapper.
- Move tests into a dedicated test project.
- Add case-aware monad-law tests and tests for distinct result-selector output types.
- Expose functional `Bind` and `Apply` operations in addition to query syntax.
- Verify functor, applicative, and monad laws for each applicable type.
- Add `Validation` for independent checks that should accumulate ordered errors.

### Phase 2: F# standard interop

- Add the optional FSharp.Core adapter package.
- Implement strict standard Option and Result conversions.
- Test null handling and round trips from both C# and F# projects.

### Phase 3: ASP.NET Core and Blazor integrations

- Identify concrete hosting scenarios and supported framework targets.
- Add optional adapters without introducing web-framework dependencies into the core.
- Keep HTTP status and error-response policy explicit and caller-controlled.
- Split Blazor-specific helpers into a separate package only if its dependencies or
  target frameworks require that boundary.

### Phase 4: source generator

- Prototype the generator against `Option` and `Result`.
- Define explicit metadata for focus and pass-through cases.
- Generate the LINQ operator surface and compare its behavior with the hand-written
  reference implementation.
- Keep generated files inspectable and diagnostics actionable.

### Phase 5: custom DU interop

- Add only when a real mixed-language use case requires it.
- Specify mapping metadata and generated conversion diagnostics.
- Test generic, multi-field, and similarly named cases before calling the mapping
  model general.

## 9. Release and compatibility criteria

Before publishing:

- choose supported target frameworks and the minimum C# compiler required for union
  declarations;
- verify F# consumers can use the emitted public C# API;
- keep generator and F# dependencies optional;
- document nullability, equality, and conversion behavior;
- publish versioned packages with compatibility tests for each supported target.

Keep the core release intentionally small. Standard F# interop and framework
integrations should remain optional packages. Add source generation only after the
core contract is stable and its value exceeds the maintenance cost; broader
LanguageExt-like abstractions should be driven by demonstrated use cases.
