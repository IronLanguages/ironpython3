// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using IronPython.Runtime;
using IronPython.Runtime.Binding;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    public abstract class ScopeStatement : Statement {
        private bool _forceCompile;                 // true if this scope should always be compiled

        private FunctionCode? _funcCode;            // the function code object created for this scope

        private ClosureInfo[]? _closureVariables;                       // closed over variables, bool indicates if we accessed it in this scope.
        private List<PythonVariable>? _freeVars;                        // list of variables accessed from outer scopes
        private List<string>? _globalVars;                              // global variables accessed from this scope
        private List<string>? _cellVars;                                // variables accessed from nested scopes
        private Dictionary<string, PythonReference>? _references;       // names of all variables referenced, null after binding completes
        private Dictionary<string, NonlocalStatement>? _nonlocalVars;   // nonlocal variables declared in this scope, null after binding completes (except for classes)

        internal Dictionary<PythonVariable, MSAst.Expression> _variableMapping = new Dictionary<PythonVariable, MSAst.Expression>();
        private readonly DelayedFunctionCode _funcCodeExpr = new DelayedFunctionCode();             // expression that refers to the function code for this scope

        internal static MSAst.ParameterExpression LocalCodeContextVariable = Ast.Parameter(typeof(CodeContext), "$localContext");
        private static MSAst.ParameterExpression _catchException = Ast.Parameter(typeof(Exception), "$updException");
        internal const string NameForExec = "module: <exec>";

        /// <summary>
        /// from module import *
        /// </summary>
        internal bool ContainsImportStar { get; set; }

        /// <summary>
        /// True if this block contains a try/with statement.
        /// </summary>
        internal bool ContainsExceptionHandling { get; set; }

        internal virtual bool IsGeneratorMethod => false;

        /// <summary>
        /// The variable used to hold out parents closure tuple in our local scope.
        /// </summary>
        internal MSAst.ParameterExpression? LocalParentTuple { get; private set; }

        /// <summary>
        /// Gets the expression associated with the local CodeContext.  If the function
        /// doesn't have a local CodeContext then this is the global context.
        /// </summary>
        internal virtual MSAst.Expression LocalContext => LocalCodeContextVariable;

        /// <summary>
        /// True if this scope accesses a variable from an outer scope.
        /// </summary>
        [MemberNotNullWhen(true, nameof(FreeVariables))]
        internal bool IsClosure => FreeVariables is not null && FreeVariables.Count > 0;

        /// <summary>
        /// True if an inner scope is accessing a non-global variable defined in this or an outer scope.
        /// </summary>
        internal bool ContainsNestedFreeVariables { get; set; }

        /// <summary>
        /// True if we are forcing the creation of a dictionary for storing locals.
        /// 
        /// This occurs for calls to locals(), dir(), vars(), unqualified exec, and
        /// from ... import *.
        /// </summary>
        internal bool NeedsLocalsDictionary { get; set; }

        /// <summary>
        /// True if this scope contains a parameterless call to super().
        ///
        /// It is used to ensure that the first argument is accessible through a closure cell.
        /// </summary>
        internal bool ContainsSuperCall{ get; set; }

        public virtual string Name => "<unknown scope>";

        internal virtual string Filename => GlobalParent.SourceUnit.Path ?? "<string>";

        /// <summary>
        /// True if variables can be set in a late bound fashion that we don't
        /// know about at code gen time - for example via from foo import *.
        /// 
        /// This is tracked independently of NeedsLocalsDictionary
        /// </summary>
        internal virtual bool HasLateBoundVariableSets { get; set; }

        /// <summary>
        /// mapping of string to variables
        /// </summary>
        internal Dictionary<string, PythonVariable>? Variables { get; private set; }

        internal virtual bool IsGlobal => false;

        internal bool NeedsLocalContext
            => NeedsLocalsDictionary || ContainsNestedFreeVariables || ContainsSuperCall;

        internal virtual string[] ParameterNames => ArrayUtils.EmptyStrings;

        internal virtual int ArgCount => 0;

        internal virtual int KwOnlyArgCount => 0;

        internal virtual FunctionAttributes Flags => FunctionAttributes.None;

        internal abstract Microsoft.Scripting.Ast.LightLambdaExpression GetLambda();

        /// <summary>
        /// Gets or creates the FunctionCode object for this FunctionDefinition.
        /// </summary>
        internal FunctionCode GetOrMakeFunctionCode() {
            if (_funcCode is null) {
                Interlocked.CompareExchange(ref _funcCode, new FunctionCode(GlobalParent.PyContext, OriginalDelegate, this, ScopeDocumentation, null, true), null);
            }
            return _funcCode;
        }

        internal virtual string? ScopeDocumentation => null;

        internal virtual Delegate? OriginalDelegate => null;

        internal virtual IList<string> GetVarNames() {
            var res = new List<string>();

            AppendVariables(res);

            return res;
        }


        internal virtual void AddFreeVariable(PythonVariable variable, bool accessedInScope) {
            Debug.Assert(variable.Kind is VariableKind.Local or VariableKind.Parameter);

            _freeVars ??= new List<PythonVariable>();

            if (!_freeVars.Contains(variable)) {
                _freeVars.Add(variable);
                if (TryGetVariable(variable.Name, out PythonVariable? nonlocal) &&
                    nonlocal.Kind is VariableKind.Nonlocal &&
                    nonlocal.MaybeDeleted) {

                    variable.RegisterDeletion();
                }
            }
        }

        internal bool ShouldInterpret {
            get {
                if (_forceCompile) {
                    return false;
                }
                if (GlobalParent.CompilationMode == CompilationMode.Lookup) {
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
            _globalVars ??= new List<string>();

            if (!_globalVars.Contains(name)) {
                _globalVars.Add(name);
            }
            return name;
        }

        internal void AddCellVariable(PythonVariable variable) {
            _cellVars ??= new List<string>();

            if (!_cellVars.Contains(variable.Name)) {
                _cellVars.Add(variable.Name);
            }
        }

        internal List<string> AppendVariables(List<string> res) {
            if (Variables is not null) {
                foreach (var variable in Variables) {
                    if (variable.Value.Kind != VariableKind.Local) {
                        continue;
                    }

                    if (CellVariables is null || !CellVariables.Contains(variable.Key)) {
                        res.Add(variable.Key);
                    }
                }
            }
            return res;
        }

        /// <summary>
        /// Variables that are bound in an outer scope - but not a global scope
        /// </summary>
        internal IList<PythonVariable>? FreeVariables => _freeVars;

        /// <summary>
        /// Variables that are bound to the global scope
        /// </summary>
        internal IList<string>? GlobalVariables => _globalVars;

        /// <summary>
        /// Variables that are referred to from a nested scope and need to be
        /// promoted to cells.
        /// </summary>
        internal IList<string>? CellVariables => _cellVars;

        internal Type? GetClosureTupleType() {
            if (NumTupleCells > 0) {
                Type[] args = new Type[NumTupleCells];
                for (int i = 0; i < NumTupleCells; i++) {
                    args[i] = typeof(ClosureCell);
                }
                return MutableTuple.MakeTupleType(args);
            }
            return null;
        }

        internal virtual int NumTupleCells
            => _closureVariables is null ? 0 : _closureVariables.Length;

        internal abstract bool ExposesLocalVariable(PythonVariable variable);

        internal virtual MSAst.Expression GetParentClosureTuple() {
            // PythonAst will never call this.
            throw new NotSupportedException();
        }

        internal bool TryGetVariable(string name, [NotNullWhen(true)] out PythonVariable? variable) {
            if (Variables is not null) {
                return Variables.TryGetValue(name, out variable);
            } else {
                variable = null;
                return false;
            }
        }

        internal virtual bool TryBindOuter(ScopeStatement from, PythonReference reference, [NotNullWhen(true)] out PythonVariable? variable) {
            // Hide scope contents by default
            variable = null;
            return false;
        }

        private protected bool TryBindOuterScopes(ScopeStatement from, PythonReference reference, [NotNullWhen(true)] out PythonVariable? variable, bool stopAtGlobal) {
            for (ScopeStatement? parent = from.Parent; parent != null && !(stopAtGlobal && parent.IsGlobal); parent = !parent.IsGlobal ? parent.Parent : null) {
                if (parent.TryBindOuter(from, reference, out variable)) {
                    return true;
                }
            }
            variable = null;
            return false;
        }

        internal abstract PythonVariable? BindReference(PythonNameBinder binder, PythonReference reference);

        internal virtual void Bind(PythonNameBinder binder) {
            if (_references is not null) {
                foreach (var reference in _references.Values) {
                    reference.PythonVariable = BindReference(binder, reference);
                    if ((reference.PythonVariable is null || reference.PythonVariable.Kind is VariableKind.Global) && TryGetNonlocalStatement(reference.Name, out NonlocalStatement? node)) {
                        binder.ReportSyntaxError($"no binding for nonlocal '{reference.Name}' found", node);
                    }
                }
            }
        }

        internal virtual void FinishBind(PythonNameBinder binder) {
            List<ClosureInfo>? closureVariables = null;

            if (IsClosure) {
                Debug.Assert(FreeVariables.Count <= Parent.NumTupleCells);

                Type tupleType = Parent.GetClosureTupleType()!; // not null because Parent.NumTupleCells > 0
                Assert.NotNull(tupleType);

                LocalParentTuple = Ast.Parameter(tupleType, "$tuple");

                ClosureInfo[] parentClosure = Parent._closureVariables!; // not null because Parent.NumTupleCells > 0
                Assert.NotNull(parentClosure);

                foreach (var variable in FreeVariables) {
                    Debug.Assert(!HasClosureVariable(closureVariables, variable));
                    for (int i = 0; i < parentClosure.Length; i++) {
                        if (parentClosure[i].Variable == variable) {
                            Ast prop = LocalParentTuple;
                            foreach (PropertyInfo pi in MutableTuple.GetAccessPath(tupleType, parentClosure.Length, i)) {
                                prop = Ast.Property(prop, pi);
                            }
                            _variableMapping[variable] = new ClosureExpression(variable, prop, null);
                            break;
                        }
                    }
                    Debug.Assert(_variableMapping.ContainsKey(variable));

                    closureVariables ??= new List<ClosureInfo>();
                    closureVariables.Add(new ClosureInfo(variable, this is not ClassDefinition));
                }
            }

            if (Variables != null) {
                foreach (PythonVariable variable in Variables.Values) {
                    if (variable.Kind is VariableKind.Local or VariableKind.Parameter &&
                        (variable.AccessedInNestedScope || ExposesLocalVariable(variable))) {

                        Debug.Assert(!HasClosureVariable(closureVariables, variable));

                        closureVariables ??= new List<ClosureInfo>();
                        closureVariables.Add(new ClosureInfo(variable, true));
                    }

                    if (variable.Kind == VariableKind.Local) {
                        Debug.Assert(variable.Scope == this);

                        if (variable.AccessedInNestedScope || ExposesLocalVariable(variable)) {
                            _variableMapping[variable] = new ClosureExpression(variable, Ast.Parameter(typeof(ClosureCell), variable.Name), null);
                        } else {
                            _variableMapping[variable] = Ast.Parameter(typeof(object), variable.Name);
                        }
                    } else if (variable.Kind == VariableKind.Attribute) {
                        // If no user-supplied dictionary is in place, a more efficient access is possible, see CollectableCompilationMode
                        // However, this would probably have a negligible effect in this case.
                        _variableMapping[variable] = new LookupGlobalVariable(LocalContext, variable.Name, isLocal: true);
                    }
                }
            }

            if (closureVariables != null) {
                _closureVariables = closureVariables.ToArray();
            }

            // no longer needed
            _references = null;
            if (this is not ClassDefinition) {
                // ClassDefinition still checks for nonlocals during reduce
                _nonlocalVars = null;
            }
        }

        private static bool HasClosureVariable(List<ClosureInfo>? closureVariables, PythonVariable variable) {
            if (closureVariables is null) {
                return false;
            }

            for (int i = 0; i < closureVariables.Count; i++) {
                if (closureVariables[i].Variable == variable) {
                    return true;
                }
            }

            return false;
        }

        [MemberNotNull(nameof(Variables))]
        private void EnsureVariables() {
            Variables ??= new Dictionary<string, PythonVariable>(StringComparer.Ordinal);
        }

        internal void AddGlobalVariable(PythonVariable variable) {
            EnsureVariables();
            Variables[variable.Name] = variable;
        }

        internal PythonReference Reference(string name) {
            _references ??= new Dictionary<string, PythonReference>(StringComparer.Ordinal);
            if (!_references.TryGetValue(name, out PythonReference? reference)) {
                _references[name] = reference = new PythonReference(name);
                if (name == "super" && this is not ClassDefinition) {
                    Reference("__class__");
                }
            }
            return reference;
        }

        internal bool IsReferenced(string name) {
            return _references?.TryGetValue(name, out _) ?? false;
        }

        internal bool IsFreeVariable(PythonVariable? variable) {
            PythonVariable? limitVariable = variable?.LimitVariable;
            if (limitVariable is null || FreeVariables is null) {
                return false;
            }
            return FreeVariables.Contains(limitVariable);
        }

        private protected bool TryGetNonlocalStatement(string name, [NotNullWhen(true)] out NonlocalStatement? node) {
            node = null;
            return _nonlocalVars?.TryGetValue(name, out node) ?? false;
        }

        internal PythonVariable/*!*/ CreateVariable(string name, VariableKind kind, string? key = null) {
            EnsureVariables();
            Debug.Assert(!Variables.ContainsKey(key ?? name));
            PythonVariable variable;
            Variables[key ?? name] = variable = new PythonVariable(name, kind, this);
            return variable;
        }

        internal virtual PythonVariable/*!*/ EnsureVariable(string name) {
            if (!TryGetVariable(name, out PythonVariable? variable)) {
                return CreateVariable(name, VariableKind.Local);
            }
            return variable;
        }

        private PythonVariable CreateNonlocalVariable(string name) {
            EnsureVariables();
            Debug.Assert(!Variables.ContainsKey(name));
            Debug.Assert(!IsReferenced(name));
            PythonReference reference = Reference(name);
            PythonVariable variable;
            Variables[name] = variable = new PythonReferenceVariable(reference, this);
            return variable;
        }

        internal void EnsureNonlocalVariable(string name, NonlocalStatement node) {
           _nonlocalVars ??= new();
            if (!_nonlocalVars.ContainsKey(name)) {
                CreateNonlocalVariable(name);
                _nonlocalVars[name] = node;
            }
        }

        internal PythonVariable DefineParameter(string name) {
            return CreateVariable(name, VariableKind.Parameter);
        }

        internal PythonContext PyContext
            => (PythonContext)GlobalParent.CompilerContext.SourceUnit.LanguageContext;

        #region Debug Info Tracking

        private MSAst.SymbolDocumentInfo Document => GlobalParent.Document;

        internal MSAst.Expression/*!*/ AddDebugInfo(MSAst.Expression/*!*/ expression, SourceLocation start, SourceLocation end) {
            if (PyContext.PythonOptions.GCStress is not null) {
                expression = Ast.Block(
                    Ast.Call(
                        typeof(GC).GetMethod(nameof(GC.Collect), new[] { typeof(int) })!,
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
            if (!ContainsExceptionHandling) {
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
        private class DelayedFunctionCode : MSAst.Expression {
            private MSAst.Expression? _funcCode;

            public override bool CanReduce => true;

            public MSAst.Expression? Code {
                get {
                    return _funcCode;
                }
                set {
                    _funcCode = value;
                }
            }

            public override Type Type => typeof(FunctionCode);

            protected override MSAst.Expression VisitChildren(MSAst.ExpressionVisitor visitor) {
                if (_funcCode is not null) {
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
                return _funcCode!;
            }

            public override MSAst.ExpressionType NodeType => MSAst.ExpressionType.Extension;
        }

        internal MSAst.Expression? FuncCodeExpr {
            get {
                return _funcCodeExpr.Code;
            }
            set {
                _funcCodeExpr.Code = value;
            }
        }

        internal MSAst.MethodCallExpression CreateLocalContext(MSAst.Expression parentContext, bool newNamespace = true) {
            var closureVariables = _closureVariables ?? Array.Empty<ClosureInfo>();
            
            int numFreeVars = FreeVariables?.Count ?? 0;
            int firstArgIdx = -1;
            if ((NeedsLocalsDictionary || ContainsSuperCall) && ArgCount > 0) {
                for (int idx = numFreeVars; idx < closureVariables.Length; idx++) {
                    if (closureVariables[idx].Variable.Kind == VariableKind.Parameter) {
                        firstArgIdx = idx;
                        break;
                    }
                }
            }

            return Ast.Call(
                AstMethods.CreateLocalContext,
                parentContext,
                MutableTuple.Create(ArrayUtils.ConvertAll(closureVariables, x => GetClosureCell(x))),
                Ast.Constant(ArrayUtils.ConvertAll(closureVariables, x => x.AccessedInScope ? x.Variable.Name : null)),
                AstUtils.Constant(numFreeVars),
                AstUtils.Constant(firstArgIdx),
                AstUtils.Constant(newNamespace)
            );
        }

        private MSAst.Expression GetClosureCell(ClosureInfo variable) {
            return ((ClosureExpression)GetVariableExpression(variable.Variable)).ClosureCell;
        }

        internal virtual MSAst.Expression GetVariableExpression(PythonVariable variable) {
            Assert.NotNull(variable);
            Assert.NotNull(variable.LimitVariable);
            if (variable.Kind is VariableKind.Global) {
                return GlobalParent.ModuleVariables[variable];
            }

            Debug.Assert(_variableMapping.ContainsKey(variable.LimitVariable!));
            return _variableMapping[variable.LimitVariable!];
        }

        internal virtual MSAst.Expression LookupVariableExpression(PythonVariable variable)
            => GetVariableExpression(variable);

        internal void CreateVariables(ReadOnlyCollectionBuilder<MSAst.ParameterExpression> locals, List<MSAst.Expression> init) {
            if (Variables is not null) {
                foreach (PythonVariable variable in Variables.Values) {
                    if (variable.Kind is VariableKind.Local or VariableKind.Parameter) {
                        if (GetVariableExpression(variable) is ClosureExpression closure) {
                            init.Add(closure.Create());
                            locals.Add((MSAst.ParameterExpression)closure.ClosureCell);
                        } else if (variable.Kind == VariableKind.Local) {
                            locals.Add((MSAst.ParameterExpression)GetVariableExpression(variable));
                            if (variable.ReadBeforeInitialized) {
                                init.Add(
                                    AssignValue(
                                        GetVariableExpression(variable),
                                        MSAst.Expression.Field(null, typeof(Uninitialized).GetField(nameof(Uninitialized.Instance))!)
                                    )
                                );
                            }
                        }
                    }
                }
            }

            if (IsClosure) {
                Type tupleType = Parent.GetClosureTupleType()!; // not null if IsClosure
                Assert.NotNull(tupleType);
                Assert.NotNull(LocalParentTuple); // should be set by FinishBind

                init.Add(
                    MSAst.Expression.Assign(
                        LocalParentTuple!,
                        MSAst.Expression.Convert(
                            GetParentClosureTuple(),
                            tupleType
                        )
                    )
                );

                locals.Add(LocalParentTuple!);
            }
        }

        internal MSAst.Expression AddDecorators(MSAst.Expression ret, IList<Expression>? decorators) {
            // add decorators
            if (decorators is not null) {
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

        internal readonly struct ClosureInfo {
            public readonly PythonVariable Variable;
            public readonly bool AccessedInScope;

            public ClosureInfo(PythonVariable variable, bool accessedInScope) {
                Variable = variable;
                AccessedInScope = accessedInScope;
            }
        }

        internal virtual bool PrintExpressions => false;

        #region Profiling Support

        internal virtual string ProfilerName => Name;

        /// <summary>
        /// Reducible node so that re-writing for profiling does not occur until
        /// after the script code has been completed and is ready to be compiled.
        /// 
        /// Without this extra node profiling would force reduction of the node
        /// and we wouldn't have setup our constant access correctly yet.
        /// </summary>
        private class DelayedProfiling : MSAst.Expression {
            private readonly ScopeStatement _ast;
            private readonly MSAst.Expression _body;
            private readonly MSAst.ParameterExpression _tick;

            public DelayedProfiling(ScopeStatement ast, MSAst.Expression body, MSAst.ParameterExpression tick) {
                _ast = ast;
                _body = body;
                _tick = tick;
            }

            public override bool CanReduce => true;

            public override Type Type => _body.Type;

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

            public override MSAst.ExpressionType NodeType => MSAst.ExpressionType.Extension;
        }

        internal MSAst.Expression AddProfiling(MSAst.Expression/*!*/ body) {
            if (GlobalParent._profiler is not null) {
                MSAst.ParameterExpression tick = Ast.Variable(typeof(long), "$tick");
                return new DelayedProfiling(this, body, tick);
            }
            return body;
        }

        #endregion
    }
}
