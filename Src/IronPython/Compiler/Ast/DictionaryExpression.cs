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

using System;
using System.Collections.Generic;

using Microsoft.Scripting.Interpreter;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class DictionaryExpression : Expression, IInstructionProvider {
        private readonly SliceExpression[] _items;
        private static MSAst.Expression EmptyDictExpression = Ast.Call(AstMethods.MakeEmptyDict);

        public DictionaryExpression(params SliceExpression[] items) {
            _items = items;
        }

        public IList<SliceExpression> Items {
            get { return _items; }
        }

        public override MSAst.Expression Reduce() {
            // create keys & values into array and then call helper function
            // which creates the dictionary
            if (_items.Length != 0) {
                return ReduceConstant() ?? ReduceDictionaryWithItems();
            }

            // empty dictionary
            return EmptyDictExpression;
        }

        private MSAst.Expression ReduceDictionaryWithItems() {
            MSAst.Expression[] parts = new MSAst.Expression[_items.Length * 2];
            Type t = null;
            bool heterogeneous = false;
            for (int index = 0; index < _items.Length; index++) {
                SliceExpression slice = _items[index];
                // Eval order should be:
                //   { 2 : 1, 4 : 3, 6 :5 }
                // This is backwards from parameter list eval, so create temporaries to swap ordering.


                parts[index * 2] = TransformOrConstantNull(slice.SliceStop, typeof(object));
                MSAst.Expression key = parts[index * 2 + 1] = TransformOrConstantNull(slice.SliceStart, typeof(object));

                Type newType;
                if (key.NodeType == MSAst.ExpressionType.Convert) {
                    newType = ((MSAst.UnaryExpression)key).Operand.Type;
                } else {
                    newType = key.Type;
                }

                if (t == null) {
                    t = newType;
                } else if (newType == typeof(object)) {
                    heterogeneous = true;
                } else if (newType != t) {
                    heterogeneous = true;
                }
            }

            return Ast.Call(
                heterogeneous ? AstMethods.MakeDictFromItems : AstMethods.MakeHomogeneousDictFromItems,
                Ast.NewArrayInit(
                    typeof(object),
                    parts
                )
            );
        }

        private MSAst.Expression ReduceConstant() {
            for (int index = 0; index < _items.Length; index++) {
                SliceExpression slice = _items[index];
                if (!slice.SliceStop.IsConstant || !slice.SliceStart.IsConstant) {
                    return null;
                }
            }

            CommonDictionaryStorage storage = new CommonDictionaryStorage();
            for (int index = 0; index < _items.Length; index++) {
                SliceExpression slice = _items[index];

                storage.AddNoLock(slice.SliceStart.GetConstantValue(), slice.SliceStop.GetConstantValue());
            }


            return Ast.Call(AstMethods.MakeConstantDict, Ast.Constant(new ConstantDictionaryStorage(storage), typeof(object)));
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_items != null) {
                    foreach (SliceExpression s in _items) {
                        s.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        #region IInstructionProvider Members

        void IInstructionProvider.AddInstructions(LightCompiler compiler) {
            if (_items.Length == 0) {
                compiler.Instructions.Emit(EmptyDictInstruction.Instance);
                return;
            }

            compiler.Compile(Reduce());
        }

        #endregion

        class EmptyDictInstruction: Instruction {
            public static EmptyDictInstruction Instance = new EmptyDictInstruction();

            public override int Run(InterpretedFrame frame) {
                frame.Push(PythonOps.MakeEmptyDict());
                return +1;
            }

            public override int ProducedStack {
                get { return 1; }
            }
        }
    }
}
