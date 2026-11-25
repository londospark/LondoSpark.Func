using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LondoSpark.Func.Generators;

/// <summary>Writes a typed instruction set (constructors, handler interfaces, and interpreters) for a class marked <c>[Instructions&lt;TOp&gt;]</c>.</summary>
/// <remarks>
/// <code>
/// public sealed record Ask(string Prompt) : IInstruction&lt;string&gt;;
/// public sealed record Tell(string Message) : IInstruction&lt;Unit&gt;;
/// public union ConsoleOp(Ask, Tell);
///
/// [Instructions&lt;ConsoleOp&gt;]
/// public static partial class Console;
/// </code>
/// becomes <c>Console.ask(prompt)</c>, <c>Console.tell(message)</c>, <c>Console.pure(value)</c>,
/// <c>Console.IHandler</c>, <c>Console.IAsyncHandler</c>, and the <c>Run</c> and <c>RunAsync</c> extensions.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class InstructionsGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "LondoSpark.Func.InstructionsAttribute`1";
    private const string InstructionMetadataName = "LondoSpark.Func.IInstruction`1";

    internal static readonly DiagnosticDescriptor ClassShape = new(
        "LSF0100",
        "Instruction set class has the wrong shape",
        "'{0}' must be a top-level, non-generic static partial class to hold an instruction set",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generator adds extension methods to the class, which C# allows only in top-level, non-generic static classes.");

    internal static readonly DiagnosticDescriptor NotAUnion = new(
        "LSF0101",
        "Instruction set is not a union",
        "'{0}' is not a union; declare the instruction set as 'union {0}(...)' with one case per instruction",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NoResultType = new(
        "LSF0102",
        "Instruction does not name one result type",
        "Instruction '{0}' must implement IInstruction<TResult> exactly once to say what an interpreter answers it with",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat TypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sets = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (attributeContext, _) => Describe(attributeContext));

        context.RegisterSourceOutput(sets, static (output, set) =>
        {
            foreach (var problem in set.Problems)
            {
                output.ReportDiagnostic(problem.ToDiagnostic());
            }

            if (set.Problems.IsEmpty)
            {
                output.AddSource($"{set.HintName}.Instructions.g.cs", Render(set));
            }
        });
    }

    private static InstructionSet Describe(GeneratorAttributeSyntaxContext context)
    {
        var holder = (INamedTypeSymbol)context.TargetSymbol;
        var declaration = (ClassDeclarationSyntax)context.TargetNode;
        var location = LocationInfo.From(declaration.Identifier.GetLocation());
        var problems = new List<Problem>();

        var shapeIsValid = holder.IsStatic
            && holder.ContainingType is null
            && holder.TypeParameters.IsEmpty
            && declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        if (!shapeIsValid)
        {
            problems.Add(new Problem(ClassShape, location, holder.Name));
        }

        var attribute = context.Attributes.First();
        var op = attribute.AttributeClass!.TypeArguments[0] as INamedTypeSymbol;
        var cases = new List<InstructionCase>();

        var caseTypes = op is null
            ? []
            : op.InstanceConstructors
                .Where(static constructor => constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.Length == 1)
                .Select(static constructor => constructor.Parameters[0].Type)
                .OfType<INamedTypeSymbol>()
                .Where(caseType => !SymbolEqualityComparer.Default.Equals(caseType, op))
                .ToList();

        if (op is null || !op.IsValueType || caseTypes.Count == 0)
        {
            problems.Add(new Problem(NotAUnion, location, op?.Name ?? "?"));
        }

        foreach (var caseType in caseTypes)
        {
            var results = caseType.AllInterfaces
                .Where(static candidate => MetadataName(candidate.OriginalDefinition) == InstructionMetadataName)
                .ToList();
            if (results.Count != 1)
            {
                problems.Add(new Problem(NoResultType, location, caseType.Name));
                continue;
            }

            var constructors = caseType.InstanceConstructors
                .Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public
                    && !(constructor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, caseType)))
                .Select(static constructor => new Constructor(
                    string.Join(", ", constructor.Parameters.Select(Parameter)),
                    string.Join(", ", constructor.Parameters.Select(static parameter => Lower(parameter.Name)))))
                .Distinct()
                .ToImmutableArray();

            cases.Add(new InstructionCase(
                caseType.ToDisplayString(TypeFormat),
                caseType.Name,
                results[0].TypeArguments[0].ToDisplayString(TypeFormat),
                new EquatableArray<Constructor>(constructors)));
        }

        return new InstructionSet(
            holder.ContainingNamespace.IsGlobalNamespace ? null : holder.ContainingNamespace.ToDisplayString(),
            holder.Name,
            holder.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty).Replace('<', '_').Replace('>', '_'),
            op?.ToDisplayString(TypeFormat) ?? string.Empty,
            new EquatableArray<InstructionCase>([.. cases]),
            new EquatableArray<Problem>([.. problems]));
    }

    private static string MetadataName(INamedTypeSymbol type) =>
        type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() + "." + type.MetadataName : type.MetadataName;

    private static string Parameter(IParameterSymbol parameter)
    {
        var text = parameter.Type.ToDisplayString(TypeFormat) + " " + Lower(parameter.Name);
        if (!parameter.HasExplicitDefaultValue)
        {
            return text;
        }

        var value = parameter.ExplicitDefaultValue;
        return text + " = " + (value is null ? "default" : SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false));
    }

    private static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    private static string Lower(string name) =>
        Identifier(char.ToLowerInvariant(name[0]) + name.Substring(1));

    private static string Render(InstructionSet set)
    {
        var op = set.Op;
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        if (set.Namespace is { } ns)
        {
            builder.Append("namespace ").Append(ns).AppendLine(";");
            builder.AppendLine();
        }

        // An instruction named Run or RunAsync gets a lowercase constructor beside the interpreter of the same name.
        builder.AppendLine("[global::System.Diagnostics.CodeAnalysis.SuppressMessage(\"Naming\", \"CA1708:Identifiers should differ by more than case\", Justification = \"Lowercase instruction constructors sit beside Run and RunAsync.\")]");
        builder.Append("static partial class ").AppendLine(set.ClassName);
        builder.AppendLine("{");

        var lowerNames = new HashSet<string>(set.Cases.Select(static instruction => Lower(instruction.Name)));
        if (!lowerNames.Contains("pure"))
        {
            builder.AppendLine($"    /// <summary>A program that finishes with <paramref name=\"value\"/> without running an instruction.</summary>");
            builder.AppendLine($"    public static global::LondoSpark.Func.Program<{op}, T> pure<T>(T value) =>");
            builder.AppendLine($"        global::LondoSpark.Func.Program.Return<{op}, T>(value);");
            builder.AppendLine();
        }

        foreach (var instruction in set.Cases)
        {
            var program = $"global::LondoSpark.Func.Program<{op}, {instruction.Result}>";
            var name = Lower(instruction.Name);
            builder.AppendLine($"    /// <summary>A program that runs one <see cref=\"{Cref(instruction.Type)}\"/> and finishes with its result.</summary>");
            builder.AppendLine($"    public static {program} {name}({instruction.Type} instruction) =>");
            builder.AppendLine($"        global::LondoSpark.Func.Program.Lift<{op}, {instruction.Result}>(instruction);");
            builder.AppendLine();
            foreach (var constructor in instruction.Constructors)
            {
                builder.AppendLine($"    /// <summary>A program that runs one <see cref=\"{Cref(instruction.Type)}\"/> and finishes with its result.</summary>");
                builder.AppendLine($"    public static {program} {name}({constructor.Parameters}) =>");
                builder.AppendLine($"        global::LondoSpark.Func.Program.Lift<{op}, {instruction.Result}>(new {instruction.Type}({constructor.Arguments}));");
                builder.AppendLine();
            }
        }

        builder.AppendLine($"    /// <summary>Answers every instruction of <see cref=\"{Cref(op)}\"/>; the compiler checks each one is handled.</summary>");
        builder.AppendLine("    public interface IHandler");
        builder.AppendLine("    {");
        foreach (var instruction in set.Cases)
        {
            builder.AppendLine($"        /// <summary>Answers <see cref=\"{Cref(instruction.Type)}\"/>.</summary>");
            builder.AppendLine($"        {instruction.Result} Handle({instruction.Type} instruction);");
        }

        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine($"    /// <summary>Answers every instruction of <see cref=\"{Cref(op)}\"/> asynchronously; the compiler checks each one is handled.</summary>");
        builder.AppendLine("    public interface IAsyncHandler");
        builder.AppendLine("    {");
        foreach (var instruction in set.Cases)
        {
            builder.AppendLine($"        /// <summary>Answers <see cref=\"{Cref(instruction.Type)}\"/>.</summary>");
            builder.AppendLine($"        global::System.Threading.Tasks.ValueTask<{instruction.Result}> HandleAsync({instruction.Type} instruction, global::System.Threading.CancellationToken cancellationToken);");
        }

        builder.AppendLine("    }");
        builder.AppendLine();

        var programType = $"global::LondoSpark.Func.Program<{op}, T>";
        builder.AppendLine("    /// <summary>Runs the program, answering each instruction with <paramref name=\"handler\"/>.</summary>");
        builder.AppendLine($"    public static T Run<T>(this {programType} program, IHandler handler)");
        builder.AppendLine("    {");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(handler);");
        builder.AppendLine("        while (true)");
        builder.AppendLine("        {");
        builder.AppendLine($"            if (program.TryGetValue(out {programType}.Done done))");
        builder.AppendLine("            {");
        builder.AppendLine("                return done.Value;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine($"            if (!program.TryGetValue(out {programType}.Pending pending))");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.InvalidOperationException(\"A default program contains no case.\");");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            program = pending.Instruction switch");
        builder.AppendLine("            {");
        foreach (var instruction in set.Cases)
        {
            builder.AppendLine($"                {instruction.Type} instruction => pending.Next.Resume<{instruction.Result}>(handler.Handle(instruction)),");
        }

        builder.AppendLine("            };");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>Runs the program, answering each instruction with <paramref name=\"handler\"/>.</summary>");
        builder.AppendLine($"    public static async global::System.Threading.Tasks.ValueTask<T> RunAsync<T>(this {programType} program, IAsyncHandler handler, global::System.Threading.CancellationToken cancellationToken = default)");
        builder.AppendLine("    {");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(handler);");
        builder.AppendLine("        while (true)");
        builder.AppendLine("        {");
        builder.AppendLine($"            if (program.TryGetValue(out {programType}.Done done))");
        builder.AppendLine("            {");
        builder.AppendLine("                return done.Value;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine($"            if (!program.TryGetValue(out {programType}.Pending pending))");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.InvalidOperationException(\"A default program contains no case.\");");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            cancellationToken.ThrowIfCancellationRequested();");
        builder.AppendLine("            program = pending.Instruction switch");
        builder.AppendLine("            {");
        foreach (var instruction in set.Cases)
        {
            builder.AppendLine($"                {instruction.Type} instruction => pending.Next.Resume<{instruction.Result}>(await handler.HandleAsync(instruction, cancellationToken).ConfigureAwait(false)),");
        }

        builder.AppendLine("            };");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string Cref(string type) =>
        type.Replace("global::", string.Empty).Replace('<', '{').Replace('>', '}').TrimEnd('?');

    private sealed record InstructionSet(
        string? Namespace,
        string ClassName,
        string HintName,
        string Op,
        EquatableArray<InstructionCase> Cases,
        EquatableArray<Problem> Problems);

    private sealed record InstructionCase(string Type, string Name, string Result, EquatableArray<Constructor> Constructors);

    private sealed record Constructor(string Parameters, string Arguments);

    private sealed record Problem(DiagnosticDescriptor Descriptor, LocationInfo Location, string Argument)
    {
        public Diagnostic ToDiagnostic() =>
            Diagnostic.Create(Descriptor, Location.ToLocation(), Argument);
    }
}
