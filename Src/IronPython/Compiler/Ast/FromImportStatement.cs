/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using MSAst = System.Linq.Expressions;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class FromImportStatement : Statement {
        private static readonly string[] _star = new[] { "*" };
        private readonly ModuleName _root;
        private readonly string[] _names;
        private readonly string[] _asNames;
        private readonly bool _fromFuture;

        private PythonVariable[] _variables;

        public static IList<string> Star {
            get { return FromImportStatement._star; }
        }

        public DottedName Root {
            get { return _root; }
        } 

        public bool IsFromFuture {
            get { return _fromFuture; }
        }

        public IList<string> Names {
            get { return _names; }
        }

        public IList<string> AsNames {
            get { return _asNames; }
        }

        internal PythonVariable[] Variables {
            get { return _variables; }
            set { _variables = value; }
        }

        public FromImportStatement(ModuleName root, string[] names, string[] asNames, bool fromFuture) {
            _root = root;
            _names = names;
            _asNames = asNames;
            _fromFuture = fromFuture;
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
                                Parent.GetVariableExpression(_variables[i]),
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
            RelativeModuleName rmn = _root as RelativeModuleName;
            if (rmn != null) {
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
