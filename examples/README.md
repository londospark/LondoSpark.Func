# Examples

Small, self-contained projects that use `LondoSpark.Func` the way an application
would. They build with the rest of the solution, so they cannot drift from the API.

| Example | Project type | Shows |
| ------- | ------------ | ----- |
| [`DomainModeling`](DomainModeling) | class library | `Validation` accumulating field-keyed errors, `ToResult` folding them into one domain failure, `Option` for lookup, `Result` for sequencing |
| [`ConsolePipeline`](ConsolePipeline) | console app | argument parsing with `ToOption`, case switches through `using static` imports and an alias, `ErrorResult` enrichment, `ToSeparatedString`, `ToExitCode` |
| [`MinimalApi`](MinimalApi) | ASP.NET Core minimal API | one `Validate(...).Bind(Register).ToHttpResult(...)` pipeline, `ToHttpResult()` for a plain map, and field-keyed `400` problems from `LondoSpark.Func.AspNetCore` |
| [`JsonSerialization`](JsonSerialization) | console app | `LondoSpark.Func.Json` converters that round-trip `Option<T>` and `Result<TSuccess, TFailure>` |
| [`FSharpPipeline`](FSharpPipeline) | F# console app | lossless F# `option`/`Result` round-trips through the core types |
| [`Scripting`](Scripting) | console app | a guessing game written as a `Program<GameOp, int>` over a generated instruction set, run by a terminal interpreter or, with `--demo`, a replay interpreter |

## Running

```sh
dotnet run --project examples/ConsolePipeline -- list
dotnet run --project examples/MinimalApi
dotnet run --project examples/JsonSerialization
dotnet run --project examples/FSharpPipeline
dotnet run --project examples/Scripting -- --demo
```

## What the examples ask of the library

The examples build against the shipped packages, so a helper that is missing shows up
as glue in the example rather than as a note here. The one piece of glue left:

- **Arguments as options.** `ConsolePipeline` calls
  `args.ElementAtOrDefault(n).ToOption()` for each positional argument, which obscures
  the intent. A boundary helper for indexed arguments would remove those repetitions.
