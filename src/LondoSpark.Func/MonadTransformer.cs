using System;

namespace LondoSpark.Func;

/// <summary>Marks a record struct that wraps one monad inside another, so the generator makes the pair a monad transformer: a monad of its own that binds both layers.</summary>
/// <remarks>
/// <para>
/// A program whose steps can fail is a <c>Program&lt;TOp, Result&lt;T, E&gt;&gt;</c>: two monads, one inside the other.
/// Query syntax over it binds only the outer one, so every step has to unwrap the result and decide by hand whether to
/// carry on. A result or option inside any other monad is a monad transformer (<c>ExceptT</c> and <c>MaybeT</c> in
/// Haskell, <c>EitherT</c> and <c>OptionT</c> in Scala's Cats); declare one and the generator writes it:
/// <code>
/// [MonadTransformer]
/// public readonly partial record struct Script&lt;T&gt;(Program&lt;RunbookOp, Result&lt;T, string&gt;&gt; Value);
///
/// Script&lt;string&gt; Release(string tag) =&gt;
///     from tests in run("dotnet test")                         // an outer step that cannot fail
///     from passed in Require(tests.Succeeded, "tests failed")  // a Script&lt;Unit&gt; that may fail
///     from pushed in run($"git push origin {tag}")             // skipped after a failure
///     select tag;
/// </code>
/// </para>
/// <para>
/// The wrapped type is <c>Outer&lt;Inner&lt;T&gt;&gt;</c>. The inner monad is <see cref="Option{TSome}"/> or
/// <see cref="Result{TSuccess, TFailure}"/>, which compose under any outer monad; the outer monad is any type with query
/// operators (<c>Select</c> and <c>SelectMany</c> with a projection) in scope where the struct is declared. The
/// generator knows how to wrap a plain value in <see cref="Program{TOp, T}"/>, <see cref="Option{TSome}"/>, and
/// <see cref="Result{TSuccess, TFailure}"/>; for any other outer monad, declare
/// <c>private static Outer&lt;X&gt; Pure&lt;X&gt;(X value)</c> on the struct.
/// </para>
/// <para>
/// The generated type has <c>Return</c>, <c>Lift</c> for an outer step that cannot fail, <c>Failure</c> (or <c>None</c>),
/// <c>Select</c>, <c>Bind</c>, and <c>SelectMany</c> over steps of its own type, of the outer monad, and of the inner
/// one, and converts to and from the wrapped type.
/// </para>
/// <para>
/// Over <see cref="Program{TOp, T}"/>, the operations are <see cref="TransformedProgram"/>'s, built into the program's
/// own steps: lifting an instruction allocates nothing, and each bind allocates only the program step that holds its
/// delegates. Over any other outer monad, each operation allocates the closures it passes to that monad.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class MonadTransformerAttribute : Attribute;
