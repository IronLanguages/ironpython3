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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

using LightLambdaExpression = Microsoft.Scripting.Ast.LightLambdaExpression;
using AstUtils = Microsoft.Scripting.Ast.Utils;

using Debugging = Microsoft.Scripting.Debugging;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class FunctionDefinition : ScopeStatement, IInstructionProvider {
        protected Statement _body;
        private readonly string _name;
        private readonly Parameter[] _parameters;
        private IList<Expression> _decorators;
        private bool _generator;                        // The function is a generator
        private bool _isLambda;

        // true if this function can set sys.exc_info(). Only functions with an except block can set that.
        private bool _canSetSysExcInfo;
        private bool _containsTryFinally;               // true if the function contains try/finally, used for generator optimization

        private PythonVariable _variable;               // The variable corresponding to the function name or null for lambdas
        internal PythonVariable _nameVariable;          // the variable that refers to the global __name__
        private LightLambdaExpression _dlrBody;       // the transformed body including all of our initialization, etc...
        internal bool _hasReturn;
        private int _headerIndex;

        private static int _lambdaId;
        internal static readonly MSAst.ParameterExpression _functionParam = Ast.Parameter(typeof(PythonFunction), "$function");
        private static readonly MSAst.Expression _GetClosureTupleFromFunctionCall = MSAst.Expression.Call(null, typeof(PythonOps).GetMethod("GetClosureTupleFromFunction"), _functionParam);
        private static readonly MSAst.Expression _parentContext = new GetParentContextFromFunctionExpression();
        internal static readonly MSAst.LabelTarget _returnLabel = MSAst.Expression.Label(typeof(object), "return");

        public FunctionDefinition(string name, Parameter[] parameters)
            : this(name, parameters, (Statement)null) {            
        }

        
        public FunctionDefinition(string name, Parameter[] parameters, Statement body) {
            ContractUtils.RequiresNotNullItems(parameters, "parameters");

            if (name == null) {
                _name = "<lambda$" + Interlocked.Increment(ref _lambdaId) + ">";
                _isLambda = true;
            } else {
                _name = name;
            }

            _parameters = parameters;
            _body = body;
        }

        [Obsolete("sourceUnit is now ignored.  FunctionDefinitions should belong to a PythonAst which has a SourceUnit")]
        public FunctionDefinition(string name, Parameter[] parameters, SourceUnit sourceUnit)
            : this(name, parameters, (Statement)null) {
        }

        [Obsolete("sourceUnit is now ignored.  FunctionDefinitions should belong to a PythonAst which has a SourceUnit")]
        public FunctionDefinition(string name, Parameter[] parameters, Statement body, SourceUnit sourceUnit)             
            : this(name, parameters, body) {
        }

        internal override MSAst.Expression LocalContext {
            get {
                if (NeedsLocalContext) {
                    return base.LocalContext;
                }

                return GlobalParent.LocalContext;
            }
        }
        public bool IsLambda {
            get {
                return _isLambda;
            }
        }

        public IList<Parameter> Parameters {
            get { return _parameters; }
        }

        internal override string[] ParameterNames {
            get {
                return ArrayUtils.ConvertAll(_parameters, val => val.Name);
            }
        }

        internal override int ArgCount {
            get {
                return _parameters.Length;
            }
        }

        public Statement Body {
            get { return _body; }
            set { _body = value; }
        }

        public SourceLocation Header {
            get { return GlobalParent.IndexToLocation(_headerIndex); }
        }

        public int HeaderIndex {
            get { return _headerIndex; }
            set { _headerIndex = value; }
        }

        public override string Name {
            get { return _name; }
        }

        public IList<Expression> Decorators {
            get { return _decorators; }
            internal set { _decorators = value; }
        }

        internal override bool IsGeneratorMethod {
            get {
                return IsGenerator;
            }
        }

        public bool IsGenerator {
            get { return _generator; }
            set { _generator = value; }
        }

        // Called by parser to mark that this function can set sys.exc_info(). 
        // An alternative technique would be to just walk the body after the parse and look for a except block.
        internal bool CanSetSysExcInfo {
            set { _canSetSysExcInfo = value; }
        }

        internal bool ContainsTryFinally {
            get { return _containsTryFinally; }
            set { _containsTryFinally = value; }
        }

        internal PythonVariable PythonVariable {
            get { return _variable; }
            set { _variable = value; }
        }

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            return NeedsLocalsDictionary; 
        }

        internal override FunctionAttributes Flags {
            get {
                FunctionAttributes fa = FunctionAttributes.None;
                if (_parameters != null) {
                    int i;
                    for (i = 0; i < _parameters.Length; i++) {
                        Parameter p = _parameters[i];
                        if (p.IsDictionary || p.IsList) break;
                    }
                    // Check for the list parameter
                    if (i < _parameters.Length && _parameters[i].IsList) {
                        i++;
                        fa |= FunctionAttributes.ArgumentList;
                    }
                    // Keyword-only arguments
                    for (; i < _parameters.Length; i++) {
                        Parameter p = _parameters[i];
                        if (p.IsDictionary) break;
                    }
                    // Check for the dictionary parameter, which must be the last
                    if (i < _parameters.Length && _parameters[i].IsDictionary) {
                        i++;
                        fa |= FunctionAttributes.KeywordDictionary;
                    }

                    // All parameters must now be exhausted
                    Debug.Assert(i == _parameters.Length);
                }

                if (_canSetSysExcInfo) {
                    fa |= FunctionAttributes.CanSetSysExcInfo;
                }

                if (ContainsTryFinally) {
                    fa |= FunctionAttributes.ContainsTryFinally;
                }

                if (IsGenerator) {
                    fa |= FunctionAttributes.Generator;
                }

                return fa;
            }
        }

        internal override bool TryBindOuter(ScopeStatement from, PythonReference reference, out PythonVariable variable) {
            // Functions expose their locals to direct access
            ContainsNestedFreeVariables = true;
            if (TryGetVariable(reference.Name, out variable)) {
                variable.AccessedInNestedScope = true;

                if (variable.Kind == VariableKind.Local || variable.Kind == VariableKind.Parameter) {
                    from.AddFreeVariable(variable, true);

                    for (ScopeStatement scope = from.Parent; scope != this; scope = scope.Parent) {
                        scope.AddFreeVariable(variable, false);
                    }

                    AddCellVariable(variable);
                } else {
                    from.AddReferencedGlobal(reference.Name);
                }
                return true;
            }
            return false;
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, PythonReference reference) {
            PythonVariable variable;

            // First try variables local to this scope
            if (TryGetVariable(reference.Name, out variable)) {
                if (variable.Kind == VariableKind.Global) {
                    AddReferencedGlobal(reference.Name);
                }
                return variable;
            }

            // Try to bind in outer scopes
            for (ScopeStatement parent = Parent; parent != null; parent = parent.Parent) {
                if (parent.TryBindOuter(this, reference, out variable)) {
                    return variable;
                }
            }

            return null;
        }


        internal override void Bind(PythonNameBinder binder) {
            base.Bind(binder);
            Verify(binder);

            if (((PythonContext)binder.Context.SourceUnit.LanguageContext).PythonOptions.FullFrames) {
                // force a dictionary if we have enabled full frames for sys._getframe support
                NeedsLocalsDictionary = true;
            }
        }
        
        internal override void FinishBind(PythonNameBinder binder) {
            foreach (var param in _parameters) {
                _variableMapping[param.PythonVariable] = param.FinishBind(NeedsLocalsDictionary);
            }
            base.FinishBind(binder);
        }

        private void Verify(PythonNameBinder binder) {
            if (ContainsImportStar && IsClosure) {
                binder.ReportSyntaxError(
                    String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "import * is not allowed in function '{0}' because it is a nested function",
                        Name),
                    this);
            }
            if (ContainsImportStar && Parent is FunctionDefinition) {
                binder.ReportSyntaxError(
                    String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "import * is not allowed in function '{0}' because it is a nested function",
                        Name),
                    this);
            }
            if (ContainsImportStar && ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                    String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "import * is not allowed in function '{0}' because it contains a nested function with free variables",
                        Name),
                    this);
            }
            if (ContainsUnqualifiedExec && ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                    String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "unqualified exec is not allowed in function '{0}' because it contains a nested function with free variables",
                        Name),
                    this);
            }
            if (ContainsUnqualifiedExec && IsClosure) {
                binder.ReportSyntaxError(
                    String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "unqualified exec is not allowed in function '{0}' because it is a nested function",
                        Name),
                    this);
            }
        }

        /// <summary>
        /// Pulls the closure tuple from our function/generator which is flowed into each function call.
        /// </summary>
        internal override MSAst.Expression/*!*/ GetParentClosureTuple() {
            return _GetClosureTupleFromFunctionCall;
        }

        public override MSAst.Expression Reduce() {
            Debug.Assert(_variable != null, "Shouldn't be called by lambda expression");

            MSAst.Expression function = MakeFunctionExpression();
            return GlobalParent.AddDebugInfoAndVoid(
                AssignValue(Parent.GetVariableExpression(_variable), function),
                new SourceSpan(GlobalParent.IndexToLocation(StartIndex), GlobalParent.IndexToLocation(HeaderIndex))
            );
        }
        
        /// <summary>
        /// Returns an expression which creates the function object.
        /// </summary>
        internal MSAst.Expression MakeFunctionExpression() {
            List<MSAst.Expression> defaults = new List<MSAst.Expression>(0);
            foreach (var param in _parameters) {
                if (param.DefaultValue != null) {
                    defaults.Add(AstUtils.Convert(param.DefaultValue, typeof(object)));
                }
            }

            MSAst.Expression funcCode = GlobalParent.Constant(GetOrMakeFunctionCode());
            FuncCodeExpr = funcCode;

            MSAst.Expression ret;
            if (EmitDebugFunction()) {
                LightLambdaExpression code = CreateFunctionLambda();

                // we need to compile all of the debuggable code together at once otherwise mdbg gets confused.  If we're
                // in tracing mode we'll still compile things one off though just to keep things simple.  The code will still
                // be debuggable but naive debuggers like mdbg will have more issues.
                ret = Ast.Call(
                    AstMethods.MakeFunctionDebug,                                                   // method
                    Parent.LocalContext,                                                            // 1. Emit CodeContext
                    FuncCodeExpr,                                                                   // 2. FunctionCode                        
                    ((IPythonGlobalExpression)GetVariableExpression(_nameVariable)).RawValue(),     // 3. module name
                    defaults.Count == 0 ?                                                           // 4. default values
                        AstUtils.Constant(null, typeof(object[])) :
                        (MSAst.Expression)Ast.NewArrayInit(typeof(object), defaults),
                    IsGenerator ?
                        (MSAst.Expression)new PythonGeneratorExpression(code, GlobalParent.PyContext.Options.CompilationThreshold) :
                        (MSAst.Expression)code
                );
            } else {
                ret = Ast.Call(
                    AstMethods.MakeFunction,                                                        // method
                    Parent.LocalContext,                                                            // 1. Emit CodeContext
                    FuncCodeExpr,                                                                   // 2. FunctionCode
                    ((IPythonGlobalExpression)GetVariableExpression(_nameVariable)).RawValue(),     // 3. module name
                    defaults.Count == 0 ?                                                           // 4. default values
                        AstUtils.Constant(null, typeof(object[])) :
                        (MSAst.Expression)Ast.NewArrayInit(typeof(object), defaults)
                );
            }

            return AddDecorators(ret, _decorators);
        }

        #region IInstructionProvider Members

        void IInstructionProvider.AddInstructions(LightCompiler compiler) {
            if (_decorators != null) {
                // decorators aren't supported, skip using the optimized instruction.
                compiler.Compile(Reduce());
                return;
            }

            // currently needed so we can later compile
            MSAst.Expression funcCode = GlobalParent.Constant(GetOrMakeFunctionCode());
            FuncCodeExpr = funcCode;

            var variable = Parent.GetVariableExpression(_variable);

            CompileAssignment(compiler, variable, CreateFunctionInstructions);
        }

        private void CreateFunctionInstructions(LightCompiler compiler) {
            // emit context if we have a special local context
            CodeContext globalContext = null;

            compiler.Compile(Parent.LocalContext);

            // emit name if necessary
            PythonGlobalVariableExpression name = GetVariableExpression(_nameVariable) as PythonGlobalVariableExpression;
            PythonGlobal globalName = null;
            if (name == null) {
                compiler.Compile(((IPythonGlobalExpression)GetVariableExpression(_nameVariable)).RawValue());
            } else {
                globalName = name.Global;
            }

            // emit defaults
            int defaultCount = 0;
            for (int i = _parameters.Length - 1; i >= 0; i--) {
                var param = _parameters[i];

                if (param.DefaultValue != null) {
                    compiler.Compile(AstUtils.Convert(param.DefaultValue, typeof(object)));
                    defaultCount++;
                }
            }

            compiler.Instructions.Emit(new FunctionDefinitionInstruction(globalContext, this, defaultCount, globalName));
        }

        private static void CompileAssignment(LightCompiler compiler, MSAst.Expression variable, Action<LightCompiler> compileValue) {
            var instructions = compiler.Instructions;

            ClosureExpression closure = variable as ClosureExpression;
            if (closure != null) {
                compiler.Compile(closure.ClosureCell);
            }
            LookupGlobalVariable lookup = variable as LookupGlobalVariable;
            if (lookup != null) {
                compiler.Compile(lookup.CodeContext);
                instructions.EmitLoad(lookup.Name);
            }

            compileValue(compiler);

            if (closure != null) {
                instructions.EmitStoreField(ClosureExpression._cellField);
                return;
            }
            if (lookup != null) {
                var setter = typeof(PythonOps).GetMethod(lookup.IsLocal ? "SetLocal" : "SetGlobal");
                instructions.Emit(CallInstruction.Create(setter));
                return;
            }

            MSAst.ParameterExpression functionValueParam = variable as MSAst.ParameterExpression;
            if (functionValueParam != null) {
                instructions.EmitStoreLocal(compiler.Locals.GetLocalIndex(functionValueParam));
                return;
            }

            var globalVar = variable as PythonGlobalVariableExpression;
            if (globalVar != null) {
                instructions.Emit(new PythonSetGlobalInstruction(globalVar.Global));
                instructions.EmitPop();
                return;
            }
            Debug.Assert(false, "Unsupported variable type for light compiling function");
        }

        class FunctionDefinitionInstruction : Instruction {
            private readonly FunctionDefinition _def;
            private readonly int _defaultCount;
            private readonly CodeContext _context;
            private readonly PythonGlobal _name;

            public FunctionDefinitionInstruction(CodeContext context, FunctionDefinition/*!*/ definition, int defaultCount, PythonGlobal name) {
                Assert.NotNull(definition);

                _context = context;
                _defaultCount = defaultCount;
                _def = definition;
                _name = name;
            }

            public override int Run(InterpretedFrame frame) {
                object[] defaults;
                if (_defaultCount > 0) {
                    defaults = new object[_defaultCount];
                    for (int i = 0; i < _defaultCount; i++) {
                        defaults[i] = frame.Pop();
                    }
                } else {
                    defaults = ArrayUtils.EmptyObjects;
                }

                object modName;
                if (_name != null) {
                    modName = _name.RawValue;
                } else {
                    modName = frame.Pop();
                }

                CodeContext context = (CodeContext)frame.Pop();
                
                frame.Push(PythonOps.MakeFunction(context, _def.FunctionCode, modName, defaults));

                return +1;
            }

            public override int ConsumedStack {
                get {
                    return _defaultCount +
                        (_context == null ? 1 : 0) +
                        (_name    == null ? 1 : 0);
                }
            }

            public override int ProducedStack {
                get {
                    return 1;
                }
            }
        }

        #endregion

        /// <summary>
        /// Creates the LambdaExpression which is the actual function body.
        /// </summary>
        private LightLambdaExpression EnsureFunctionLambda() {
            if (_dlrBody == null) {
                PerfTrack.NoteEvent(PerfTrack.Categories.Compiler, "Creating FunctionBody");
                _dlrBody = CreateFunctionLambda();
            }

            return _dlrBody;
        }

        internal override Delegate OriginalDelegate {
            get {
                Delegate originalDelegate;
                bool needsWrapperMethod = _parameters.Length > PythonCallTargets.MaxArgs;
                GetDelegateType(_parameters, needsWrapperMethod, out originalDelegate);
                return originalDelegate;
            }
        }

        internal override string ScopeDocumentation {
            get {
                return GetDocumentation(_body);
            }
        }

        /// <summary>
        /// Creates the LambdaExpression which implements the body of the function.
        /// 
        /// The functions signature is either "object Function(PythonFunction, ...)"
        /// where there is one object parameter for each user defined parameter or
        /// object Function(PythonFunction, object[]) for functions which take more
        /// than PythonCallTargets.MaxArgs arguments.
        /// </summary>
        private LightLambdaExpression CreateFunctionLambda() {
            bool needsWrapperMethod = _parameters.Length > PythonCallTargets.MaxArgs;
            Delegate originalDelegate;
            Type delegateType = GetDelegateType(_parameters, needsWrapperMethod, out originalDelegate);

            MSAst.ParameterExpression localContext = null;
            ReadOnlyCollectionBuilder<MSAst.ParameterExpression> locals = new ReadOnlyCollectionBuilder<MSAst.ParameterExpression>();
            if (NeedsLocalContext) {
                localContext = LocalCodeContextVariable;
                locals.Add(localContext);
            }

            MSAst.ParameterExpression[] parameters = CreateParameters(needsWrapperMethod, locals);

            List<MSAst.Expression> init = new List<MSAst.Expression>();

            foreach (var param in _parameters) {
                IPythonVariableExpression pyVar = GetVariableExpression(param.PythonVariable) as IPythonVariableExpression;
                if (pyVar != null) {
                    var varInit = pyVar.Create();
                    if (varInit != null) {
                        init.Add(varInit);
                    }
                }
            }

            // Transform the parameters.
            init.Add(Ast.ClearDebugInfo(GlobalParent.Document));

            locals.Add(PythonAst._globalContext);
            init.Add(Ast.Assign(PythonAst._globalContext, new GetGlobalContextExpression(_parentContext)));

            GlobalParent.PrepareScope(locals, init);

            // Create variables and references. Since references refer to
            // parameters, do this after parameters have been created.

            CreateFunctionVariables(locals, init);

            // Initialize parameters - unpack tuples.
            // Since tuples unpack into locals, this must be done after locals have been created.
            InitializeParameters(init, needsWrapperMethod, parameters);

            List<MSAst.Expression> statements = new List<MSAst.Expression>();
            // add beginning sequence point
            var start = GlobalParent.IndexToLocation(StartIndex);
            statements.Add(GlobalParent.AddDebugInfo(
                AstUtils.Empty(),
                new SourceSpan(new SourceLocation(0, start.Line, start.Column), new SourceLocation(0, start.Line, Int32.MaxValue))));


            // For generators, we need to do a check before the first statement for Generator.Throw() / Generator.Close().
            // The exception traceback needs to come from the generator's method body, and so we must do the check and throw
            // from inside the generator.
            if (IsGenerator) {
                MSAst.Expression s1 = YieldExpression.CreateCheckThrowExpression(SourceSpan.None);
                statements.Add(s1);
            }

            MSAst.ParameterExpression extracted = null;
            if (!IsGenerator && _canSetSysExcInfo) {
                // need to allocate the exception here so we don't share w/ exceptions made & freed
                // during the body.
                extracted = Ast.Parameter(typeof(Exception), "$ex");
                locals.Add(extracted);
            }

            if (_body.CanThrow && !(_body is SuiteStatement) && _body.StartIndex != -1) {
                statements.Add(UpdateLineNumber(GlobalParent.IndexToLocation(_body.StartIndex).Line));
            }

            statements.Add(Body);
            MSAst.Expression body = Ast.Block(statements);

            // If this function can modify sys.exc_info() (_canSetSysExcInfo), then it must restore the result on finish.
            // 
            // Wrap in 
            //   $temp = PythonOps.SaveCurrentException()
            //   <body>
            //   PythonOps.RestoreCurrentException($temp)
            // Skip this if we're a generator. For generators, the try finally is handled by the PythonGenerator class 
            //  before it's invoked. This is because the restoration must occur at every place the function returns from 
            //  a yield point. That's different than the finally semantics in a generator.
            if (extracted != null) {
                MSAst.Expression s = AstUtils.Try(
                    Ast.Assign(
                        extracted,
                        Ast.Call(AstMethods.SaveCurrentException)
                    ),
                    body
                ).Finally(
                    Ast.Call(
                        AstMethods.RestoreCurrentException, extracted
                    )
                );
                body = s;
            }

            if (_body.CanThrow && GlobalParent.PyContext.PythonOptions.Frames) {
                body = AddFrame(LocalContext, Ast.Property(_functionParam, typeof(PythonFunction).GetProperty("__code__")), body);
                locals.Add(FunctionStackVariable);
            }

            body = AddProfiling(body);
            body = WrapScopeStatements(body, _body.CanThrow);
            body = Ast.Block(body, AstUtils.Empty());
            body = AddReturnTarget(body);


            MSAst.Expression bodyStmt = body;
            if (localContext != null) {
                var createLocal = CreateLocalContext(_parentContext);

                init.Add(
                    Ast.Assign(
                        localContext,
                        createLocal
                    )
                );
            }

            init.Add(bodyStmt);

            bodyStmt = Ast.Block(init);

            // wrap a scope if needed
            bodyStmt = Ast.Block(locals.ToReadOnlyCollection(), bodyStmt);

            return AstUtils.LightLambda(
                typeof(object),
                delegateType,
                AddDefaultReturn(bodyStmt, typeof(object)),
                Name + "$" + Interlocked.Increment(ref _lambdaId),
                parameters
            );
        }

        internal override LightLambdaExpression GetLambda() {
            return EnsureFunctionLambda();
        }

        internal FunctionCode FunctionCode {
            get {
                return GetOrMakeFunctionCode();
            }
        }

        private static MSAst.Expression/*!*/ AddDefaultReturn(MSAst.Expression/*!*/ body, Type returnType) {
            if (body.Type == typeof(void) && returnType != typeof(void)) {
                body = Ast.Block(body, Ast.Default(returnType));
            }
            return body;
        }

        private MSAst.ParameterExpression[] CreateParameters(bool needsWrapperMethod, ReadOnlyCollectionBuilder<MSAst.ParameterExpression> locals) {
            MSAst.ParameterExpression[] parameters;
            if (needsWrapperMethod) {
                parameters = new[] { _functionParam, Ast.Parameter(typeof(object[]), "allArgs") };
                foreach (var param in _parameters) {
                    locals.Add(param.ParameterExpression);
                }
            } else {
                parameters = new MSAst.ParameterExpression[_parameters.Length + 1];
                for (int i = 1; i < parameters.Length; i++) {
                    parameters[i] = _parameters[i - 1].ParameterExpression;
                }
                parameters[0] = _functionParam;
            }
            return parameters;
        }

        internal void CreateFunctionVariables(ReadOnlyCollectionBuilder<MSAst.ParameterExpression> locals, List<MSAst.Expression> init) {
            CreateVariables(locals, init);
        }

        internal MSAst.Expression/*!*/ AddReturnTarget(MSAst.Expression/*!*/ expression) {
            if (_hasReturn) {
                return Ast.Label(_returnLabel, AstUtils.Convert(expression, typeof(object)));
            }

            return expression;
        }

        internal override string ProfilerName {
            get {
                var sb = new StringBuilder("def ");
                sb.Append(Name);
                sb.Append('(');
                bool comma = false;
                foreach (var p in _parameters) {
                    if (comma) {
                        sb.Append(", ");
                    } else {
                        comma = true;
                    }
                    sb.Append(p.Name);
                }
                sb.Append(')');
                return sb.ToString();
            }
        }

        private bool EmitDebugFunction() {
            return EmitDebugSymbols && !GlobalParent.PyContext.EnableTracing;
        }

        internal override IList<string> GetVarNames() {
            List<string> res = new List<string>();

            foreach (Parameter p in _parameters) {
                res.Add(p.Name);
            }

            AppendVariables(res);

            return res;
        }
        
        private void InitializeParameters(List<MSAst.Expression> init, bool needsWrapperMethod, MSAst.Expression[] parameters) {
            for (int i = 0; i < _parameters.Length; i++) {
                Parameter p = _parameters[i];
                if (needsWrapperMethod) {
                    // if our method signature is object[] we need to first unpack the argument
                    // from the incoming array.
                    init.Add(
                        AssignValue(
                            GetVariableExpression(p.PythonVariable),
                            Ast.ArrayIndex(
                                parameters[1],
                                Ast.Constant(i)
                            )
                        )
                    );
                }

                p.Init(init);
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_parameters != null) {
                    foreach (Parameter p in _parameters) {
                        p.Walk(walker);
                    }
                }
                if (_decorators != null) {
                    foreach (Expression decorator in _decorators) {
                        decorator.Walk(walker);
                    }
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        /// <summary>
        /// Determines delegate type for the Python function
        /// </summary>
        private static Type GetDelegateType(Parameter[] parameters, bool wrapper, out Delegate originalTarget) {
            return PythonCallTargets.GetPythonTargetType(wrapper, parameters.Length, out originalTarget);
        }

        internal override bool CanThrow {
            get {
                return false;
            }
        }

        internal override void RewriteBody(MSAst.ExpressionVisitor visitor) {
            _dlrBody = null;    // clear the cached body if we've been reduced
            
            MSAst.Expression funcCode = GlobalParent.Constant(GetOrMakeFunctionCode());
            FuncCodeExpr = funcCode;
            
            Body = new RewrittenBodyStatement(Body, visitor.Visit(Body));
        }

        internal static readonly ArbitraryGlobalsVisitor ArbitraryGlobalsVisitorInstance = new ArbitraryGlobalsVisitor();

        /// <summary>
        /// Rewrites the tree for performing lookups against globals instead of being bound
        /// against the optimized scope. This is used if the user creates a function using public
        /// PythonFunction ctor.
        /// </summary>
        internal class ArbitraryGlobalsVisitor : MSAst.ExpressionVisitor {
            protected override MSAst.Expression VisitExtension(MSAst.Expression node) {

                // update the global get/set/raw gets variables
                var global = node as PythonGlobalVariableExpression;
                if (global != null) {
                    return new LookupGlobalVariable(
                        PythonAst._globalContext,
                        global.Variable.Name,
                        global.Variable.Kind == VariableKind.Local
                    );
                }

                // set covers sets and deletes
                var setGlobal = node as PythonSetGlobalVariableExpression;
                if (setGlobal != null) {
                    if (setGlobal.Value == PythonGlobalVariableExpression.Uninitialized) {
                        return new LookupGlobalVariable(
                            PythonAst._globalContext,
                            setGlobal.Global.Variable.Name,
                            setGlobal.Global.Variable.Kind == VariableKind.Local
                        ).Delete();
                    } else {
                        return new LookupGlobalVariable(
                            PythonAst._globalContext,
                            setGlobal.Global.Variable.Name,
                            setGlobal.Global.Variable.Kind == VariableKind.Local
                        ).Assign(Visit(setGlobal.Value));
                    }
                }

                var rawValue = node as PythonRawGlobalValueExpression;
                if (rawValue != null) {
                    return new LookupGlobalVariable(
                        PythonAst._globalContext,
                        rawValue.Global.Variable.Name,
                        rawValue.Global.Variable.Kind == VariableKind.Local
                    );
                }

                return base.VisitExtension(node);
            }
        }
    }
}
