// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ImportStatement : Statement {
        private readonly ModuleName[] _names;
        private readonly string[] _asNames;
        private readonly bool _forceAbsolute;

        private PythonVariable[] _variables;

        public ImportStatement(ModuleName[] names, string[] asNames, bool forceAbsolute) {
            _names = names;
            _asNames = asNames;
            _forceAbsolute = forceAbsolute;
        }

        internal PythonVariable[] Variables {
            get { return _variables; }
            set { _variables = value; }
        }

        public IList<DottedName> Names {
            get { return _names; }
        }

        public IList<string> AsNames {
            get { return _asNames; }
        }

        public override MSAst.Expression Reduce() {
            ReadOnlyCollectionBuilder<MSAst.Expression> statements = new ReadOnlyCollectionBuilder<MSAst.Expression>();

            for (int i = 0; i < _names.Length; i++) {
                statements.Add(
                    // _references[i] = PythonOps.Import(<code context>, _names[i])
                    GlobalParent.AddDebugInfoAndVoid(
                        AssignValue(
                            Parent.GetVariableExpression(_variables[i]),
                            LightExceptions.CheckAndThrow(
                                Expression.Call(
                                    _asNames[i] == null ? AstMethods.ImportTop : AstMethods.ImportBottom,
                                    Parent.LocalContext,                                     // 1st arg - code context
                                    AstUtils.Constant(_names[i].MakeString()),                   // 2nd arg - module name
                                    AstUtils.Constant(_forceAbsolute ? 0 : -1)                   // 3rd arg - absolute or relative imports
                                )
                            )
                        ),
                        _names[i].Span
                    )
                );
            }

            statements.Add(AstUtils.Empty());
            return GlobalParent.AddDebugInfo(Ast.Block(statements.ToReadOnlyCollection()), Span);
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }
    }
}
