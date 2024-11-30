// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

using MSAst = System.Linq.Expressions;


namespace IronPython.Compiler.Ast {
    public abstract class Statement : Node {
        public virtual string? Documentation => null;

        public override Type Type => typeof(void);
    }

    internal class RewrittenBodyStatement : Statement {
        private readonly MSAst.Expression _body;
        private readonly string? _doc;
        private readonly Statement _originalBody;

        public RewrittenBodyStatement(Statement originalBody, MSAst.Expression body) {
            _body = body;
            _doc = originalBody.Documentation;
            _originalBody = originalBody;
            SetLoc(originalBody.GlobalParent, originalBody.IndexSpan);
        }

        public override MSAst.Expression Reduce() => _body;

        public override string? Documentation => _doc;

        public override void Walk(PythonWalker walker) {
            _originalBody.Walk(walker);
        }
    }

}
