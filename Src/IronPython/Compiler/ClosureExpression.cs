// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq.Expressions;
using System.Reflection;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Compiler {
    /// <summary>
    /// Small reducable node which just fetches the value from a ClosureCell
    /// object.  Like w/ global variables the compiler recognizes these on 
    /// sets and turns them into assignments on the python global object.
    /// </summary>
    internal class ClosureExpression : Expression, IPythonVariableExpression {
        private readonly Expression/*!*/ _closureCell;
        private readonly ParameterExpression _parameter;
        private readonly Ast.PythonVariable/*!*/ _variable;
        internal static readonly FieldInfo _cellField = typeof(ClosureCell).GetField("Value");

        public ClosureExpression(Ast.PythonVariable/*!*/ variable, Expression/*!*/ closureCell, ParameterExpression parameter) {
            Assert.NotNull(closureCell);

            _variable = variable;
            _closureCell = closureCell;
            _parameter = parameter;
        }

        #region ClosureExpression Public API

        /// <summary>
        /// Gets the expression which points at the closure cell.
        /// </summary>
        public Expression/*!*/ ClosureCell {
            get {
                return _closureCell;
            }
        }

        /// <summary>
        /// The original expression for the incoming parameter if this is a parameter closure.  Otherwise
        /// the value is null.
        /// </summary>
        public ParameterExpression OriginalParameter {
            get {
                return _parameter;
            }
        }

        /// <summary>
        /// Gets the PythonVariable for which this closure expression was created.
        /// </summary>
        public Ast.PythonVariable/*!*/ PythonVariable {
            get {
                return _variable;
            }
        }

        /// <summary>
        /// Creates the storage for the closure cell.  If this is a closure over a parameter it
        /// captures the initial incoming parameter value.
        /// </summary>
        public Expression/*!*/ Create() {
            if (OriginalParameter != null) {
                return Expression.Assign(_closureCell, Expression.Call(Ast.AstMethods.MakeClosureCellWithValue, OriginalParameter));
            }
            return Expression.Assign(_closureCell, MakeClosureCellExpression.Instance);
        }

        private class MakeClosureCellExpression : Expression, IInstructionProvider {
            private static readonly Expression _call = Expression.Call(Ast.AstMethods.MakeClosureCell);
            public static readonly MakeClosureCellExpression Instance = new MakeClosureCellExpression();

            public override bool CanReduce {
                get {
                    return true;
                }
            }

            public override ExpressionType NodeType {
                get {
                    return ExpressionType.Extension;
                }
            }

            public override Type Type {
                get {
                    return typeof(ClosureCell);
                }
            }

            public override Expression Reduce() {
                return _call;
            }

            #region IInstructionProvider Members

            public void AddInstructions(LightCompiler compiler) {
                compiler.Instructions.Emit(MakeClosureCellInstruction.Instance);
            }

            #endregion

            private class MakeClosureCellInstruction : Instruction {
                public static readonly MakeClosureCellInstruction Instance = new MakeClosureCellInstruction();

                public override int ProducedStack {
                    get {
                        return 1;
                    }
                }

                public override int ConsumedStack {
                    get {
                        return 0;
                    }
                }

                public override int Run(InterpretedFrame frame) {
                    frame.Push(PythonOps.MakeClosureCell());
                    return +1;
                }
            }
        }

        #endregion

        #region Expression overrides

        public sealed override ExpressionType NodeType {
            get { return ExpressionType.Extension; }
        }

        public sealed override Type/*!*/ Type {
            get { return typeof(object); }
        }

        public override bool CanReduce {
            get {
                return true;
            }
        }

        public string Name {
            get {
                return _variable.Name;
            }
        }

        /// <summary>
        /// Reduces the closure cell to a read of the value stored in the cell.
        /// </summary>
        public override Expression/*!*/ Reduce() {
            return Expression.Field(
                _closureCell,
                _cellField
            );
        }

        #endregion

        #region IPythonVariableExpression implementation

        /// <summary>
        /// Assigns a value to the closure cell.
        /// </summary>
        public Expression/*!*/ Assign(Expression/*!*/ value) {
            return Expression.Assign(
                Expression.Field(_closureCell, _cellField),
                value
            );
        }

        /// <summary>
        /// Removes the current value from the closure cell.
        /// </summary>
        public Expression/*!*/ Delete() {
            return Expression.Assign(
                Expression.Field(_closureCell, _cellField),
                Expression.Field(null, typeof(Uninitialized).GetDeclaredField("Instance"))
            );
        }

        #endregion
    }
}
