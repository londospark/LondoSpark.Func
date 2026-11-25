using System;

namespace LondoSpark.Func;

/// <summary>Marks an instruction case with the type an interpreter answers it with.</summary>
/// <typeparam name="TResult">The instruction's result type; use <see cref="Unit"/> for instructions run for their effect.</typeparam>
/// <remarks>
/// <code>
/// public sealed record Ask(string Prompt) : IInstruction&lt;string&gt;;
/// public sealed record Tell(string Message) : IInstruction&lt;Unit&gt;;
/// public union ConsoleOp(Ask, Tell);
/// </code>
/// </remarks>
#pragma warning disable CA1040 // Avoid empty interfaces - the type argument is the information.
public interface IInstruction<TResult>;
#pragma warning restore CA1040

/// <summary>Asks the generator to write a typed instruction set for the union <typeparamref name="TOp"/> into this static partial class.</summary>
/// <typeparam name="TOp">A union whose cases each implement <see cref="IInstruction{TResult}"/>.</typeparam>
/// <remarks>
/// <para>
/// <code>
/// [Instructions&lt;ConsoleOp&gt;]
/// public static partial class Console;
/// </code>
/// generates, for each case, a lowercase constructor per public constructor of the case
/// (<c>Console.ask(string prompt)</c>, returning <c>Program&lt;ConsoleOp, string&gt;</c>), plus:
/// </para>
/// <list type="bullet">
/// <item><description><c>Console.IHandler</c>, one <c>Handle</c> overload per instruction, and <c>program.Run(handler)</c>.</description></item>
/// <item><description><c>Console.IAsyncHandler</c>, one <c>HandleAsync</c> overload per instruction, and <c>program.RunAsync(handler)</c>.</description></item>
/// </list>
/// <para>
/// Handlers are typed per instruction, so the compiler checks every instruction is answered with the right type, and
/// answers do not box. The class must be static, partial, non-generic, and not nested, because it holds extension methods.
/// The generator is bundled into the <c>LondoSpark.Func</c> package beside its analyzers.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
#pragma warning disable CS9113, IDE0060 // The type argument is read by the generator.
public sealed class InstructionsAttribute<TOp> : Attribute;
#pragma warning restore CS9113, IDE0060
