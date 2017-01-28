using System;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Binding;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {

    public class StarredExpression : Expression {
        public StarredExpression(Expression value) {
            Value = value;
        }

        public Expression Value { get; }

        public override MSAst.Expression Reduce() {
            return Value;
        }

        internal override MSAst.Expression TransformSet(SourceSpan span, MSAst.Expression right, PythonOperationKind op) {
            return Value.TransformSet(span, right, op);
        }

        internal override string CheckAssign() {
            return Value.CheckAssign();
        }

        internal override string CheckDelete() {
            return Value.CheckDelete();
        }

        internal override MSAst.Expression TransformDelete() {
            return Value.TransformDelete();
        }

        public override Type Type {
            get {
                return Value.Type;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (Value != null) {
                    Value.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow {
            get {
                return Value.CanThrow;
            }
        }
    }
}
