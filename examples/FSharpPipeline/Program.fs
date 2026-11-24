module Examples.FSharpPipeline.Program

open LondoSpark.Func.FSharp

// Values cross the language boundary without losing their case: an F# option or
// Result becomes the core type, composes, and converts back.
//
//   dotnet run --project examples/FSharpPipeline

let parseNumber (text: string) : Result<int, string> =
    match System.Int32.TryParse text with
    | true, value -> Ok value
    | _ -> Error(sprintf "'%s' is not a number" text)

let describe (result: Result<int, string>) : string =
    match result with
    | Ok value -> sprintf "success %d" value
    | Error error -> sprintf "failure %s" error

let roundTrip (text: string) : string =
    text
    |> parseNumber
    |> FSharpResultExtensions.ToResult
    |> FSharpResultExtensions.ToFSharpResult
    |> describe

printfn "round-trip: %s" (roundTrip "42")
printfn "round-trip: %s" (roundTrip "not a number")

// A present null is still a present option, so the absent case stays distinguishable.
let presentNull: (string | null) option = Some null
let absent: (string | null) option = None

let describeOption (optionValue: (string | null) option) : string =
    let core = FSharpOptionExtensions.ToOption optionValue
    let back = FSharpOptionExtensions.ToFSharpOption core

    match back with
    | Some value -> sprintf "present %A" value
    | None -> "absent"

printfn "option: %s" (describeOption presentNull)
printfn "option: %s" (describeOption absent)
