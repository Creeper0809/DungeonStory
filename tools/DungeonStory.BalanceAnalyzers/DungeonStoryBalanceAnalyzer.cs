using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DungeonStory.BalanceAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DungeonStoryBalanceAnalyzer : DiagnosticAnalyzer
    {
        private const string Category = "DungeonStory.Balance";
        private static readonly DiagnosticDescriptor DirectImmutableConstruction = Rule(
            "DSB001", "Immutable balance records must use a capture factory",
            "Construct immutable balance record '{0}' only inside its own type or a [BalanceCaptureFactory] context");
        private static readonly DiagnosticDescriptor DeferredStringNormalization = Rule(
            "DSB002", "Deferred balance string normalization is forbidden",
            "'{0}' is forbidden in balance serialization or presentation; normalize at capture");
        private static readonly DiagnosticDescriptor DeferredOrdering = Rule(
            "DSB003", "Deferred balance ordering is forbidden",
            "'{0}' is forbidden in balance serialization; consume frozen rank order");
        private static readonly DiagnosticDescriptor MutableCanonicalRecord = Rule(
            "DSB004", "Canonical balance records must be immutable",
            "Member '{0}' exposes mutable state from a [BalanceImmutableRecord]");
        private static readonly DiagnosticDescriptor NonCanonicalWriterInput = Rule(
            "DSB005", "Balance writers require frozen or immutable inputs",
            "Writer parameter '{0}' has mutable input type '{1}'");
        private static readonly DiagnosticDescriptor AnalyzerBinaryDrift = Rule(
            "DSB006", "Analyzer source and binary hashes must agree",
            "Analyzer binary drift is validated by the V27 manifest hash gate: {0}");
        private static readonly DiagnosticDescriptor UnboundedSerializerStackalloc = Rule(
            "DSB007", "Serializer stack allocation must be bounded",
            "Balance serialization stackalloc must be a literal constant no greater than 256");
        private static readonly DiagnosticDescriptor AllocatingWriterExpression = Rule(
            "DSB008", "Allocating writer expression is forbidden",
            "'{0}' allocates in the balance serialization layer");

        private static readonly ImmutableHashSet<string> NormalizationMethods =
            ImmutableHashSet.Create(StringComparer.Ordinal,
                "Normalize", "Trim", "TrimStart", "TrimEnd", "Replace",
                "ToLower", "ToLowerInvariant", "ToUpper", "ToUpperInvariant");
        private static readonly ImmutableHashSet<string> OrderingMethods =
            ImmutableHashSet.Create(StringComparer.Ordinal,
                "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending", "Distinct");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                DirectImmutableConstruction,
                DeferredStringNormalization,
                DeferredOrdering,
                MutableCanonicalRecord,
                NonCanonicalWriterInput,
                AnalyzerBinaryDrift,
                UnboundedSerializerStackalloc,
                AllocatingWriterExpression);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeStackalloc, SyntaxKind.StackAllocArrayCreationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeInterpolation, SyntaxKind.InterpolatedStringExpression);
            context.RegisterSymbolAction(AnalyzeWriterMethod, SymbolKind.Method);
        }

        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var syntax = (ObjectCreationExpressionSyntax)context.Node;
            var created = context.SemanticModel.GetTypeInfo(syntax, context.CancellationToken).Type;
            if (!HasAttribute(created, "BalanceImmutableRecordAttribute"))
                return;
            var containing = context.ContainingSymbol?.ContainingType;
            if (SymbolEqualityComparer.Default.Equals(created, containing)
                || HasAttribute(containing, "BalanceCaptureFactoryAttribute"))
                return;
            context.ReportDiagnostic(Diagnostic.Create(
                DirectImmutableConstruction, syntax.GetLocation(), created?.ToDisplayString()));
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var syntax = (InvocationExpressionSyntax)context.Node;
            var method = context.SemanticModel.GetSymbolInfo(syntax, context.CancellationToken).Symbol as IMethodSymbol;
            if (method == null)
                return;
            bool serialization = IsLayer(context.ContainingSymbol, "BalanceSerializationLayerAttribute");
            bool presentation = IsLayer(context.ContainingSymbol, "BalancePresentationLayerAttribute");
            if ((serialization || presentation) && NormalizationMethods.Contains(method.Name)
                && method.ContainingType?.SpecialType == SpecialType.System_String)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DeferredStringNormalization, syntax.GetLocation(), method.Name));
            }
            if (serialization && OrderingMethods.Contains(method.Name)
                && method.ContainingNamespace?.ToDisplayString() == "System.Linq")
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DeferredOrdering, syntax.GetLocation(), method.Name));
            }
            if (!serialization)
                return;
            bool allocating = method.Name == "Substring"
                && method.ContainingType?.SpecialType == SpecialType.System_String
                || method.Name == "ToString"
                || method.Name == "Concat" && method.ContainingType?.SpecialType == SpecialType.System_String
                || method.ContainingType?.ToDisplayString() == "System.Text.StringBuilder";
            if (allocating)
                context.ReportDiagnostic(Diagnostic.Create(
                    AllocatingWriterExpression, syntax.GetLocation(), method.Name));
        }

        private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            if (!HasAttribute(context.ContainingSymbol?.ContainingType, "BalanceImmutableRecordAttribute"))
                return;
            var property = context.SemanticModel.GetDeclaredSymbol(
                (PropertyDeclarationSyntax)context.Node, context.CancellationToken);
            if (property == null || property.DeclaredAccessibility != Accessibility.Public)
                return;
            if (property.SetMethod != null || IsMutableCollection(property.Type))
                context.ReportDiagnostic(Diagnostic.Create(
                    MutableCanonicalRecord, context.Node.GetLocation(), property.Name));
        }

        private static void AnalyzeField(SyntaxNodeAnalysisContext context)
        {
            if (!HasAttribute(context.ContainingSymbol?.ContainingType, "BalanceImmutableRecordAttribute"))
                return;
            foreach (VariableDeclaratorSyntax variable in ((FieldDeclarationSyntax)context.Node).Declaration.Variables)
            {
                var field = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) as IFieldSymbol;
                if (field != null && field.DeclaredAccessibility == Accessibility.Public
                    && (!field.IsReadOnly && !field.IsConst || IsMutableCollection(field.Type)))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MutableCanonicalRecord, variable.GetLocation(), field.Name));
                }
            }
        }

        private static void AnalyzeStackalloc(SyntaxNodeAnalysisContext context)
        {
            if (!IsLayer(context.ContainingSymbol, "BalanceSerializationLayerAttribute"))
                return;
            var stackAllocation = (StackAllocArrayCreationExpressionSyntax)context.Node;
            var arrayType = stackAllocation.Type as ArrayTypeSyntax;
            ExpressionSyntax size = arrayType?.RankSpecifiers.FirstOrDefault()?.Sizes.FirstOrDefault();
            Optional<object> constant = size == null
                ? default(Optional<object>)
                : context.SemanticModel.GetConstantValue(size, context.CancellationToken);
            if (!constant.HasValue || !(constant.Value is int value) || value > 256 || value < 0)
                context.ReportDiagnostic(Diagnostic.Create(
                    UnboundedSerializerStackalloc, stackAllocation.GetLocation()));
        }

        private static void AnalyzeInterpolation(SyntaxNodeAnalysisContext context)
        {
            if (IsLayer(context.ContainingSymbol, "BalanceSerializationLayerAttribute"))
                context.ReportDiagnostic(Diagnostic.Create(
                    AllocatingWriterExpression, context.Node.GetLocation(), "interpolated string"));
        }

        private static void AnalyzeWriterMethod(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol)context.Symbol;
            if (!HasAttribute(method.ContainingType, "BalanceSerializationLayerAttribute")
                || !method.Name.StartsWith("Write", StringComparison.Ordinal))
                return;
            foreach (IParameterSymbol parameter in method.Parameters)
            {
                if (IsAllowedWriterParameter(parameter.Type))
                    continue;
                context.ReportDiagnostic(Diagnostic.Create(
                    NonCanonicalWriterInput,
                    parameter.Locations.FirstOrDefault(),
                    parameter.Name,
                    parameter.Type.ToDisplayString()));
            }
        }

        private static bool IsAllowedWriterParameter(ITypeSymbol type)
        {
            if (type == null || type.TypeKind == TypeKind.Enum || type.IsValueType)
                return true;
            if (type.SpecialType == SpecialType.System_String)
                return true;
            string name = type.ToDisplayString();
            if (name == "System.IO.Stream" || name == "System.IO.StreamWriter"
                || name == "System.ReadOnlySpan<char>")
                return true;
            if (HasAttribute(type, "BalanceSerializationLayerAttribute"))
                return true;
            if (HasAttribute(type, "BalanceImmutableRecordAttribute"))
                return true;
            if (type is INamedTypeSymbol named && named.IsGenericType
                && named.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IReadOnlyList<T>")
                return named.TypeArguments[0].SpecialType == SpecialType.System_String
                    || HasAttribute(named.TypeArguments[0], "BalanceImmutableRecordAttribute");
            return false;
        }

        private static bool IsMutableCollection(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol)
                return true;
            string original = (type as INamedTypeSymbol)?.ConstructedFrom.ToDisplayString();
            return original == "System.Collections.Generic.List<T>"
                || original == "System.Collections.Generic.Dictionary<TKey, TValue>"
                || original == "System.Collections.Generic.HashSet<T>"
                || original == "System.Collections.Generic.IList<T>"
                || original == "System.Collections.Generic.IDictionary<TKey, TValue>"
                || original == "System.Collections.Generic.ICollection<T>";
        }

        private static bool IsLayer(ISymbol symbol, string attributeName) =>
            HasAttribute(symbol as IMethodSymbol, attributeName)
            || HasAttribute(symbol?.ContainingType, attributeName);

        private static bool HasAttribute(ISymbol symbol, string attributeName) =>
            symbol != null && symbol.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.Name == attributeName);

        private static DiagnosticDescriptor Rule(string id, string title, string message) =>
            new DiagnosticDescriptor(
                id, title, message, Category,
                DiagnosticSeverity.Error, isEnabledByDefault: true);
    }
}
