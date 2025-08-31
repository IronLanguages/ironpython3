// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Interpreter;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    public class DictionaryExpression : Expression, IInstructionProvider {
        private readonly SliceExpression[] _items;
        private readonly bool _hasNullKey;
        private static readonly MSAst.Expression EmptyDictExpression = Expression.Call(AstMethods.MakeEmptyDict);

        public DictionaryExpression(params SliceExpression[] items) {
            // TODO: use two arrays instead of SliceExpression
            foreach (var item in items) {
                if (item.SliceStart is null) _hasNullKey = true;
                if (item.SliceStop is null) throw PythonOps.ValueError("None disallowed in expression list");
            }
            _items = items;
        }

        public IReadOnlyList<SliceExpression> Items => _items;

        public override MSAst.Expression Reduce() {
            // empty dictionary
            if (_items.Length == 0) {
                return EmptyDictExpression;
            }

            if (_hasNullKey) {
                // TODO: unpack constant dicts?
                return ReduceDictionaryWithUnpack(Parent.LocalContext, _items.AsSpan());
            }

            // create keys & values into array and then call helper function
            // which creates the dictionary
            return ReduceConstant() ?? ReduceDictionaryWithItems(_items.AsSpan());
        }

        private static MSAst.Expression ReduceDictionaryWithUnpack(MSAst.Expression context, ReadOnlySpan<SliceExpression> items) {
            Debug.Assert(items.Length > 0);
            var expressions = new List<MSAst.Expression>(items.Length + 2);
            var varExpr = Expression.Variable(typeof(PythonDictionary), "$dict");
            bool isInit = false;
            var cnt = 0;
            for (var i = 0; i < items.Length; i++) {
                var item = items[i];
                if (item.SliceStart is null) {
                    if (cnt != 0) {
                        var dict = ReduceDictionaryWithItems(items.Slice(i - cnt, cnt));
                        if (!isInit) {
                            expressions.Add(Expression.Assign(varExpr, dict));
                            isInit = true;
                        } else {
                            expressions.Add(Expression.Call(AstMethods.DictUpdate, context, varExpr, dict));
                        }
                        cnt = 0;
                    }
                    if (!isInit) {
                        expressions.Add(Expression.Assign(varExpr, EmptyDictExpression));
                        isInit = true;
                    }
                    expressions.Add(Expression.Call(AstMethods.DictUpdate, context, varExpr, TransformOrConstantNull(item.SliceStop, typeof(object))));
                } else {
                    cnt++;
                }
            }
            if (cnt != 0) {
                var dict = ReduceDictionaryWithItems(items.Slice(items.Length - cnt, cnt));
                if (isInit) {
                    expressions.Add(Expression.Call(AstMethods.DictUpdate, context, varExpr, dict));
                } else {
                    return dict;
                }
            }
            expressions.Add(varExpr);
            return Expression.Block(typeof(PythonDictionary), new MSAst.ParameterExpression[] { varExpr }, expressions);
        }

        private static MSAst.Expression ReduceDictionaryWithItems(ReadOnlySpan<SliceExpression> items) {
            MSAst.Expression[] parts = new MSAst.Expression[items.Length * 2];
            Type? t = null;
            bool heterogeneous = false;
            for (int index = 0; index < items.Length; index++) {
                SliceExpression slice = items[index];
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

            return Expression.Call(
                heterogeneous ? AstMethods.MakeDictFromItems : AstMethods.MakeHomogeneousDictFromItems,
                Expression.NewArrayInit(
                    typeof(object),
                    parts
                )
            );
        }

        private MSAst.Expression? ReduceConstant() {
            for (int index = 0; index < _items.Length; index++) {
                SliceExpression slice = _items[index];
                if (slice.SliceStart is null || !slice.SliceStart.IsConstant || !slice.SliceStop!.IsConstant) {
                    return null;
                }
            }

            CommonDictionaryStorage storage = new CommonDictionaryStorage();
            for (int index = 0; index < _items.Length; index++) {
                SliceExpression slice = _items[index];

                Debug.Assert(slice.SliceStart is not null);
                storage.AddNoLock(slice.SliceStart!.GetConstantValue(), slice.SliceStop!.GetConstantValue());
            }

            return Expression.Call(AstMethods.MakeConstantDict, Expression.Constant(new ConstantDictionaryStorage(storage), typeof(object)));
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

        private class EmptyDictInstruction : Instruction {
            public static readonly EmptyDictInstruction Instance = new EmptyDictInstruction();

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
