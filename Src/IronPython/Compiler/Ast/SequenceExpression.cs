// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using IronPython.Runtime.Binding;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    public abstract class SequenceExpression : Expression {
        private readonly Expression[] _items;

        protected SequenceExpression(Expression[] items) {
            _items = items;
        }

        public IList<Expression> Items => _items;

        internal override MSAst.Expression TransformSet(SourceSpan span, MSAst.Expression right, PythonOperationKind op) {
            // if we just have a simple named multi-assignment  (e.g. a, b = 1,2)
            // then go ahead and step over the entire statement at once.  If we have a 
            // more complex statement (e.g. a.b, c.d = 1, 2) then we'll step over the
            // sets individually as they could be property sets the user wants to step
            // into.  TODO: Enable stepping of the right hand side?
            bool emitIndividualSets = false;
            foreach (Expression e in _items) {
                if (IsComplexAssignment(e)) {
                    emitIndividualSets = true;
                    break;
                }
            }

            SourceSpan rightSpan = SourceSpan.None;
            SourceSpan leftSpan =
                (Span.Start.IsValid && span.IsValid) ?
                    new SourceSpan(Span.Start, span.End) :
                    SourceSpan.None;

            SourceSpan totalSpan = SourceSpan.None;
            if (emitIndividualSets) {
                rightSpan = span;
                leftSpan = SourceSpan.None;
                totalSpan = (Span.Start.IsValid && span.IsValid) ?
                    new SourceSpan(Span.Start, span.End) :
                    SourceSpan.None;
            }

            // 1. Evaluate the expression and assign the value to the temp.
            MSAst.ParameterExpression right_temp = Expression.Variable(typeof(object), "unpacking");

            // 2. Add the assignment "right_temp = right" into the suite/block
            MSAst.Expression assignStmt1 = MakeAssignment(right_temp, right);

            int expected = _items.Length;
            int argcntafter = -1;
            for (var i = 0; i < _items.Length; i++) {
                var item = _items[i];
                if (item is StarredExpression) {
                    expected = i;
                    argcntafter = _items.Length - i - 1;
                    break;
                }
            }

            // 3. Call GetEnumeratorValues on the right side (stored in temp)
            MSAst.Expression enumeratorValues = Expression.Convert(LightExceptions.CheckAndThrow(
                Expression.Call(
                    // method
                    argcntafter != -1 ?
                        AstMethods.UnpackIterable :
                        emitIndividualSets ?
                            AstMethods.GetEnumeratorValues :
                            AstMethods.GetEnumeratorValuesNoComplexSets,
                    // arguments
                    Parent.LocalContext,
                    right_temp,
                    AstUtils.Constant(expected),
                    AstUtils.Constant(argcntafter)
                )
            ), typeof(object[]));

            // 4. Create temporary variable for the array
            MSAst.ParameterExpression array_temp = Expression.Variable(typeof(object[]), "array");

            // 5. Assign the value of the method call (mce) into the array temp
            // And add the assignment "array_temp = Ops.GetEnumeratorValues(...)" into the block
            MSAst.Expression assignStmt2 = MakeAssignment(
                array_temp,
                enumeratorValues,
                rightSpan
            );

            ReadOnlyCollectionBuilder<MSAst.Expression> sets = new ReadOnlyCollectionBuilder<MSAst.Expression>(_items.Length + 1);
            for (int i = 0; i < _items.Length; i++) {
                // target = array_temp[i]

                Expression target = _items[i];
                if (target == null) {
                    continue;
                }

                // 6. array_temp[i]
                MSAst.Expression element = Expression.ArrayAccess(
                    array_temp,                             // array expression
                    AstUtils.Constant(i)                         // index
                );

                // 7. target = array_temp[i], and add the transformed assignment into the list of sets
                MSAst.Expression set = target.TransformSet(
                    emitIndividualSets ?                    // span
                        target.Span :
                        SourceSpan.None,
                    element,
                    PythonOperationKind.None
                );
                sets.Add(set);
            }
            // 9. add the sets as their own block so they can be marked as a single span, if necessary.
            sets.Add(AstUtils.Empty());
            MSAst.Expression itemSet = GlobalParent.AddDebugInfo(Expression.Block(sets.ToReadOnlyCollection()), leftSpan);

            // 10. Return the suite statement (block)
            return GlobalParent.AddDebugInfo(Expression.Block(new[] { array_temp, right_temp }, assignStmt1, assignStmt2, itemSet, AstUtils.Empty()), totalSpan);
        }

        internal override string? CheckAssign() {
            var starCount = 0;
            foreach (var item in Items) {
                if (item.CheckAssign() is { } checkAssign) {
                    // we don't return the same message here as CPython doesn't seem to either, 
                    // for example ((yield a), 2,3) = (2,3,4) gives a different error than
                    // a = yield 3 = yield 4.
                    return checkAssign;
                }

                if (item is StarredExpression && ++starCount > 1) {
                    return "two starred expressions in assignment";
                }
            }

            if (Items.Count > 256 && starCount > 0) {
                return "too many expressions in star-unpacking assignment";
            }

            return null;
        }

        internal override string? CheckDelete() => null;

        internal override string CheckAugmentedAssign()
            => CheckAssign() ?? "illegal expression for augmented assignment";

        private static bool IsComplexAssignment(Expression expr) => !(expr is NameExpression);

        internal override MSAst.Expression TransformDelete() {
            MSAst.Expression[] statements = new MSAst.Expression[_items.Length + 1];
            for (int i = 0; i < _items.Length; i++) {
                statements[i] = _items[i].TransformDelete();
            }
            statements[_items.Length] = AstUtils.Empty();
            return GlobalParent.AddDebugInfo(Expression.Block(statements), Span);
        }

        internal override bool CanThrow {
            get {
                foreach (Expression e in _items) {
                    if (e.CanThrow) {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
