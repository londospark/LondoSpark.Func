# Examples

Small, self-contained projects that use `LondoSpark.Func` the way an application
would. They build with the rest of the solution, so they cannot drift from the API.

| Example | Project type | Shows |
| ------- | ------------ | ----- |
| [`DomainModeling`](DomainModeling) | class library | `Validation` accumulating field-keyed errors, `Option` for lookup, `Result` for expected failure |
| [`ConsolePipeline`](ConsolePipeline) | console app | argument parsing with `ToOption`, `Option`/`Result` composition, `ErrorResult` enrichment, `ToSeparatedString`, `ToExitCode` |
| [`MinimalApi`](MinimalApi) | ASP.NET Core minimal API | `ToOkOrNotFound`, `ToHttpResult`, and field-keyed `400` problems from `LondoSpark.Func.AspNetCore` |
| [`JsonSerialization`](JsonSerialization) | console app | `LondoSpark.Func.Json` converters that round-trip `Option<T>` and `Result<TSuccess, TFailure>` |
| [`FSharpPipeline`](FSharpPipeline) | F# console app | lossless F# `option`/`Result` round-trips through the core types |

## Running

```sh
dotnet run --project examples/ConsolePipeline -- seed
dotnet run --project examples/MinimalApi
dotnet run --project examples/JsonSerialization
dotnet run --project examples/FSharpPipeline
```

## What the examples ask of the library

These projects deliberately do their own glue where the library has no helper yet,
so the gaps are visible:

- **ASP.NET Core results.** `MinimalApi` maps unions to `IResult` by hand. A
  `LondoSpark.Func.AspNetCore` package could map `Option` → 404, `Result` → a chosen
  status, and `Validation` → `ValidationProblem` consistently.
- **CLI exit codes.** `ConsolePipeline` maps a `Result` to a process exit code by
  hand; a small adapter would remove that boilerplate.
- **Validation errors as a message.** Turning `NonEmptyList<string>` into a single
  user-facing sentence is common enough to deserve a helper.
- **Validation keyed by field.** `ValidationProblem` wants a field-to-messages map,
  but `Validation<TSuccess, string>` only carries messages. A keyed failure type or a
  helper would be needed for field-level errors.
