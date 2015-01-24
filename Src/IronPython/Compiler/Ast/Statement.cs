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

using System;

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif


namespace IronPython.Compiler.Ast {
    public abstract class Statement : Node {
        public virtual string Documentation {
            get {
                return null;
            }
        }

        public override Type Type {
            get {
                return typeof(void);
            }
        }
    }

    internal class RewrittenBodyStatement : Statement {
        private readonly MSAst.Expression _body;
        private readonly string _doc;
        private readonly Statement _originalBody;

        public RewrittenBodyStatement(Statement originalBody, MSAst.Expression body) {
            _body = body;
            _doc = originalBody.Documentation;
            _originalBody = originalBody;
            SetLoc(originalBody.GlobalParent, originalBody.IndexSpan);
        }

        public override MSAst.Expression Reduce() {
            return _body;
        }

        public override string Documentation {
            get {
                return _doc;
            }
        }

        public override void Walk(PythonWalker walker) {
            _originalBody.Walk(walker);
        }
    }

}
