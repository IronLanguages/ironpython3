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

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

using IronPython.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;

namespace IronPython.Compiler.Ast {
    public class Arg : Node {
        private readonly string _name;
        private readonly Expression _expression;

        public Arg(Expression expression) : this(null, expression) { }

        public Arg(string name, Expression expression) {
            _name = name;
            _expression = expression;
        }

        public string Name {
            get { return _name; }
        }

        public Expression Expression {
            get { return _expression; }
        } 

        public override string ToString() {
            return base.ToString() + ":" + _name;
        }

        internal Argument GetArgumentInfo() {
            if (_name == null) {
                return Argument.Simple;
            } else if (_name == "*") {
                return new Argument(ArgumentType.List);
            } else if (_name == "**") {
                return new Argument(ArgumentType.Dictionary);
            } else {
                return new Argument(_name);
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_expression != null) {
                    _expression.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
