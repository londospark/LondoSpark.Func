open LondoSpark.Func.FSharp

let assertEqual name expected actual =
    if expected <> actual then
        failwithf "%s: expected %A, got %A" name expected actual

let optionCases: (string | null) option list =
    [ None
      Some "present"
      Some null ]

for optionValue in optionCases do
    let roundTripped =
        optionValue
        |> FSharpOptionExtensions.ToOption
        |> FSharpOptionExtensions.ToFSharpOption

    assertEqual "F# option round-trip" optionValue roundTripped

let resultCases: Microsoft.FSharp.Core.Result<string | null, string | null> list =
    [ Ok "success"
      Error "failure"
      Ok null
      Error null ]

for resultValue in resultCases do
    let roundTripped =
        resultValue
        |> FSharpResultExtensions.ToResult
        |> FSharpResultExtensions.ToFSharpResult

    assertEqual "F# Result round-trip" resultValue roundTripped

let nullSuccess: LondoSpark.Func.Result<string | null, string | null> =
    FSharpResultExtensions.ToResult(Ok null)

let nullFailure: LondoSpark.Func.Result<string | null, string | null> =
    FSharpResultExtensions.ToResult(Error null)

if nullSuccess = nullFailure then
    failwith "A null success must remain distinct from a null failure."

printfn "F# interop round-trips passed."
