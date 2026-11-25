using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LondoSpark.Func.Analyzers;

/// <summary>Recognises the library's unions and their nested case types, shared by the analyzers and code fixes.</summary>
public static class UnionCaseNames
{
    /// <summary>The metadata names of the unions whose cases are nested types.</summary>
    public static readonly IReadOnlyList<string> UnionMetadataNames =
    [
        "LondoSpark.Func.Option`1",
        "LondoSpark.Func.Result`2",
        "LondoSpark.Func.Validation`2",
    ];

    /// <summary>Renders a closed union type for a <c>using static</c> directive: namespace-qualified, without <c>global::</c> or nullable annotations.</summary>
    public static readonly SymbolDisplayFormat ImportFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>Determines whether <paramref name="type"/> is a constructed Option, Result, or Validation.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> for one of the library's unions.</returns>
    public static bool IsUnion(ITypeSymbol? type) =>
        type is INamedTypeSymbol { IsGenericType: true } named
        && named.ContainingNamespace?.ToDisplayString() == "LondoSpark.Func"
        && UnionMetadataNames.Contains(named.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + named.OriginalDefinition.MetadataName);

    /// <summary>Determines whether <paramref name="type"/> names a case nested in one of the library's unions.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> for a nested case such as <c>Result&lt;A, B&gt;.Success</c>.</returns>
    public static bool IsCase(ITypeSymbol? type) =>
        type is INamedTypeSymbol { ContainingType: { } union } && IsUnion(union);

    /// <summary>The metadata name of the prelude of lowercase constructors.</summary>
    public const string PreludeMetadataName = "LondoSpark.Func.Prelude";

    /// <summary>Finds the union whose static import would bring the case <paramref name="symbol"/> into scope.</summary>
    /// <param name="symbol">A nested case type such as <c>Result&lt;A, B&gt;.Success</c>.</param>
    /// <returns>The constructed union, or <see langword="null"/> when the symbol is not a case.</returns>
    public static INamedTypeSymbol? ImportingUnion(ISymbol? symbol) =>
        symbol is INamedTypeSymbol caseType && IsCase(caseType) ? caseType.ContainingType : null;

    /// <summary>Determines whether <paramref name="union"/> declares a nested case named <paramref name="name"/>.</summary>
    /// <param name="union">The constructed union.</param>
    /// <param name="name">The name written without its union.</param>
    /// <returns><see langword="true"/> when a static import of the union would bring the name into scope.</returns>
    public static bool DeclaresImportable(INamedTypeSymbol union, string name) =>
        !union.GetTypeMembers(name).IsEmpty;

    /// <summary>Determines whether <paramref name="name"/> is one of the prelude's lowercase constructors.</summary>
    /// <param name="compilation">The compilation that references the library.</param>
    /// <param name="name">The name written without an import.</param>
    /// <returns><see langword="true"/> for <c>some</c>, <c>none</c>, <c>success</c>, or <c>failure</c>.</returns>
    public static bool IsPreludeMember(Compilation compilation, string name) =>
        compilation.GetTypeByMetadataName(PreludeMetadataName) is { } prelude
        && prelude.GetMembers(name).Any(static member =>
            member.IsStatic && member.DeclaredAccessibility == Accessibility.Public && member is IMethodSymbol or IFieldSymbol);

    /// <summary>Determines whether a union can be named by a <c>using static</c> directive, which cannot refer to type parameters.</summary>
    /// <param name="union">The constructed union.</param>
    /// <returns><see langword="true"/> when every type argument is a concrete type.</returns>
    public static bool CanImport(INamedTypeSymbol union) =>
        union.TypeArguments.All(static argument => !ContainsTypeParameter(argument));

    /// <summary>Finds the nested case types of a union that a static import would bring into scope.</summary>
    /// <param name="union">The constructed union.</param>
    /// <returns>The case names, for example <c>Success</c> and <c>Failure</c>.</returns>
    public static IEnumerable<string> CaseNames(INamedTypeSymbol union) =>
        union.GetTypeMembers().Select(static member => member.Name);

    /// <summary>Recognises a case spelled through its union, as a qualified name or, in expression position such as a constant pattern, a member access.</summary>
    /// <param name="node">The node to test.</param>
    /// <param name="union">Receives the syntax naming the union, such as <c>Result&lt;A, B&gt;</c> or an alias.</param>
    /// <param name="caseName">Receives the syntax naming the case.</param>
    /// <returns><see langword="true"/> for the outermost name of such a spelling outside a using directive.</returns>
    public static bool TryGetQualifiedCase(
        SyntaxNode node,
        out SyntaxNode union,
        out SimpleNameSyntax caseName)
    {
        // netstandard2.0 has no public NotNullWhen, so the outs are only meaningful when this returns true.
        var (left, right) = node switch
        {
            QualifiedNameSyntax { Parent: not QualifiedNameSyntax } qualified => (qualified.Left, qualified.Right),
            MemberAccessExpressionSyntax { Parent: not MemberAccessExpressionSyntax } access
                when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) => (access.Expression, access.Name),
            _ => ((SyntaxNode?)null, (SimpleNameSyntax?)null)
        };

        union = left!;
        caseName = right!;
        return left is not null && right is not null && node.FirstAncestorOrSelf<UsingDirectiveSyntax>() is null;
    }

    private static bool ContainsTypeParameter(ITypeSymbol type) =>
        type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
            IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
            INamedTypeSymbol named => named.TypeArguments.Any(ContainsTypeParameter)
                || (named.ContainingType is { } containing && ContainsTypeParameter(containing)),
            _ => false
        };
}
