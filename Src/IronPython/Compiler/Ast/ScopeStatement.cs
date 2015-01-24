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
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Binding;

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    public abstract class ScopeStatement : Statement {
        private bool _importStar;                   // from module import *
        private bool _unqualifiedExec;              // exec "code"
        private bool _nestedFreeVariables;          // nested function with free variable
        private bool _locals;                       // The scope needs locals dictionary
                                                    // due to "exec" or call to dir, locals, eval, vars...
        private bool _hasLateboundVarSets;          // calls code which can assign to variables
        private bool _containsExceptionHandling;    // true if this block contains a try/with statement
        private bool _forceCompile;                 // true if this scope should always be compiled

        private FunctionCode _funcCode;             // the function code object created for this scope

        private Dictionary<string, PythonVariable> _variables;          // mapping of string to variables
        private ClosureInfo[] _closureVariables;                        // closed over variables, bool indicates if we accessed it in this scope.
        private List<PythonVariable> _freeVars;                         // list of variables accessed from outer scopes
        private List<string> _globalVars;                               // global variables accessed from this scope
        private List<string> _cellVars;                                 // variables accessed from nested scopes
        private Dictionary<string, PythonReference> _references;        // names of all variables referenced, null after binding completes

        internal Dictionary<PythonVariable, MSAst.Expression> _variableMapping = new Dictionary<PythonVariable, MSAst.Expression>();
        private MSAst.ParameterExpression _localParentTuple;                                        // parent's tuple local saved locally
        private readonly DelayedFunctionCode _funcCodeExpr = new DelayedFunctionCode();             // expression that refers to the function code for this scope

        internal static MSAst.ParameterExpression LocalCodeContextVariable = Ast.Parameter(typeof(CodeContext), "$localContext");
        private static MSAst.ParameterExpression _catchException = Ast.Parameter(typeof(Exception), "$updException");
        internal const string NameForExec = "module: <exec>";
        
        internal bool ContainsImportStar {
            get { return _importStar; }
            set { _importStar = value; }
        }

        internal bool ContainsExceptionHandling {
            get {
                return _containsExceptionHandling;
            }
            set {
                _containsExceptionHandling = value;
            }
        }

        internal bool ContainsUnqualifiedExec {
            get { return _unqualifiedExec; }
            set { _unqualifiedExec = value; }
        }

        internal virtual bool IsGeneratorMethod {
            get {
                return false;
            }
        }

        /// <summary>
        /// The variable used to hold out parents closure tuple in our local scope.
        /// </summary>
        internal MSAst.ParameterExpression LocalParentTuple {
            get {
                return _localParentTuple;
            }
        }

        /// <summary>
        /// Gets the expression associated with the local CodeContext.  If the function
        /// doesn't have a local CodeContext then this is the global context.
        /// </summary>
        internal virtual MSAst.Expression LocalContext {
            get {
                return LocalCodeContextVariable;
            }
        }

        /// <summary>
        /// True if this scope accesses a variable from an outer scope.
        /// </summary>
        internal bool IsClosure {
            get { return FreeVariables != null && FreeVariables.Count > 0; }
        }

        /// <summary>
        /// True if an inner scope is accessing a variable defined in this scope.
        /// </summary>
        internal bool ContainsNestedFreeVariables {
            get { return _nestedFreeVariables; }
            set { _nestedFreeVariables = value; }
        }

        /// <summary>
        /// True if we are forcing the creation of a dictionary for storing locals.
        /// 
        /// This occurs for calls to locals(), dir(), vars(), unqualified exec, and
        /// from ... import *.
        /// </summary>
        internal bool NeedsLocalsDictionary {
            get { return _locals; }
            set { _locals = value; }
        }

        public virtual string Name {
            get {
                return "<unknown>";
            }
        }

        internal virtual string Filename {
            get {
                return GlobalParent.SourceUnit.Path ?? "<string>";
            }
        }

        /// <summary>
        /// True if variables can be set in a late bound fashion that we don't
        /// know about at code gen time - for example via from foo import *.
        /// 
        /// This is tracked independently of the ContainsUnqualifiedExec/NeedsLocalsDictionary
        /// </summary>
        internal virtual bool HasLateBoundVariableSets {
            get {
                return _hasLateboundVarSets;
            }
            set {
                _hasLateboundVarSets = value;
            }
        }

        internal Dictionary<string, PythonVariable> Variables {
            get { return _variables; }
        }

        internal virtual bool IsGlobal {
            get { return false; }
        }

        internal bool NeedsLocalContext {
            get {
                return NeedsLocalsDictionary || ContainsNestedFreeVariables;
            }
        }

        internal virtual string[] ParameterNames {
            get {
                return ArrayUtils.EmptyStrings;
            }
        }

        internal virtual int ArgCount {
            get {
                return 0;
            }
        }

        internal virtual FunctionAttributes Flags {
            get {
                return FunctionAttributes.None;
            }
        }

        internal abstract Microsoft.Scripting.Ast.LightLambdaExpression GetLambda();

        /// <summary>
        /// Gets or creates the FunctionCode object for this FunctionDefinition.
        /// </summary>
        internal FunctionCode GetOrMakeFunctionCode() {
            if (_funcCode == null) {
                Interlocked.CompareExchange(ref _funcCode, new FunctionCode(GlobalParent.PyContext, OriginalDelegate, this, ScopeDocumentation, null, true), null);
            }
            return _funcCode;
        }

        internal virtual string ScopeDocumentation {
            get {
                return null;
            }
        }

        internal virtual Delegate OriginalDelegate {
            get {
                return null;
            }
        }

        internal virtual IList<string> GetVarNames() {
            List<string> res = new List<string>();

            AppendVariables(res);

            return res;
        }


        internal void AddFreeVariable(PythonVariable variable, bool accessedInScope) {
            if (_freeVars == null) {
                _freeVars = new List<PythonVariable>();
            }

            if(!_freeVars.Contains(variable)) {
                _freeVars.Add(variable);
            }
        }

        internal bool ShouldInterpret {
            get {
                if (_forceCompile) {
                    return false;
                } else if (GlobalParent.CompilationMode == CompilationMode.Lookup) {
                    return true;
                }
                CompilerContext context = GlobalParent.CompilerContext;

                return ((PythonContext)context.SourceUnit.LanguageContext).ShouldInterpret((PythonCompilerOptions)context.Options, context.SourceUnit);
            }
            set {
                _forceCompile = !value;
            }
        }

        internal string AddReferencedGlobal(string name) {
            if (_globalVars == null) {
                _globalVars = new List<string>();
            }
            if (!_globalVars.Contains(name)) {
                _globalVars.Add(name);
            }
            return name;
        }

        internal void AddCellVariable(PythonVariable variable) {
            if (_cellVars == null) {
                _cellVars = new List<string>();
            }

            if (!_cellVars.Contains(variable.Name)) {
                _cellVars.Add(variable.Name);
            }
        }

        internal List<string> AppendVariables(List<string> res) {
            if (Variables != null) {
                foreach (var variable in Variables) {
                    if (variable.Value.Kind != VariableKind.Local) {
                        continue;
                    }

                    if (CellVariables == null || !CellVariables.Contains(variable.Key)) {
                        res.Add(variable.Key);
                    }
                }
            }
            return res;
        }

        /// <summary>
        /// Variables that are bound in an outer scope - but not a global scope
        /// </summary>
        internal IList<PythonVariable> FreeVariables {
            get {
                return _freeVars;
            }
        }

        /// <summary>
        /// Variables that are bound to the global scope
        /// </summary>
        internal IList<string> GlobalVariables {
            get {
                return _globalVars;
            }
        }

        /// <summary>
        /// Variables that are referred to from a nested scope and need to be
        /// promoted to cells.
        /// </summary>
        internal IList<string> CellVariables {
            get {
                return _cellVars;
            }
        }

        internal Type GetClosureTupleType() {
            if (TupleCells > 0) {
                Type[] args = new Type[TupleCells];
                for (int i = 0; i < TupleCells; i++) {
                    args[i] = typeof(ClosureCell);
                }
                return MutableTuple.MakeTupleType(args);
            }
            return null;
        }

        internal virtual int TupleCells {
            get {
                if (_closureVariables == null) {
                    return 0;
                }

                return _closureVariables.Length;
            }
        }

        internal abstract bool ExposesLocalVariable(PythonVariable variable);

        internal virtual MSAst.Expression GetParentClosureTuple() {
            // PythonAst will never call this.
            throw new NotSupportedException();
        }

        private bool TryGetAnyVariable(string name, out PythonVariable variable) {
            if (_variables != null) {
                return _variables.TryGetValue(name, out variable);
            } else {
                variable = null;
                return false;
            }
        }

        internal bool TryGetVariable(string name, out PythonVariable variable) {
            if (TryGetAnyVariable(name, out variable)) {
                return true;
            } else {
                variable = null;
                return false;
            }
        }

        internal virtual bool TryBindOuter(ScopeStatement from, PythonReference reference, out PythonVariable variable) {
            // Hide scope contents by default (only functions expose their locals)
            variable = null;
            return false;
        }

        internal abstract PythonVariable BindReference(PythonNameBinder binder, PythonReference reference);

        internal virtual void Bind(PythonNameBinder binder) {
            if (_references != null) {
                foreach (var reference in _references.Values) {
                    PythonVariable variable;
                    reference.PythonVariable = variable = BindReference(binder, reference);

                    // Accessing outer scope variable which is being deleted?
                    if (variable != null) {
                        if (variable.Deleted && variable.Scope != this && !variable.Scope.IsGlobal) {

                            // report syntax error
                            binder.ReportSyntaxError(
                                String.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    "can not delete variable '{0}' referenced in nested scope",
                                    reference.Name
                                    ),
                                this);
                        }
                    }
                }
            }
        }

        internal virtual void FinishBind(PythonNameBinder binder) {
            List<ClosureInfo> closureVariables = null;
            
            if (FreeVariables != null && FreeVariables.Count > 0) {
                _localParentTuple = Ast.Parameter(Parent.GetClosureTupleType(), "$tuple");

                foreach (var variable in _freeVars) {
                    var parentClosure = Parent._closureVariables;                    
                    Debug.Assert(parentClosure != null);
                    
                    for (int i = 0; i < parentClosure.Length; i++) {
                        if (parentClosure[i].Variable == variable) {
                            _variableMapping[variable] = new ClosureExpression(variable, Ast.Property(_localParentTuple, String.Format("Item{0:D3}", i)), null);
                            break;
                        }
                    }
                    Debug.Assert(_variableMapping.ContainsKey(variable));
                    
                    if (closureVariables == null) {
                        closureVariables = new List<ClosureInfo>();
                    }
                    closureVariables.Add(new ClosureInfo(variable, !(this is ClassDefinition)));
                }
            }

            if (Variables != null) {
                foreach (PythonVariable variable in Variables.Values) {
                    if (!HasClosureVariable(closureVariables, variable) &&
                        !variable.IsGlobal && (variable.AccessedInNestedScope || ExposesLocalVariable(variable))) {
                        if (closureVariables == null) {
                            closureVariables = new List<ClosureInfo>();
                        }
                        closureVariables.Add(new ClosureInfo(variable, true));
                    }

                    if (variable.Kind == VariableKind.Local) {
                        Debug.Assert(variable.Scope == this);

                        if (variable.AccessedInNestedScope || ExposesLocalVariable(variable)) {
                            _variableMapping[variable] = new ClosureExpression(variable, Ast.Parameter(typeof(ClosureCell), variable.Name), null);
                        } else {
                            _variableMapping[variable] = Ast.Parameter(typeof(object), variable.Name);
                        }
                    }
                }
            }

            if (closureVariables != null) {
                _closureVariables = closureVariables.ToArray();
            }

            // no longer needed
            _references = null;
        }

        private static bool HasClosureVariable(List<ClosureInfo> closureVariables, PythonVariable variable) {
            if (closureVariables == null) {
                return false;
            }

            for (int i = 0; i < closureVariables.Count; i++) {
                if (closureVariables[i].Variable == variable) {
                    return true;
                }
            }

            return false;
        }

        private void EnsureVariables() {
            if (_variables == null) {
                _variables = new Dictionary<string, PythonVariable>(StringComparer.Ordinal);
            }
        }

        internal void AddGlobalVariable(PythonVariable variable) {
            EnsureVariables();
            _variables[variable.Name] = variable;
        }

        internal PythonReference Reference(string name) {
            if (_references == null) {
                _references = new Dictionary<string, PythonReference>(StringComparer.Ordinal);
            }
            PythonReference reference;
            if (!_references.TryGetValue(name, out reference)) {
                _references[name] = reference = new PythonReference(name);
            }
            return reference;
        }

        internal bool IsReferenced(string name) {
            PythonReference reference;
            return _references != null && _references.TryGetValue(name, out reference);
        }

        internal PythonVariable/*!*/ CreateVariable(string name, VariableKind kind) {
            EnsureVariables();
            Debug.Assert(!_variables.ContainsKey(name));
            PythonVariable variable;
            _variables[name] = variable = new PythonVariable(name, kind, this);
            return variable;
        }

        internal PythonVariable/*!*/ EnsureVariable(string name) {
            PythonVariable variable;
            if (!TryGetVariable(name, out variable)) {
                return CreateVariable(name, VariableKind.Local);
            }
            return variable;
        }

        internal PythonVariable DefineParameter(string name) {
            return CreateVariable(name, VariableKind.Parameter);
        }

        internal PythonContext PyContext {
            get {
                return (PythonContext)GlobalParent.CompilerContext.SourceUnit.LanguageContext;
            }
        }

        #region Debug Info Tracking

        private MSAst.SymbolDocumentInfo Document {
            get {
                return GlobalParent.Document;
            }
        }

        internal MSAst.Expression/*!*/ AddDebugInfo(MSAst.Expression/*!*/ expression, SourceLocation start, SourceLocation end) {
            if (PyContext.PythonOptions.GCStress != null) {
                expression = Ast.Block(
                    Ast.Call(
                        typeof(GC).GetMethod("Collect", new[] { typeof(int) }),
                        Ast.Constant(PyContext.PythonOptions.GCStress.Value)
                    ),
                    expression
                );
            }

            return AstUtils.AddDebugInfo(expression, Document, start, end);
        }

        internal MSAst.Expression/*!*/ AddDebugInfo(MSAst.Expression/*!*/ expression, SourceSpan location) {
            return AddDebugInfo(expression, location.Start, location.End);
        }

        internal MSAst.Expression/*!*/ AddDebugInfoAndVoid(MSAst.Expression/*!*/ expression, SourceSpan location) {
            if (expression.Type != typeof(void)) {
                expression = AstUtils.Void(expression);
            }
            return AddDebugInfo(expression, location);
        }

        #endregion

        #region Runtime Line Number Tracing

        /// <summary>
        /// Gets the expression for updating the dynamic stack trace at runtime when an
        /// exception is thrown.
        /// </summary>
        internal MSAst.Expression GetUpdateTrackbackExpression(MSAst.ParameterExpression exception) {
            if (!_containsExceptionHandling) {
                Debug.Assert(Name != null);
                Debug.Assert(exception.Type == typeof(Exception));
                return UpdateStackTrace(exception);
            }

            return GetSaveLineNumberExpression(exception, true);
        }

        private MSAst.Expression UpdateStackTrace(MSAst.ParameterExpression exception) {
            return Ast.Call(
                AstMethods.UpdateStackTrace,
                exception,
                LocalContext,
                _funcCodeExpr,
                LineNumberExpression
            );
        }


        /// <summary>
        /// Gets the expression for the actual updating of the line number for stack traces to be available
        /// </summary>
        internal MSAst.Expression GetSaveLineNumberExpression(MSAst.ParameterExpression exception, bool preventAdditionalAdds) {
            Debug.Assert(exception.Type == typeof(Exception));
            return Ast.Block(
                AstUtils.If(
                    Ast.Not(
                        LineNumberUpdated
                    ),
                    UpdateStackTrace(exception)
                ),
                Ast.Assign(
                    LineNumberUpdated,
                    AstUtils.Constant(preventAdditionalAdds)
                ),
                AstUtils.Empty()
            );
        }

        /// <summary>
        /// Wraps the body of a statement which should result in a frame being available during
        /// exception handling.  This ensures the line number is updated as the stack is unwound.
        /// </summary>
        internal MSAst.Expression/*!*/ WrapScopeStatements(MSAst.Expression/*!*/ body, bool canThrow) {
            if (canThrow) {
                body = Ast.Block(
                    new[] { LineNumberExpression, LineNumberUpdated },
                    Ast.TryCatch(
                        body,
                        Ast.Catch(
                            _catchException,
                            Ast.Block(
                                GetUpdateTrackbackExpression(_catchException),
                                Ast.Rethrow(body.Type)
                            )
                        )
                    )
                );
            }
            
            return body;
        }

        #endregion

        /// <summary>
        /// Provides a place holder for the expression which represents
        /// a FunctionCode.  For functions/classes this gets updated after
        /// the AST has been generated because the FunctionCode needs to
        /// know about the tree which gets generated.  For modules we 
        /// immediately have the value because it always comes in as a parameter.
        /// </summary>
        class DelayedFunctionCode : MSAst.Expression {
            private MSAst.Expression _funcCode;

            public override bool CanReduce {
                get {
                    return true;
                }
            }

            public MSAst.Expression Code {
                get {
                    return _funcCode;
                }
                set {
                    _funcCode = value;
                }
            }

            public override Type Type {
                get {
                    return typeof(FunctionCode);
                }
            }

            protected override MSAst.Expression VisitChildren(MSAst.ExpressionVisitor visitor) {
                if (_funcCode != null) {
                    MSAst.Expression funcCode = visitor.Visit(_funcCode);
                    if (funcCode != _funcCode) {
                        DelayedFunctionCode res = new DelayedFunctionCode();
                        res._funcCode = funcCode;
                        return res;
                    }
                }
                return this;
            }

            public override MSAst.Expression Reduce() {
                Debug.Assert(_funcCode != null);
                return _funcCode;
            }

            public override MSAst.ExpressionType NodeType {
                get {
                    return MSAst.ExpressionType.Extension;
                }
            }
        }

        internal MSAst.Expression FuncCodeExpr {
            get {
                return _funcCodeExpr.Code;
            }
            set {
                _funcCodeExpr.Code = value;
            }
        }

        internal MSAst.MethodCallExpression CreateLocalContext(MSAst.Expression parentContext) {
            var closureVariables = _closureVariables;
            if (_closureVariables == null) {
                closureVariables = new ClosureInfo[0];
            }
            return Ast.Call(
                AstMethods.CreateLocalContext,
                parentContext,
                MutableTuple.Create(ArrayUtils.ConvertAll(closureVariables, x => GetClosureCell(x))),
                Ast.Constant(ArrayUtils.ConvertAll(closureVariables, x => x.AccessedInScope ? x.Variable.Name : null))
            );
        }

        private MSAst.Expression GetClosureCell(ClosureInfo variable) {
            return ((ClosureExpression)GetVariableExpression(variable.Variable)).ClosureCell;
        }

        internal virtual MSAst.Expression GetVariableExpression(PythonVariable variable) {
            if (variable.IsGlobal) {
                return GlobalParent.ModuleVariables[variable];
            }

            Debug.Assert(_variableMapping.ContainsKey(variable));
            return _variableMapping[variable];
        }

        internal void CreateVariables(ReadOnlyCollectionBuilder<MSAst.ParameterExpression> locals, List<MSAst.Expression> init) {
            if (Variables != null) {
                foreach (PythonVariable variable in Variables.Values) {
                    if(variable.Kind != VariableKind.Global) {
                        
                        ClosureExpression closure = GetVariableExpression(variable) as ClosureExpression;
                        if (closure != null) {
                            init.Add(closure.Create());
                            locals.Add((MSAst.ParameterExpression)closure.ClosureCell);
                        } else if (variable.Kind == VariableKind.Local) {
                            locals.Add((MSAst.ParameterExpression)GetVariableExpression(variable));
                            if (variable.ReadBeforeInitialized) {
                                init.Add(
                                    AssignValue(
                                        GetVariableExpression(variable),
                                        MSAst.Expression.Field(null, typeof(Uninitialized).GetField("Instance"))
                                    )
                                );
                            }
                        }
                    }
                }
            }

            if (IsClosure) {
                Type tupleType = Parent.GetClosureTupleType();
                Debug.Assert(tupleType != null);

                init.Add(
                    MSAst.Expression.Assign(
                        LocalParentTuple,
                        MSAst.Expression.Convert(
                            GetParentClosureTuple(),
                            tupleType
                        )
                    )
                );

                locals.Add(LocalParentTuple);
            }
        }

        internal MSAst.Expression AddDecorators(MSAst.Expression ret, IList<Expression> decorators) {
            // add decorators
            if (decorators != null) {
                for (int i = decorators.Count - 1; i >= 0; i--) {
                    Expression decorator = decorators[i];
                    ret = Parent.Invoke(
                        new CallSignature(1),
                        Parent.LocalContext,
                        decorator,
                        ret
                    );
                }
            }
            return ret;
        }

        internal MSAst.Expression/*!*/ Invoke(CallSignature signature, params MSAst.Expression/*!*/[]/*!*/ args) {
            PythonInvokeBinder invoke = PyContext.Invoke(signature);
            switch (args.Length) {
                case 1: return GlobalParent.CompilationMode.Dynamic(invoke, typeof(object), args[0]);
                case 2: return GlobalParent.CompilationMode.Dynamic(invoke, typeof(object), args[0], args[1]);
                case 3: return GlobalParent.CompilationMode.Dynamic(invoke, typeof(object), args[0], args[1], args[2]);
                case 4: return GlobalParent.CompilationMode.Dynamic(invoke, typeof(object), args[0], args[1], args[2], args[3]);
                default:
                    return GlobalParent.CompilationMode.Dynamic(
                        invoke,
                        typeof(object),
                        args
                    );
            }
        }

        internal ScopeStatement CopyForRewrite() {
            return (ScopeStatement)MemberwiseClone();
        }

        internal virtual void RewriteBody(MSAst.ExpressionVisitor visitor) {
            _funcCode = null;
        }

        struct ClosureInfo {
            public PythonVariable Variable;
            public bool AccessedInScope;

            public ClosureInfo(PythonVariable variable, bool accessedInScope) {
                Variable = variable;
                AccessedInScope = accessedInScope;
            }
        }

        internal virtual bool PrintExpressions {
            get {
                return false;
            }
        }

        #region Profiling Support

        internal virtual string ProfilerName {
            get {
                return Name;
            }
        }

        /// <summary>
        /// Reducible node so that re-writing for profiling does not occur until
        /// after the script code has been completed and is ready to be compiled.
        /// 
        /// Without this extra node profiling would force reduction of the node
        /// and we wouldn't have setup our constant access correctly yet.
        /// </summary>
        class DelayedProfiling : MSAst.Expression {
            private readonly ScopeStatement _ast;
            private readonly MSAst.Expression _body;
            private readonly MSAst.ParameterExpression _tick;

            public DelayedProfiling(ScopeStatement ast, MSAst.Expression body, MSAst.ParameterExpression tick) {
                _ast = ast;
                _body = body;
                _tick = tick;
            }

            public override bool CanReduce {
                get {
                    return true;
                }
            }

            public override Type Type {
                get {
                    return _body.Type;
                }
            }

            protected override MSAst.Expression VisitChildren(MSAst.ExpressionVisitor visitor) {
                return visitor.Visit(_body);
            }

            public override MSAst.Expression Reduce() {
                string profilerName = _ast.ProfilerName;
                bool unique = (profilerName == NameForExec);
                return Ast.Block(
                    new[] { _tick },
                    _ast.GlobalParent._profiler.AddProfiling(_body, _tick, profilerName, unique)
                );
            }

            public override MSAst.ExpressionType NodeType {
                get {
                    return MSAst.ExpressionType.Extension;
                }
            }
        }

        internal MSAst.Expression AddProfiling(MSAst.Expression/*!*/ body) {
            if (GlobalParent._profiler != null) {
                MSAst.ParameterExpression tick = Ast.Variable(typeof(long), "$tick");
                return new DelayedProfiling(this, body, tick);
            }
            return body;
        }

        #endregion
    }
}
