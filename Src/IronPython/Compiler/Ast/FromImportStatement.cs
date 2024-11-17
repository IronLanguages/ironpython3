// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Runtime;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class FromImportStatement : Statement {
        private static readonly string[] _star = new[] { "*" };
        private readonly ModuleName _root;
        private readonly string[] _names;
        private readonly string[] _asNames;

        public static IList<string> Star => FromImportStatement._star;

        public DottedName Root => _root;

        public bool IsFromFuture { get; }

        public IList<string> Names => _names;

        public IList<string> AsNames => _asNames;

        internal PythonVariable[] Variables { get; set; }

        public FromImportStatement(ModuleName root, string[] names, string[] asNames, bool fromFuture) {
            _root = root;
            _names = names;
            _asNames = asNames;
            IsFromFuture = fromFuture;
        }

        public override MSAst.Expression Reduce() {
            if (_names == _star) {
                // from a[.b] import *
                return GlobalParent.AddDebugInfo(
                    Ast.Call(
                        AstMethods.ImportStar,
                        Parent.LocalContext,
                        AstUtils.Constant(_root.MakeString()),
                        AstUtils.Constant(GetLevel())
                    ),
                    Span
                );
            } else {
                // from a[.b] import x [as xx], [ y [ as yy] ] [ , ... ]

                ReadOnlyCollectionBuilder<MSAst.Expression> statements = new ReadOnlyCollectionBuilder<MSAst.Expression>();
                MSAst.ParameterExpression module = Ast.Variable(typeof(object), "module");

                // Create initializer of the array of names being passed to ImportWithNames
                MSAst.Expression[] names = new MSAst.Expression[_names.Length];
                for (int i = 0; i < names.Length; i++) {
                    names[i] = AstUtils.Constant(_names[i]);
                }

                // module = PythonOps.ImportWithNames(<context>, _root, make_array(_names))
                statements.Add(
                    GlobalParent.AddDebugInfoAndVoid(
                        AssignValue(
                            module,
                            LightExceptions.CheckAndThrow(
                                Expression.Call(
                                    AstMethods.ImportWithNames,
                                    Parent.LocalContext,
                                    AstUtils.Constant(_root.MakeString()),
                                    Ast.NewArrayInit(typeof(string), names),
                                    AstUtils.Constant(GetLevel())
                                )
                            )
                        ),
                        _root.Span
                    )
                );

                // now load all the names being imported and assign the variables
                for (int i = 0; i < names.Length; i++) {
                    statements.Add(
                        GlobalParent.AddDebugInfoAndVoid(
                            AssignValue(
                                Parent.GetVariableExpression(Variables[i]),
                                Ast.Call(
                                    AstMethods.ImportFrom,
                                    Parent.LocalContext,
                                    module,
                                    names[i]
                                )
                            ),
                            Span
                        )
                    );
                }

                statements.Add(AstUtils.Empty());
                return GlobalParent.AddDebugInfo(Ast.Block(new[] { module }, statements.ToArray()), Span);
            }
        }

        private object GetLevel() {
            if (_root is RelativeModuleName rmn) {
                return rmn.DotCount;
            }

            return 0;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }
    }
}
