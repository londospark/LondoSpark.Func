### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
LSF0100 | Usage | Error | An instruction set is declared on a top-level, non-generic static partial class
LSF0101 | Usage | Error | An instruction set is a union
LSF0102 | Usage | Error | Every instruction names one result type
LSF0103 | Usage | Error | An async result type is a top-level partial struct with one type parameter
LSF0104 | Usage | Error | A monad transformer is a partial record struct wrapping Outer<Option<T>> or Outer<Result<T, E>>
LSF0105 | Usage | Error | A monad transformer over an unknown outer monad declares Pure
