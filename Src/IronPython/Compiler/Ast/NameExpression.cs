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

using System;
using System.Diagnostics;
using System.Dynamic;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class NameExpression : Expression {
        private readonly string _name;
        private PythonReference _reference;
        private bool _assigned;                  // definitely assigned

        public NameExpression(string name) {
            _name = name;
        }

        public string Name {
            get { return _name; }
        }

        internal PythonReference Reference {
            get { return _reference; }
            set { _reference = value; }
        }

        internal bool Assigned {
            get { return _assigned; }
            set { _assigned = value; }
        }

        public override string ToString() {
            return base.ToString() + ":" + _name;
        }

        public override MSAst.Expression Reduce() {
            MSAst.Expression read;

            if (_reference.PythonVariable == null) {
                read = Ast.Call(
                    AstMethods.LookupName,
                    Parent.LocalContext,
                    Ast.Constant(_name)                    
                );
            } else {
                read = Parent.GetVariableExpression(_reference.PythonVariable);
            }

            if (!_assigned && !(read is IPythonGlobalExpression)) {
                read = Ast.Call(
                    AstMethods.CheckUninitialized,
                    read,
                    Ast.Constant(_name)
                );
            }

            return read;
        }

        internal override MSAst.Expression TransformSet(SourceSpan span, MSAst.Expression right, PythonOperationKind op) {
            MSAst.Expression assignment;

            if (op != PythonOperationKind.None) {
                right = GlobalParent.Operation(
                    typeof(object),
                    op,
                    this,
                    right
                );
            }

            SourceSpan aspan = span.IsValid ? new SourceSpan(Span.Start, span.End) : SourceSpan.None;

            if (_reference.PythonVariable != null) {
                assignment = AssignValue(
                    Parent.GetVariableExpression(_reference.PythonVariable),
                    ConvertIfNeeded(right, typeof(object))
                );
            } else {
                assignment = Ast.Call(
                    null,
                    AstMethods.SetName,
                    Parent.LocalContext, 
                    Ast.Constant(_name),
                    AstUtils.Convert(right, typeof(object))
                );
            }

            return GlobalParent.AddDebugInfoAndVoid(assignment, aspan);
        }

        internal override string CheckAssign() {
            return null;
        }

        internal override string CheckDelete() {
            return null;
        }

        internal override MSAst.Expression TransformDelete() {
            if (_reference.PythonVariable != null) {
                MSAst.Expression variable = Parent.GetVariableExpression(_reference.PythonVariable);
                // keep the variable alive until we hit the del statement to
                // better match CPython's lifetimes
                MSAst.Expression del = Ast.Block(
                    Ast.Call(
                        AstMethods.KeepAlive,
                        variable
                    ),
                    Delete(variable)
                );

                if (!_assigned) {
                    del = Ast.Block(
                        this,
                        del,
                        AstUtils.Empty()
                    );
                }
                return del;
            } else {
                return Ast.Call(
                    AstMethods.RemoveName,
                    Parent.LocalContext,
                    Ast.Constant(_name)
                );
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow {
            get {
                return !Assigned;
            }
        }
    }
}
