using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IronPythonAnalyzer {
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IronPythonAnalyzerAnalyzer : DiagnosticAnalyzer {
        public const string DiagnosticId = "IronPythonAnalyzer";

        private static readonly DiagnosticDescriptor Rule1 = new DiagnosticDescriptor("IPY01", title: "Parameter which is marked not nullable does not have the NotNullAttribute", messageFormat: "Parameter '{0}' does not have the NotNullAttribute", category: "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Non-nullable reference type parameters should have the NotNullAttribute.");
        private static readonly DiagnosticDescriptor Rule2 = new DiagnosticDescriptor("IPY02", title: "Parameter which is marked nullable has the NotNullAttribute", messageFormat: "Parameter '{0}' should not have the NotNullAttribute", category: "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Nullable reference type parameters should not have the NotNullAttribute.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule1, Rule2); } }

        public override void Initialize(AnalysisContext context) {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context) {
            var methodSymbol = (IMethodSymbol)context.Symbol;
            if (methodSymbol.DeclaredAccessibility != Accessibility.Public) return;

            var pythonTypeAttributeSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.PythonTypeAttribute");
            var pythonModuleAttributeSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.PythonModuleAttribute");

#pragma warning disable RS1024 // Compare symbols correctly

            if (methodSymbol.ContainingType.GetAttributes()
                    .Any(x => x.AttributeClass.Equals(pythonTypeAttributeSymbol)) ||
                methodSymbol.ContainingAssembly.GetAttributes()
                    .Where(x => x.AttributeClass.Equals(pythonModuleAttributeSymbol))
                    .Select(x => (INamedTypeSymbol)x.ConstructorArguments[1].Value)
                    .Any(x => x.Equals(methodSymbol.ContainingType))) {
                var pythonHiddenAttributeSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.PythonHiddenAttribute");
                if (methodSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(pythonHiddenAttributeSymbol))) return;

                var codeContextSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.CodeContext");
                var notNullAttributeSymbol = context.Compilation.GetTypeByMetadataName("Microsoft.Scripting.Runtime.NotNullAttribute");
                var disallowNullAttributeSymbol = context.Compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.DisallowNullAttribute");
                var allowNullAttributeSymbol = context.Compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.AllowNullAttribute");
                foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters) {
                    if (parameterSymbol.Type.IsValueType) continue;
                    if (parameterSymbol.Type.Equals(codeContextSymbol)) continue;
                    if (parameterSymbol.NullableAnnotation == NullableAnnotation.NotAnnotated) {
                        if (!parameterSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(notNullAttributeSymbol))
                            && !parameterSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(allowNullAttributeSymbol))) {
                            var diagnostic = Diagnostic.Create(Rule1, parameterSymbol.Locations[0], parameterSymbol.Name);
                            context.ReportDiagnostic(diagnostic);
                        }
                    } else if (parameterSymbol.NullableAnnotation == NullableAnnotation.Annotated) {
                        if (parameterSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(notNullAttributeSymbol))
                            && !parameterSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(disallowNullAttributeSymbol))) {
                            var diagnostic = Diagnostic.Create(Rule2, parameterSymbol.Locations[0], parameterSymbol.Name);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }

#pragma warning restore RS1024 // Compare symbols correctly

        }
    }
}
