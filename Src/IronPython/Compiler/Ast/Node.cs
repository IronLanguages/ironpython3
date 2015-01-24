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

using Microsoft.Scripting;

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
using System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;


    public abstract class Node : MSAst.Expression {
        private ScopeStatement _parent;
        private IndexSpan _span;

        internal static readonly MSAst.BlockExpression EmptyBlock = Ast.Block(AstUtils.Empty());
        internal static readonly MSAst.Expression[] EmptyExpression = new MSAst.Expression[0];

        internal static MSAst.ParameterExpression FunctionStackVariable = Ast.Variable(typeof(List<FunctionStack>), "$funcStack");
        internal static readonly MSAst.LabelTarget GeneratorLabel = Ast.Label(typeof(object), "$generatorLabel");
        private static MSAst.ParameterExpression _lineNumberUpdated = Ast.Variable(typeof(bool), "$lineUpdated");
        private static readonly MSAst.ParameterExpression _lineNoVar = Ast.Parameter(typeof(int), "$lineNo");

        protected Node() {
        }

        #region Public API

        public ScopeStatement Parent {
            get { return _parent; }
            set { _parent = value; }
        }
        
        public void SetLoc(PythonAst globalParent, int start, int end) {
            _span = new IndexSpan(start, end > start ? end - start : start);
            _parent = globalParent;
        }

        public void SetLoc(PythonAst globalParent, IndexSpan span) {
            _span = span;
            _parent = globalParent;
        }

        public IndexSpan IndexSpan {
            get {
                return _span;
            }
            set {
                _span = value;
            }
        }

        public SourceLocation Start {
            get {
                return GlobalParent.IndexToLocation(StartIndex); 
            }
        }

        public SourceLocation End {
            get {
                return GlobalParent.IndexToLocation(EndIndex);
            }
        }

        public int EndIndex {
            get {
                return _span.End;
            }
            set {
                _span = new IndexSpan(_span.Start, value - _span.Start);
            }
        }

        public int StartIndex {
            get {
                return _span.Start;
            }
            set {
                _span = new IndexSpan(value, 0);
            }
        }
        
        internal SourceLocation IndexToLocation(int index) {
            if (index == -1) {
                return SourceLocation.Invalid;
            }

            var locs = GlobalParent._lineLocations;
            int match = Array.BinarySearch(locs, index);
            if (match < 0) {
                // If our index = -1, it means we're on the first line.
                if (match == -1) {
                    return new SourceLocation(index, 1, index + 1);
                }

                // If we couldn't find an exact match for this line number, get the nearest
                // matching line number less than this one
                match = ~match - 1;
            }
            return new SourceLocation(index, match + 2, index - locs[match] + 1);
        }

        public SourceSpan Span {
            get {
                return new SourceSpan(Start, End);
            }
        }

        public abstract void Walk(PythonWalker walker);

        public virtual string NodeName {
            get {
                return GetType().Name;
            }
        }

        #endregion

        #region Base Class Overrides

        /// <summary>
        /// Returns true if the node can throw, false otherwise.  Used to determine
        /// whether or not we need to update the current dynamic stack info.
        /// </summary>
        internal virtual bool CanThrow {
            get {
                return true;
            }
        }

        public override bool CanReduce {
            get {
                return true;
            }
        }

        public override MSAst.ExpressionType NodeType {
            get {
                return MSAst.ExpressionType.Extension;
            }
        }

        public override string ToString() {
            return GetType().Name;
        }

        #endregion

        #region Internal APIs

        internal PythonAst GlobalParent {
            get {
                Node cur = this;
                while (!(cur is PythonAst)) {
                    Debug.Assert(cur != null);
                    cur = cur.Parent;
                }
                return (PythonAst)cur;
            }
        }

        internal bool EmitDebugSymbols {
            get {
                return GlobalParent.EmitDebugSymbols;
            }
        }

        internal bool StripDocStrings {
            get {
                return GlobalParent.PyContext.PythonOptions.StripDocStrings;
            }
        }

        internal bool Optimize {
            get {
                return GlobalParent.PyContext.PythonOptions.Optimize;
            }
        }

        internal virtual string GetDocumentation(Statement/*!*/ stmt) {
            if (StripDocStrings) {
                return null;
            }

            return stmt.Documentation;
        }

        #endregion

        #region Transformation Helpers

        internal static MSAst.Expression[] ToObjectArray(IList<Expression> expressions) {
            MSAst.Expression[] to = new MSAst.Expression[expressions.Count];
            for (int i = 0; i < expressions.Count; i++) {
                to[i] = AstUtils.Convert(expressions[i], typeof(object));
            }
            return to;
        }

        internal static MSAst.Expression TransformOrConstantNull(Expression expression, Type/*!*/ type) {
            if (expression == null) {
                return AstUtils.Constant(null, type);
            } else {
                return AstUtils.Convert(expression, type);
            }
        }

        internal MSAst.Expression TransformAndDynamicConvert(Expression expression, Type/*!*/ type) {
            Debug.Assert(expression != null);
            
            MSAst.Expression res = expression;

            // Do we need conversion?
            if (!CanAssign(type, expression.Type)) {
                // ensure we're reduced before we check for dynamic expressions.

                var reduced = expression.Reduce();
                if (reduced is LightDynamicExpression) {
                    reduced = reduced.Reduce();
                }
                
                // Add conversion step to the AST
                MSAst.DynamicExpression ae = reduced as MSAst.DynamicExpression;
                ReducableDynamicExpression rde = reduced as ReducableDynamicExpression;

                if ((ae != null && ae.Binder is PythonBinaryOperationBinder) ||
                    (rde != null && rde.Binder is PythonBinaryOperationBinder)) {
                    // create a combo site which does the conversion
                    PythonBinaryOperationBinder binder;
                    IList<MSAst.Expression> args;
                    if (ae != null) {
                        binder = (PythonBinaryOperationBinder)ae.Binder;
                        args = ArrayUtils.ToArray(ae.Arguments);
                    } else {
                        binder = (PythonBinaryOperationBinder)rde.Binder;
                        args = rde.Args;
                    }

                    ParameterMappingInfo[] infos = new ParameterMappingInfo[args.Count];
                    for (int i = 0; i < infos.Length; i++) {
                        infos[i] = ParameterMappingInfo.Parameter(i);
                    }
                    
                    res = Expression.Dynamic(
                        GlobalParent.PyContext.BinaryOperationRetType(
                            binder,
                            GlobalParent.PyContext.Convert(
                                type,
                                ConversionResultKind.ExplicitCast
                            )
                        ),
                        type,
                        args
                    );
                } else {
                    res = GlobalParent.Convert(
                        type,
                        ConversionResultKind.ExplicitCast,
                        reduced
                    );
                }
            }
            return res;
        }

        internal static bool CanAssign(Type/*!*/ to, Type/*!*/ from) {
            return to.IsAssignableFrom(from) && (to.IsValueType == from.IsValueType);
        }

        internal static MSAst.Expression/*!*/ ConvertIfNeeded(MSAst.Expression/*!*/ expression, Type/*!*/ type) {
            Debug.Assert(expression != null);
            // Do we need conversion?
            if (!CanAssign(type, expression.Type)) {
                // Add conversion step to the AST
                expression = AstUtils.Convert(expression, type);
            }
            return expression;
        }

        internal static MSAst.Expression TransformMaybeSingleLineSuite(Statement body, SourceLocation prevStart) {
            if (body.GlobalParent.IndexToLocation(body.StartIndex).Line != prevStart.Line) {
                return body;
            }

            MSAst.Expression res = body.Reduce();

            res = RemoveDebugInfo(prevStart.Line, res);

            if (res.Type != typeof(void)) {
                res = AstUtils.Void(res);
            }

            return res;
        }

        internal static MSAst.Expression RemoveDebugInfo(int prevStart, MSAst.Expression res) {
            MSAst.BlockExpression block = res as MSAst.BlockExpression;
            if (block != null && block.Expressions.Count > 0) {
                MSAst.DebugInfoExpression dbgInfo = block.Expressions[0] as MSAst.DebugInfoExpression;
                // body on the same line as an if, don't generate a 2nd sequence point
                if (dbgInfo != null && dbgInfo.StartLine == prevStart) {
                    // we remove the debug info based upon how it's generated in DebugStatement.AddDebugInfo which is
                    // the helper method which adds the debug info.
                    if (block.Type == typeof(void)) {
                        Debug.Assert(block.Expressions.Count == 3);
                        Debug.Assert(block.Expressions[2] is MSAst.DebugInfoExpression && ((MSAst.DebugInfoExpression)block.Expressions[2]).IsClear);
                        res = block.Expressions[1];
                    } else {
                        Debug.Assert(block.Expressions.Count == 4);
                        Debug.Assert(block.Expressions[3] is MSAst.DebugInfoExpression && ((MSAst.DebugInfoExpression)block.Expressions[2]).IsClear);
                        Debug.Assert(block.Expressions[1] is MSAst.BinaryExpression && ((MSAst.BinaryExpression)block.Expressions[2]).NodeType == MSAst.ExpressionType.Assign);
                        res = ((MSAst.BinaryExpression)block.Expressions[1]).Right;
                    }
                }
            }
            return res;
        }

        /// <summary>
        /// Creates a method frame for tracking purposes and enforces recursion
        /// </summary>
        internal static MSAst.Expression AddFrame(MSAst.Expression localContext, MSAst.Expression codeObject, MSAst.Expression body) {
            return new FramedCodeExpression(localContext, codeObject, body);
        }

        /// <summary>
        /// Removes the frames from generated code for when we're compiling the tracing delegate
        /// which will track the frames it's self.
        /// </summary>
        internal static MSAst.Expression RemoveFrame(MSAst.Expression expression) {
            return new FramedCodeVisitor().Visit(expression);
        }

        class FramedCodeVisitor : ExpressionVisitor {
            public override MSAst.Expression Visit(MSAst.Expression node) {
                FramedCodeExpression framedCode = node as FramedCodeExpression;
                if (framedCode != null) {
                    return framedCode.Body;
                }
                return base.Visit(node);
            }
        }

        sealed class FramedCodeExpression : MSAst.Expression {
            private readonly MSAst.Expression _localContext, _codeObject, _body;

            public FramedCodeExpression(MSAst.Expression localContext, MSAst.Expression codeObject, MSAst.Expression body) {
                _localContext = localContext;
                _codeObject = codeObject;
                _body = body;
            }

            public override ExpressionType NodeType {
                get {
                    return ExpressionType.Extension;
                }
            }

            public MSAst.Expression Body {
                get {
                    return _body;
                }
            }

            public override MSAst.Expression Reduce() {
                return AstUtils.Try(
                    Ast.Assign(
                        FunctionStackVariable,
                        Ast.Call(
                            AstMethods.PushFrame,
                            _localContext,
                            _codeObject
                        )
                    ),
                    _body
                ).Finally(
                    Ast.Call(
                        FunctionStackVariable,
                        typeof(List<FunctionStack>).GetMethod("RemoveAt"),
                        Ast.Add(
                            Ast.Property(
                                FunctionStackVariable,
                                "Count"
                            ),
                            Ast.Constant(-1)
                        )
                    )
                );
            }

            public override Type Type {
                get {
                    return _body.Type;
                }
            }

            public override bool CanReduce {
                get {
                    return true;
                }
            }

            protected override MSAst.Expression VisitChildren(ExpressionVisitor visitor) {
                var localContext = visitor.Visit(_localContext);
                var codeObject = visitor.Visit(_codeObject);
                var body = visitor.Visit(_body);

                if (localContext != _localContext || _codeObject != codeObject || body != _body) {
                    return new FramedCodeExpression(localContext, codeObject, body);
                }

                return this;
            }
        }

        internal static MSAst.Expression/*!*/ MakeAssignment(MSAst.ParameterExpression/*!*/ variable, MSAst.Expression/*!*/ right) {
            return Ast.Assign(variable, AstUtils.Convert(right, variable.Type));
        }

        internal MSAst.Expression MakeAssignment(MSAst.ParameterExpression variable, MSAst.Expression right, SourceSpan span) {
            return GlobalParent.AddDebugInfoAndVoid(Ast.Assign(variable, AstUtils.Convert(right, variable.Type)), span);
        }

        internal static MSAst.Expression/*!*/ AssignValue(MSAst.Expression/*!*/ expression, MSAst.Expression value) {
            Debug.Assert(expression != null);
            Debug.Assert(value != null);

            IPythonVariableExpression pyGlobal = expression as IPythonVariableExpression;
            if (pyGlobal != null) {
                return pyGlobal.Assign(value);
            }

            return Ast.Assign(expression, value);
        }

        internal static MSAst.Expression/*!*/ Delete(MSAst.Expression/*!*/ expression) {
            IPythonVariableExpression pyGlobal = expression as IPythonVariableExpression;
            if (pyGlobal != null) {
                return pyGlobal.Delete();
            }

            return Ast.Assign(expression, Ast.Field(null, typeof(Uninitialized).GetField("Instance")));
        }

        #endregion

        #region Basic Line Number Infrastructure

        /// <summary>
        /// A temporary variable to track if the current line number has been emitted via the fault update block.
        /// 
        /// For example consider:
        /// 
        /// try:
        ///     raise Exception()
        /// except Exception, e:
        ///     # do something here
        ///     raise
        ///     
        /// At "do something here" we need to have already emitted the line number, when we re-raise we shouldn't add it 
        /// again.  If we handled the exception then we should have set the bool back to false.
        /// 
        /// We also sometimes directly check _lineNoUpdated to avoid creating this unless we have nested exceptions.
        /// </summary>
        internal static MSAst.ParameterExpression/*!*/ LineNumberUpdated {
            get {
                return _lineNumberUpdated;
            }
        }
        
        internal static MSAst.Expression UpdateLineNumber(int line) {
            return Ast.Assign(LineNumberExpression, AstUtils.Constant(line));
        }

        internal static MSAst.Expression UpdateLineUpdated(bool updated) {
            return Ast.Assign(LineNumberUpdated, AstUtils.Constant(updated));
        }

        internal static MSAst.Expression PushLineUpdated(bool updated, MSAst.ParameterExpression saveCurrent) {
            return MSAst.Expression.Block(
                Ast.Assign(saveCurrent, LineNumberUpdated),
                Ast.Assign(LineNumberUpdated, AstUtils.Constant(updated))
            );
        }

        internal static MSAst.Expression PopLineUpdated(MSAst.ParameterExpression saveCurrent) {
            return Ast.Assign(LineNumberUpdated, saveCurrent);
        }

        /// <summary>
        /// A temporary variable to track the current line number
        /// </summary>
        internal static MSAst.ParameterExpression/*!*/ LineNumberExpression {
            get {
                return _lineNoVar;
            }
        }

        #endregion
    }
}
