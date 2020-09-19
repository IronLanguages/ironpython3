// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
        private static readonly DiagnosticDescriptor Rule3 = new DiagnosticDescriptor("IPY03", title: "BytesLikeAttribute used on a not supported type", messageFormat: "Parameter '{0}' declared bytes-like on unsupported type '{1}'", category: "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "BytesLikeAttribute is only allowed on parameters of type ReadOnlyMemory<byte>, IReadOnlyList<byte>, or IList<byte>.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule1, Rule2, Rule3); } }

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
                var siteLocalStorageSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.SiteLocalStorage");
                var notNullAttributeSymbol = context.Compilation.GetTypeByMetadataName("Microsoft.Scripting.Runtime.NotNullAttribute");
                var bytesLikeAttributeSymbol = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.BytesLikeAttribute");

                var byteType = context.Compilation.GetTypeByMetadataName("System.Byte");
                var ireadOnlyListType = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyList`1");
                var ilistType = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.IList`1");
                var ireadOnlyListOfByteType = ireadOnlyListType.Construct(byteType);
                var ilistOfByteType = ilistType.Construct(byteType);
                var ibufferProtocolType = context.Compilation.GetTypeByMetadataName("IronPython.Runtime.IBufferProtocol");

                foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters) {
                    if (parameterSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(bytesLikeAttributeSymbol))
                            && !parameterSymbol.Type.Equals(ibufferProtocolType)
                            && !parameterSymbol.Type.Equals(ireadOnlyListOfByteType)
                            && !parameterSymbol.Type.Equals(ilistOfByteType)) {
                        var diagnostic = Diagnostic.Create(Rule3, parameterSymbol.Locations[0], parameterSymbol.Name, parameterSymbol.Type.MetadataName);
                        context.ReportDiagnostic(diagnostic);
                        continue;
                    }
                    if (parameterSymbol.Type.IsValueType) continue;
                    if (parameterSymbol.Type.Equals(codeContextSymbol)) continue;
                    if (SymbolEqualityComparer.Default.Equals(parameterSymbol.Type.BaseType, siteLocalStorageSymbol)) continue;
                    if (parameterSymbol.NullableAnnotation == NullableAnnotation.NotAnnotated) {
                        if (!parameterSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(notNullAttributeSymbol))
                            && !parameterSymbol.GetAttributes().Any(x => IsAllowNull(x.AttributeClass))) {
                            var diagnostic = Diagnostic.Create(Rule1, parameterSymbol.Locations[0], parameterSymbol.Name);
                            context.ReportDiagnostic(diagnostic);
                        }
                    } else if (parameterSymbol.NullableAnnotation == NullableAnnotation.Annotated) {
                        if (parameterSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(notNullAttributeSymbol))
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

#pragma warning restore RS1024 // Compare symbols correctly

        }
    }
}
