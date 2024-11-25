// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace IronPython.DiagnosticAnalyzer {
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class Analyzer : Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer {
        public const string DiagnosticId = "IronPythonAnalyzer";

#pragma warning disable RS2008 // Enable analyzer release tracking
        private static readonly DiagnosticDescriptor Rule1 = new DiagnosticDescriptor("IPY01", title: "Parameter which is marked not nullable does not have the NotNoneAttribute", messageFormat: "Parameter '{0}' does not have the NotNoneAttribute", category: "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Non-nullable reference type parameters should have the NotNoneAttribute.");
        private static readonly DiagnosticDescriptor Rule2 = new DiagnosticDescriptor("IPY02", title: "Parameter which is marked nullable has the NotNoneAttribute", messageFormat: "Parameter '{0}' should not have the NotNoneAttribute", category: "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Nullable reference type parameters should not have the NotNoneAttribute.");
        private static readonly DiagnosticDescriptor Rule3 = new DiagnosticDescriptor("IPY03", title: "BytesLikeAttribute used on a not supported type", messageFormat: "Parameter '{0}' declared bytes-like on unsupported type '{1}'", category: "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "BytesLikeAttribute is only allowed on parameters of type IReadOnlyList<byte>, or IList<byte>.");
        private static readonly DiagnosticDescriptor Rule4 = new DiagnosticDescriptor("IPY04", title: "Call to PythonTypeOps.GetName", messageFormat: "Direct call to PythonTypeOps.GetName", category: "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "To obtain a name of a python type of a given object to display to a user, use PythonOps.GetPythonTypeName.");
        private static readonly DiagnosticDescriptor Rule5 = new DiagnosticDescriptor("IPY05", title: "DLR NotNullAttribute accessed without an alias", messageFormat: "Microsoft.Scripting.Runtime.NotNullAttribute should be accessed though alias 'NotNone'", category: "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "NotNullAttribute is ambiguous between 'System.Diagnostics.CodeAnalysis.NotNullAttribute' and 'Microsoft.Scripting.Runtime.NotNullAttribute'. The latter should be accesses as 'NotNoneAttribute'.");
#pragma warning restore RS2008 // Enable analyzer release tracking

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule1, Rule2, Rule3, Rule4, Rule5); } }

        public override void Initialize(AnalysisContext context) {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context) {
            var methodSymbol = (IMethodSymbol)context.Symbol;
            if (methodSymbol.DeclaredAccessibility != Accessibility.Public) return;

            var pythonTypeAttributeSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.PythonTypeAttribute");
            var pythonModuleAttributeSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.PythonModuleAttribute");

            if (methodSymbol.ContainingType.GetAttributes()
                    .Any(x => x.AttributeClass.Equals(pythonTypeAttributeSymbol, SymbolEqualityComparer.Default)) ||
                methodSymbol.ContainingAssembly.GetAttributes()
                    .Where(x => x.AttributeClass.Equals(pythonModuleAttributeSymbol, SymbolEqualityComparer.Default))
                    .Select(x => (INamedTypeSymbol)x.ConstructorArguments[1].Value)
                    .Any(x => x.Equals(methodSymbol.ContainingType, SymbolEqualityComparer.Default))) {
                var pythonHiddenAttributeSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.PythonHiddenAttribute");
                if (methodSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(pythonHiddenAttributeSymbol, SymbolEqualityComparer.Default))) return;

                var codeContextSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.CodeContext");
                var siteLocalStorageSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.SiteLocalStorage");
                var notNoneAttributeSymbol = context.Compilation.GetTypeByMetadataName("Microsoft.Scripting.Runtime.NotNullAttribute");
                var bytesLikeAttributeSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.BytesLikeAttribute");

                var byteType = context.Compilation.GetTypeByMetadataName("System.Byte");
                var ireadOnlyListType = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyList`1");
                var ilistType = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.IList`1");
                var ireadOnlyListOfByteType = ireadOnlyListType.Construct(byteType);
                var ilistOfByteType = ilistType.Construct(byteType);

                foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters) {
                    if (parameterSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(bytesLikeAttributeSymbol, SymbolEqualityComparer.Default))
                            && !parameterSymbol.Type.Equals(ireadOnlyListOfByteType, SymbolEqualityComparer.Default)
                            && !parameterSymbol.Type.Equals(ilistOfByteType, SymbolEqualityComparer.Default)) {
                        var diagnostic = Diagnostic.Create(Rule3, parameterSymbol.Locations[0], parameterSymbol.Name, parameterSymbol.Type.MetadataName);
                        context.ReportDiagnostic(diagnostic);
                        continue;
                    }
                    if (parameterSymbol.GetAttributes().FirstOrDefault(x => x.AttributeClass.Equals(notNoneAttributeSymbol, SymbolEqualityComparer.Default)) is AttributeData attr) {
                        SyntaxNode node = attr.ApplicationSyntaxReference.GetSyntax(); // Async?
                        if (node.GetLastToken().Text != "NotNone") {
                            var diagnostic = Diagnostic.Create(Rule5, node.GetLocation());
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                    if (parameterSymbol.Type.IsValueType) continue;
                    if (parameterSymbol.Type.Equals(codeContextSymbol, SymbolEqualityComparer.Default)) continue;
                    if (SymbolEqualityComparer.Default.Equals(parameterSymbol.Type.BaseType, siteLocalStorageSymbol)) continue;
                    if (parameterSymbol.NullableAnnotation == NullableAnnotation.NotAnnotated) {
                        if (!parameterSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(notNoneAttributeSymbol, SymbolEqualityComparer.Default))
                            && !parameterSymbol.GetAttributes().Any(x => IsAllowNull(x.AttributeClass))) {
                            var diagnostic = Diagnostic.Create(Rule1, parameterSymbol.Locations[0], parameterSymbol.Name);
                            context.ReportDiagnostic(diagnostic);
                        }
                    } else if (parameterSymbol.NullableAnnotation == NullableAnnotation.Annotated) {
                        if (parameterSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(notNoneAttributeSymbol, SymbolEqualityComparer.Default))
                            && !parameterSymbol.GetAttributes().Any(x => IsDisallowNull(x.AttributeClass))) {
                            var diagnostic = Diagnostic.Create(Rule2, parameterSymbol.Locations[0], parameterSymbol.Name);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }

                bool IsAllowNull(INamedTypeSymbol symbol) {
                    return symbol.ToString() == "System.Diagnostics.CodeAnalysis.AllowNullAttribute";
                }
                bool IsDisallowNull(INamedTypeSymbol symbol) {
                    return symbol.ToString() == "System.Diagnostics.CodeAnalysis.DisallowNullAttribute";
                }
            }
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context) {
            var invocationOperation = (IInvocationOperation)context.Operation;

            if (invocationOperation.Instance is null) { // static invocation
                var pythonTypeOps = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.Operations.PythonTypeOps");
                IMethodSymbol methodSymbol = invocationOperation.TargetMethod;
                if (methodSymbol.Name == "GetName" && methodSymbol.ContainingType.Equals(pythonTypeOps, SymbolEqualityComparer.Default)) {
                    var diagnostic = Diagnostic.Create(Rule4, invocationOperation.Syntax.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }

        }
    }
}
