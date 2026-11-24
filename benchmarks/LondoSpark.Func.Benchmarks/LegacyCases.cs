namespace LondoSpark.Func.Benchmarks;

// The boxed-union baselines predate the nested case types, so they keep their own top-level
// generic wrappers. Generic arity keeps these apart from the library's nested Some, Success,
// and Failure, so both can be in scope in one file.

/// <summary>The present case of the legacy boxed option baseline.</summary>
/// <typeparam name="T">The payload type.</typeparam>
/// <param name="Value">The present value.</param>
public readonly record struct Some<T>(T Value);

/// <summary>The successful case of the legacy boxed result baseline.</summary>
/// <typeparam name="T">The payload type.</typeparam>
/// <param name="Value">The successful value.</param>
public readonly record struct Success<T>(T Value);

/// <summary>The failed case of the legacy boxed result baseline.</summary>
/// <typeparam name="T">The payload type.</typeparam>
/// <param name="Value">The failure value.</param>
public readonly record struct Failure<T>(T Value);
