// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

using MSAst = System.Linq.Expressions;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    /// <summary>
    /// Top-level ast for all Python code.  Typically represents a module but could also
    /// be exec or eval code.
    /// </summary>
    public sealed class PythonAst : ScopeStatement {
        private Statement _body;
        private CompilationMode _mode;
        private readonly bool _printExpressions;
        private ModuleOptions _languageFeatures;
        private readonly CompilerContext _compilerContext;
        private readonly MSAst.SymbolDocumentInfo _document;
        private readonly string/*!*/ _name;
        internal int[] _lineLocations;
        private ModuleContext _modContext;
        private readonly bool _onDiskProxy;
        internal MSAst.Expression _arrayExpression;
        private CompilationMode.ConstantInfo _contextInfo;
        private Dictionary<PythonVariable, MSAst.Expression> _globalVariables = new Dictionary<PythonVariable, MSAst.Expression>();
        internal readonly Profiler _profiler;                            // captures timing data if profiling

        internal const string GlobalContextName = "$globalContext";
        internal static MSAst.ParameterExpression _functionCode = Ast.Variable(typeof(FunctionCode), "$functionCode");
        internal static readonly MSAst.ParameterExpression/*!*/ _globalArray = Ast.Parameter(typeof(PythonGlobal[]), "$globalArray");
        internal static readonly MSAst.ParameterExpression/*!*/ _globalContext = Ast.Parameter(typeof(CodeContext), GlobalContextName);
        internal static readonly ReadOnlyCollection<MSAst.ParameterExpression> _arrayFuncParams = new ReadOnlyCollectionBuilder<MSAst.ParameterExpression>(new[] { _globalContext, _functionCode }).ToReadOnlyCollection();

        public PythonAst(Statement body, bool isModule, ModuleOptions languageFeatures, bool printExpressions) {
            ContractUtils.RequiresNotNull(body, nameof(body));

            _body = body;
            IsModule = isModule;
            _printExpressions = printExpressions;
            _languageFeatures = languageFeatures;
        }

        public PythonAst(Statement body, bool isModule, ModuleOptions languageFeatures, bool printExpressions, CompilerContext context, int[] lineLocations) :
            this(isModule, languageFeatures, printExpressions, context) {

            ContractUtils.RequiresNotNull(body, nameof(body));

            _body = body;
            
            _lineLocations = lineLocations;
        }

        /// <summary>
        /// Creates a new PythonAst without a body.  ParsingFinished should be called afterwards to set
        /// the body.
        /// </summary>
        public PythonAst(bool isModule, ModuleOptions languageFeatures, bool printExpressions, CompilerContext context) {
            IsModule = isModule;
            _printExpressions = printExpressions;
            _languageFeatures = languageFeatures;
            _mode = ((PythonCompilerOptions)context.Options).CompilationMode ?? GetCompilationMode(context);
            _compilerContext = context;
            FuncCodeExpr = _functionCode;

            PythonCompilerOptions pco = context.Options as PythonCompilerOptions;
            Debug.Assert(pco != null);

            string name;
            if (!context.SourceUnit.HasPath || (pco.Module & ModuleOptions.ExecOrEvalCode) != 0) {
                name = "<module>";
            } else {
                name = context.SourceUnit.Path;
            }

            _name = name;
            Debug.Assert(_name != null);
            PythonOptions po = ((PythonContext)context.SourceUnit.LanguageContext).PythonOptions;

            if (po.EnableProfiler 
#if FEATURE_REFEMIT
                && _mode != CompilationMode.ToDisk
#endif
                ) {
                _profiler = Profiler.GetProfiler(PyContext);
            }

            _document = context.SourceUnit.Document ?? Ast.SymbolDocument(name, PyContext.LanguageGuid, PyContext.VendorGuid);
        }

        internal PythonAst(CompilerContext context)
            : this(new EmptyStatement(),
                true,
                ModuleOptions.None,
                false,
                context,
                null) {
            _onDiskProxy = true;
        }

        /// <summary>
        /// Called when parsing is complete, the body is built, the line mapping and language features are known.
        /// 
        /// This is used in conjunction with the constructor which does not take a body.  It enables creating
        /// the outer most PythonAst first so that nodes can always have a global parent.  This lets an un-bound
        /// tree to still provide it's line information immediately after parsing.  When we set the location
        /// of each node during construction we also set the global parent.  When we name bind the global 
        /// parent gets replaced with the real parent ScopeStatement.
        /// </summary>
        /// <param name="lineLocations">a mapping of where each line begins</param>
        /// <param name="body">The body of code</param>
        /// <param name="languageFeatures">The language features which were set during parsing.</param>
        public void ParsingFinished(int[] lineLocations, Statement body, ModuleOptions languageFeatures) {
            ContractUtils.RequiresNotNull(body, nameof(body));

            if (_body != null) {
                throw new InvalidOperationException("cannot set body twice");
            }

            _body = body;
            _lineLocations = lineLocations;
            _languageFeatures = languageFeatures;
        }

        /// <summary>
        /// Binds an AST and makes it capable of being reduced and compiled.  Before calling Bind an AST cannot successfully
        /// be reduced.
        /// </summary>
        public void Bind() {
            PythonNameBinder.BindAst(this, _compilerContext);
        }

        public override string Name {
            get {
                return "<module>";
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _body?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        #region Name Binding Support

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            return true;
        }

        internal override void FinishBind(PythonNameBinder binder) {
            _contextInfo = CompilationMode.GetContext();

            // create global variables for compiler context.
            PythonGlobal[] globalArray = new PythonGlobal[Variables == null ? 0 : Variables.Count];
            Dictionary<string, PythonGlobal> globals = new Dictionary<string, PythonGlobal>();
            GlobalDictionaryStorage storage = new GlobalDictionaryStorage(globals, globalArray);
            var modContext = _modContext = new ModuleContext(new PythonDictionary(storage), PyContext);

#if FEATURE_REFEMIT
            if (_mode == CompilationMode.ToDisk) {
                _arrayExpression = _globalArray;
            } else 
#endif
            {
                var newArray = new ConstantExpression(globalArray);
                newArray.Parent = this;
                _arrayExpression = newArray;
            }

            if (Variables != null) {
                int globalIndex = 0;
                foreach (PythonVariable variable in Variables.Values) {
                    PythonGlobal global = new PythonGlobal(modContext.GlobalContext, variable.Name);
                    _globalVariables[variable] = CompilationMode.GetGlobal(GetGlobalContext(), globals.Count, variable, global);
                    globalArray[globalIndex++] = globals[variable.Name] = global;
                }
            }

            CompilationMode.PublishContext(modContext.GlobalContext, _contextInfo);
        }

        internal override MSAst.Expression LocalContext {
            get {
                return GetGlobalContext();
            }
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, PythonReference reference) {
            return EnsureVariable(reference.Name);
        }

        internal override bool TryBindOuter(ScopeStatement from, PythonReference reference, out PythonVariable variable) {
            // Unbound variable
            from.AddReferencedGlobal(reference.Name);

            if (from.HasLateBoundVariableSets) {
                // If the context contains unqualified exec, new locals can be introduced
                // Therefore we need to turn this into a fully late-bound lookup which
                // happens when we don't have a PythonVariable.
                variable = null;
                return false;
            } else {
                // Create a global variable to bind to.
                variable = EnsureGlobalVariable(reference.Name);
                return true;
            }
        }

        internal override bool IsGlobal {
            get { return true; }
        }

        internal PythonVariable DocVariable { get; set; }

        internal PythonVariable NameVariable { get; set; }

        internal PythonVariable FileVariable { get; set; }

        internal CompilerContext CompilerContext {
            get {
                return _compilerContext;
            }
        }

        internal MSAst.Expression GlobalArrayInstance {
            get {
                return _arrayExpression;
            }
        }

        internal MSAst.SymbolDocumentInfo Document {
            get {
                return _document;
            }
        }

        internal Dictionary<PythonVariable, MSAst.Expression> ModuleVariables {
            get {
                return _globalVariables;
            }
        }

        internal ModuleContext ModuleContext {
            get {
                return _modContext;
            }
        }

        /// <summary>
        /// Creates a variable at the global level.  Called for known globals (e.g. __name__),
        /// for variables explicitly declared global by the user, and names accessed
        /// but not defined in the lexical scope.
        /// </summary>
        internal PythonVariable/*!*/ EnsureGlobalVariable(string name) {
            PythonVariable variable;
            if (TryGetVariable(name, out variable)) {
                variable.LiftToGlobal();
            } else {
                variable = CreateVariable(name, VariableKind.Global);
            }

            return variable;
        }

        internal override MSAst.Expression GetVariableExpression(PythonVariable variable) {
            Debug.Assert(_globalVariables.ContainsKey(variable));
            return _globalVariables[variable];
        }

        #endregion

        #region MSASt.Expression Overrides

        public override Type Type {
            get {
                return CompilationMode.DelegateType;
            }
        }

        /// <summary>
        /// Reduces the PythonAst to a LambdaExpression of type Type.
        /// </summary>
        public override MSAst.Expression Reduce() {
            return GetLambda();
        }

        internal override Microsoft.Scripting.Ast.LightLambdaExpression GetLambda() {
            string name = ((PythonCompilerOptions)_compilerContext.Options).ModuleName ?? "<unnamed>";

            return CompilationMode.ReduceAst(this, name);
        }

        #endregion

        #region Public API

        public Statement Body {
            get { return _body; }
        }

        public bool IsModule { get; }

        #endregion

        #region Transformation

        /// <summary>
        /// Returns a ScriptCode object for this PythonAst.  The ScriptCode object
        /// can then be used to execute the code against it's closed over scope or
        /// to execute it against a different scope.
        /// </summary>
        internal ScriptCode ToScriptCode() {
            return CompilationMode.MakeScriptCode(this);
        }

        internal MSAst.Expression ReduceWorker() {
            if (_body is ReturnStatement retStmt &&
                (_languageFeatures == ModuleOptions.None ||
                _languageFeatures == (ModuleOptions.ExecOrEvalCode | ModuleOptions.Interpret) ||
                _languageFeatures == (ModuleOptions.ExecOrEvalCode | ModuleOptions.Interpret | ModuleOptions.LightThrow))) {
                // for simple eval's we can construct a simple tree which just
                // leaves the value on the stack.  Return's can't exist in modules
                // so this is always safe.
                Debug.Assert(!IsModule);

                var ret = (ReturnStatement)_body;
                Ast simpleBody;
                if ((_languageFeatures & ModuleOptions.LightThrow) != 0) {
                    simpleBody = LightExceptions.Rewrite(retStmt.Expression.Reduce());
                } else {
                    simpleBody = retStmt.Expression.Reduce();
                }

                var start = IndexToLocation(ret.Expression.StartIndex);
                var end = IndexToLocation(ret.Expression.EndIndex);

                return Ast.Block(
                    Ast.DebugInfo(
                        _document,
                        start.Line,
                        start.Column,
                        end.Line,
                        end.Column
                    ),
                    AstUtils.Convert(simpleBody, typeof(object))
                );
            }

            ReadOnlyCollectionBuilder<MSAst.Expression> block = new ReadOnlyCollectionBuilder<MSAst.Expression>();
            AddInitialiation(block);

            string doc = GetDocumentation(_body);
            if (doc != null || IsModule) {
                block.Add(AssignValue(GetVariableExpression(DocVariable), Ast.Constant(doc)));
            }

            if (!(_body is SuiteStatement) && _body.CanThrow) {
                // we only initialize line numbers in suite statements but if we don't generate a SuiteStatement
                // at the top level we can miss some line number updates.  
                block.Add(UpdateLineNumber(_body.Start.Line));
            }

            block.Add(_body);

            MSAst.Expression body = Ast.Block(block.ToReadOnlyCollection());
            
            body = WrapScopeStatements(body, Body.CanThrow);   // new ComboActionRewriter().VisitNode(Transform(ag))

            body = AddModulePublishing(body);

            body = AddProfiling(body);

            if ((((PythonCompilerOptions)_compilerContext.Options).Module & ModuleOptions.LightThrow) != 0) {
                body = LightExceptions.Rewrite(body);
            }

            body = Ast.Label(FunctionDefinition._returnLabel, AstUtils.Convert(body, typeof(object)));
            if (body.Type == typeof(void)) {
                body = Ast.Block(body, Ast.Constant(null));
            }

            return body;
        }

        private void AddInitialiation(ReadOnlyCollectionBuilder<MSAst.Expression> block) {
            if (IsModule) {
                block.Add(AssignValue(GetVariableExpression(FileVariable), Ast.Constant(ModuleFileName)));
                block.Add(AssignValue(GetVariableExpression(NameVariable), Ast.Constant(ModuleName)));
            }

            if (_languageFeatures != ModuleOptions.None || IsModule) {
                block.Add(
                    Ast.Call(
                        AstMethods.ModuleStarted,
                        LocalContext,
                        AstUtils.Constant(_languageFeatures)
                    )
                );
            }
        }

        internal override bool PrintExpressions {
            get {
                return _printExpressions;
            }
        }

        private MSAst.Expression AddModulePublishing(MSAst.Expression body) {
            if (IsModule) {
                PythonCompilerOptions pco = _compilerContext.Options as PythonCompilerOptions;

                string moduleName = ModuleName;

                if ((pco.Module & ModuleOptions.Initialize) != 0) {
                    var tmp = Ast.Variable(typeof(object), "$originalModule");
                    // TODO: Should be try/fault

                    body = Ast.Block(
                        new[] { tmp },
                        AstUtils.Try(
                            Ast.Assign(tmp, Ast.Call(AstMethods.PublishModule, LocalContext, Ast.Constant(moduleName))),
                            body
                        ).Catch(
                            typeof(Exception),
                            Ast.Call(AstMethods.RemoveModule, LocalContext, Ast.Constant(moduleName), tmp),
                            Ast.Rethrow(body.Type)
                        )
                    );
                }
            }
            return body;
        }

        private string ModuleFileName {
            get {
                return _name;
            }
        }

        private string ModuleName {
            get {
                PythonCompilerOptions pco = _compilerContext.Options as PythonCompilerOptions;
                string moduleName = pco.ModuleName;
                if (moduleName == null) {
                    if (_compilerContext.SourceUnit.HasPath && _compilerContext.SourceUnit.Path.IndexOfAny(Path.GetInvalidFileNameChars()) == -1) {
                        moduleName = Path.GetFileNameWithoutExtension(_compilerContext.SourceUnit.Path);
                    } else {
                        moduleName = "<module>";
                    }
                }
                return moduleName;
            }
        }

        internal override FunctionAttributes Flags {
            get {
                return FunctionAttributes.None;
            }
        }

        internal SourceUnit SourceUnit {
            get {
                return _compilerContext?.SourceUnit;
            }
        }

        internal string[] GetNames() {
            string[] res = new string[Variables.Count];
            int i = 0;
            foreach (var variable in Variables.Values) {
                res[i++] = variable.Name;
            }

            return res;
        }

        #endregion

        #region Compilation Mode (TODO: Factor out)

        private static Compiler.CompilationMode GetCompilationMode(CompilerContext context) {
            PythonCompilerOptions options = (PythonCompilerOptions)context.Options;

            if ((options.Module & ModuleOptions.ExecOrEvalCode) != 0) {
                return CompilationMode.Lookup;
            }

#if FEATURE_REFEMIT
            PythonContext pc = ((PythonContext)context.SourceUnit.LanguageContext);
            return ((pc.PythonOptions.Optimize || options.Optimized) && !pc.PythonOptions.LightweightScopes) ?
                CompilationMode.Uncollectable :
                CompilationMode.Collectable;
#else
            return CompilationMode.Collectable;
#endif
        }

        internal CompilationMode CompilationMode {
            get {
                return _mode;
            }
        }

        private MSAst.Expression GetGlobalContext() {
            if (_contextInfo != null) {
                return _contextInfo.Expression;
            }

            return _globalContext;
        }

        internal void PrepareScope(ReadOnlyCollectionBuilder<MSAst.ParameterExpression> locals, List<MSAst.Expression> init) {
            CompilationMode.PrepareScope(this, locals, init);
        }

        internal new MSAst.Expression Constant(object value) {
            return new PythonConstantExpression(CompilationMode, value);
        }
        
        #endregion

        #region Binder Factories

        internal MSAst.Expression/*!*/ Convert(Type/*!*/ type, ConversionResultKind resultKind, MSAst.Expression/*!*/ target) {
            if (resultKind == ConversionResultKind.ExplicitCast) {
                return new DynamicConvertExpression(
                    PyContext.Convert(
                        type,
                        resultKind
                    ),
                    CompilationMode,
                    target
                );
            }

            return CompilationMode.Dynamic(
                PyContext.Convert(
                    type,
                    resultKind
                ),
                type,
                target
            );
        }

        
        internal MSAst.Expression/*!*/ Operation(Type/*!*/ resultType, PythonOperationKind operation, MSAst.Expression arg0) {
            if (resultType == typeof(object)) {
                return new PythonDynamicExpression1(
                    Binders.UnaryOperationBinder(
                        PyContext,
                        operation
                    ),
                    CompilationMode,
                    arg0
                );
            }
           
            return CompilationMode.Dynamic(
                Binders.UnaryOperationBinder(
                    PyContext,
                    operation
                ),
                resultType,
                arg0
            );
        }

        internal MSAst.Expression/*!*/ Operation(Type/*!*/ resultType, PythonOperationKind operation, MSAst.Expression arg0, MSAst.Expression arg1) {
            if (resultType == typeof(object)) {
                return new PythonDynamicExpression2(
                    Binders.BinaryOperationBinder(
                        PyContext,
                        operation
                    ),
                    _mode,
                    arg0,
                    arg1
                );
            }
            return CompilationMode.Dynamic(
                Binders.BinaryOperationBinder(
                    PyContext,
                    operation
                ),
                resultType,
                arg0,
                arg1
            );
        }

        internal MSAst.Expression/*!*/ Set(string/*!*/ name, MSAst.Expression/*!*/ target, MSAst.Expression/*!*/ value) {
            return new PythonDynamicExpression2(
                PyContext.SetMember(
                    name
                ),
                CompilationMode,
                target,
                value
            );
        }

        internal MSAst.Expression/*!*/ Get(string/*!*/ name, MSAst.Expression/*!*/ target) {
            return new DynamicGetMemberExpression(PyContext.GetMember(name), _mode, target, LocalContext);
        }

        
        internal MSAst.Expression/*!*/ Delete(Type/*!*/ resultType, string/*!*/ name, MSAst.Expression/*!*/ target) {
            return CompilationMode.Dynamic(
                PyContext.DeleteMember(
                    name
                ),
                resultType,
                target
            );
        }


        internal MSAst.Expression/*!*/ GetIndex(MSAst.Expression/*!*/[]/*!*/ expressions) {
            return new PythonDynamicExpressionN(
                PyContext.GetIndex(
                    expressions.Length
                ),
                CompilationMode,
                expressions
            );
        }

        internal MSAst.Expression/*!*/ GetSlice(MSAst.Expression/*!*/[]/*!*/ expressions) {
            return new PythonDynamicExpressionN(
                PyContext.GetSlice,
                CompilationMode,
                expressions
            );
        }

        internal MSAst.Expression/*!*/ SetIndex(MSAst.Expression/*!*/[]/*!*/ expressions) {
            return new PythonDynamicExpressionN(
                PyContext.SetIndex(
                    expressions.Length - 1
                ),
                CompilationMode,
                expressions
            );           
        }

        internal MSAst.Expression/*!*/ SetSlice(MSAst.Expression/*!*/[]/*!*/ expressions) {
            return new PythonDynamicExpressionN(
                PyContext.SetSliceBinder,
                CompilationMode,
                expressions
            );            
        }

        internal MSAst.Expression/*!*/ DeleteIndex(MSAst.Expression/*!*/[]/*!*/ expressions) {           
            return CompilationMode.Dynamic(
                PyContext.DeleteIndex(
                    expressions.Length
                ),
                typeof(void),
                expressions
            );
        }

        internal MSAst.Expression/*!*/ DeleteSlice(MSAst.Expression/*!*/[]/*!*/ expressions) {
            return new PythonDynamicExpressionN(
                PyContext.DeleteSlice,
                CompilationMode,
                expressions
            );            
        }

        #endregion

        #region Lookup Rewriting

        /// <summary>
        /// Rewrites the tree for performing lookups against globals instead of being bound
        /// against the optimized scope.  This is used if the user compiles optimized code and then
        /// runs it against a different scope.
        /// </summary>
        internal PythonAst MakeLookupCode() {
            PythonAst res = (PythonAst)MemberwiseClone();
            res._mode = CompilationMode.Lookup;
            res._contextInfo = null;

            // update the top-level globals for class/funcs accessing __name__, __file__, etc...
            Dictionary<PythonVariable, MSAst.Expression> newGlobals = new Dictionary<PythonVariable, MSAst.Expression>();
            foreach (var v in _globalVariables) {
                newGlobals[v.Key] = CompilationMode.Lookup.GetGlobal(_globalContext, -1, v.Key, null);
            }
            res._globalVariables = newGlobals;

            res._body = new RewrittenBodyStatement(_body, new LookupVisitor(res, GetGlobalContext()).Visit(_body));
            return res;
        }

        internal class LookupVisitor : MSAst.ExpressionVisitor {
            private readonly MSAst.Expression _globalContext;
            private readonly Dictionary<MSAst.Expression, ScopeStatement> _outerComprehensionScopes = new();
            private ScopeStatement _curScope;

            public LookupVisitor(PythonAst ast, MSAst.Expression globalContext) {
                _globalContext = globalContext;
                _curScope = ast;
            }

            protected override MSAst.Expression VisitMember(MSAst.MemberExpression node) {
                if (node == _globalContext) {
                    return PythonAst._globalContext;
                }
                return base.VisitMember(node);
            }

            protected override MSAst.Expression VisitExtension(MSAst.Expression node) {
                if (node == _globalContext) {
                    return PythonAst._globalContext;
                }

                // outer comprehension iterable is visited in outer comprehension scope
                if (_outerComprehensionScopes.TryGetValue(node, out ScopeStatement outerComprehensionScope)) {
                    _outerComprehensionScopes.Remove(node);
                    return VisitComprehensionIterable(node, outerComprehensionScope);
                }

                // we need to re-write nested scopes
                if (node is ScopeStatement scope) {
                    return base.VisitExtension(VisitScope(scope));
                }

                if (node is LambdaExpression lambda) {
                    return base.VisitExtension(new LambdaExpression((FunctionDefinition)VisitScope(lambda.Function)));
                }

                if (node is GeneratorExpression generator) {
                    return base.VisitExtension(new GeneratorExpression((FunctionDefinition)VisitScope(generator.Function), generator.Iterable));
                }

                if (node is Comprehension comprehension) {
                    return VisitComprehension(comprehension);
                }

                // update the global get/set/raw gets variables
                if (node is PythonGlobalVariableExpression global) {
                    return new LookupGlobalVariable(
                        _curScope == null ? PythonAst._globalContext : _curScope.LocalContext,
                        global.Variable.Name,
                        global.Variable.Kind == VariableKind.Local
                    );
                }

                // set covers sets and deletes
                if (node is PythonSetGlobalVariableExpression setGlobal) {
                    if (setGlobal.Value == PythonGlobalVariableExpression.Uninitialized) {
                        return new LookupGlobalVariable(
                            _curScope == null ? PythonAst._globalContext : _curScope.LocalContext,
                            setGlobal.Global.Variable.Name,
                            setGlobal.Global.Variable.Kind == VariableKind.Local
                        ).Delete();
                    } else {
                        return new LookupGlobalVariable(
                            _curScope == null ? PythonAst._globalContext : _curScope.LocalContext,
                            setGlobal.Global.Variable.Name,
                            setGlobal.Global.Variable.Kind == VariableKind.Local
                        ).Assign(Visit(setGlobal.Value));
                    }
                }

                if (node is PythonRawGlobalValueExpression rawValue) {
                    return new LookupGlobalVariable(
                        _curScope == null ? PythonAst._globalContext : _curScope.LocalContext,
                        rawValue.Global.Variable.Name,
                        rawValue.Global.Variable.Kind == VariableKind.Local
                    );
                }

                return base.VisitExtension(node);
            }

            private ScopeStatement VisitScope(ScopeStatement scope) {
                var newScope = scope.CopyForRewrite();
                ScopeStatement prevScope = _curScope;
                try {
                    // rewrite the method body
                    _curScope = newScope;
                    newScope.Parent = prevScope;

                    newScope.RewriteBody(this);
                } finally {
                    _curScope = prevScope;
                }
                return newScope;
            }

            private MSAst.Expression VisitComprehension(Comprehension comprehension) {
                var newScope = (ComprehensionScope)comprehension.Scope.CopyForRewrite();
                newScope.Parent = _curScope;
                var newComprehension = comprehension.CopyForRewrite(newScope);

                ScopeStatement prevScope = _curScope;
                try {
                    // mark the first (outermost) "for" iterator for rewrite in the current scope
                    _outerComprehensionScopes[((ComprehensionFor)comprehension.Iterators[0]).List] = _curScope;

                    // rewrite the rest of comprehension in the new scope
                    _curScope = newScope;

                    return base.VisitExtension(newComprehension);
                } finally {
                    _curScope = prevScope;
                }
            }

            private MSAst.Expression VisitComprehensionIterable(MSAst.Expression node, ScopeStatement scope) {
                ScopeStatement prevScope = _curScope;
                try {
                    _curScope = scope;
                    return VisitExtension(node); // no base.VisitExtension
                } finally {
                    _curScope = prevScope;
                }
            }
        }

        #endregion

        internal override string ProfilerName {
            get {
                if (_mode == CompilationMode.Lookup) {
                    return NameForExec;
                }
                if (_name.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0) {
                    return "module " + _name;
                } else {
                    return "module " + System.IO.Path.GetFileNameWithoutExtension(_name);
                }
            }
        }

        internal new bool EmitDebugSymbols {
            get {
                return PyContext.EmitDebugSymbols(SourceUnit);
            }
        }

        /// <summary>
        /// True if this is on-disk code which we don't really have an AST for.
        /// </summary>
        internal bool OnDiskProxy {
            get {
                return _onDiskProxy;
            }
        }
    }
}


