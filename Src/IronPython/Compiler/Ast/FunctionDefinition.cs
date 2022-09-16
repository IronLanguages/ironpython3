// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using MSAst = System.Linq.Expressions;

using LightLambdaExpression = Microsoft.Scripting.Ast.LightLambdaExpression;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class FunctionDefinition : ScopeStatement, IInstructionProvider {
        private readonly string _name;
        private readonly Parameter[] _parameters;
        internal PythonVariable _nameVariable;        // the variable that refers to the global __name__
        private LightLambdaExpression _dlrBody;       // the transformed body including all of our initialization, etc...
        internal bool _hasReturn;
        private static int _lambdaId;
        internal static readonly MSAst.ParameterExpression _functionParam = Ast.Parameter(typeof(PythonFunction), "$function");
        private static readonly MSAst.Expression _GetClosureTupleFromFunctionCall = MSAst.Expression.Call(null, typeof(PythonOps).GetMethod(nameof(PythonOps.GetClosureTupleFromFunction)), _functionParam);
        private static readonly MSAst.Expression _parentContext = new GetParentContextFromFunctionExpression();
        internal static readonly MSAst.LabelTarget _returnLabel = MSAst.Expression.Label(typeof(object), "return");

        public FunctionDefinition(string name, Parameter[] parameters, bool isAsync = false)
            : this(name, parameters, (Statement)null, isAsync) {
        }

        public FunctionDefinition(string name, Parameter[] parameters, Statement body, bool isAsync = false) {
            ContractUtils.RequiresNotNullItems(parameters, nameof(parameters));

            if (name == null) {
                _name = "<lambda$" + Interlocked.Increment(ref _lambdaId) + ">";
                IsLambda = true;
            } else {
                _name = name;
            }

            _parameters = parameters;
            Body = body;
            IsAsync = isAsync;
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
        public bool IsLambda { get; }

        public bool IsAsync { get; }

        public IList<Parameter> Parameters => _parameters;

        internal override string[] ParameterNames => ArrayUtils.ConvertAll(_parameters, val => val.Name);

        internal override int ArgCount {
            get {
                int argCount = 0;
                for (argCount = 0; argCount < _parameters.Length; argCount++)
                {
                    Parameter p = _parameters[argCount];
                    if (p.IsDictionary || p.IsList || p.IsKeywordOnly) break;
                }
                return argCount;
            }
        }

        internal override int KwOnlyArgCount {
            get {
                int kwOnlyArgCount = 0;
                for (int i = ArgCount; i < _parameters.Length; i++, kwOnlyArgCount++) {
                    Parameter p = _parameters[i];
                    if (p.IsDictionary || p.IsList) break;
                }
                return kwOnlyArgCount;
            }
        }

        public Statement Body { get; set; }

        public SourceLocation Header => GlobalParent.IndexToLocation(HeaderIndex);

        public int HeaderIndex { get; set; }

        public override string Name => _name;

        public IList<Expression> Decorators { get; internal set; }

        public Expression ReturnAnnotation { get; internal set; }

        internal override bool IsGeneratorMethod => IsGenerator;

        /// <summary>
        /// The function is a generator
        /// </summary>
        public bool IsGenerator { get; set; }

        internal bool GeneratorStop { get; set; }

        /// <summary>
        /// Called by parser to mark that this function can set sys.exc_info().
        /// An alternative technique would be to just walk the body after the parse and look for a except block.
        ///
        /// true if this function can set sys.exc_info(). Only functions with an except block can set that.
        /// </summary>
        internal bool CanSetSysExcInfo { private get; set; }

        /// <summary>
        /// true if the function contains try/finally, used for generator optimization
        /// </summary>
        internal bool ContainsTryFinally { get; set; }

        /// <summary>
        /// The variable corresponding to the function name or null for lambdas
        /// </summary>
        internal PythonVariable PythonVariable { get; set; }

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            return NeedsLocalsDictionary
                || (ContainsSuperCall && variable.Kind is VariableKind.Parameter
                        && _parameters is not null && _parameters.Length > 0
                        && _parameters[0].PythonVariable == variable);
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
                    // Check for the list and dictionary parameters, which must be the last(two)
                    if (i < _parameters.Length && _parameters[i].IsList) {
                        i++;
                        fa |= FunctionAttributes.ArgumentList;
                    }
                    if (i < _parameters.Length && _parameters[i].IsDictionary) {
                        i++;
                        fa |= FunctionAttributes.KeywordDictionary;
                    }

                    // All parameters must now be exhausted
                    Debug.Assert(i == _parameters.Length);
                }

                if (CanSetSysExcInfo) {
                    fa |= FunctionAttributes.CanSetSysExcInfo;
                }

                if (ContainsTryFinally) {
                    fa |= FunctionAttributes.ContainsTryFinally;
                }

                if (IsGenerator) {
                    fa |= FunctionAttributes.Generator;
                }

                if (GeneratorStop) {
                    fa |= FunctionAttributes.GeneratorStop;
                }

                return fa;
            }
        }

        internal override void AddFreeVariable(PythonVariable variable, bool accessedInScope) {
            if (!accessedInScope) {
                ContainsNestedFreeVariables = true;
            }
            base.AddFreeVariable(variable, accessedInScope);
        }

        internal override bool TryBindOuter(ScopeStatement from, PythonReference reference, out PythonVariable variable) {
            // Functions expose their locals to direct access
            if (TryGetVariable(reference.Name, out variable) && variable.Kind != VariableKind.Nonlocal) {
                variable.AccessedInNestedScope = true;

                if (variable.Kind == VariableKind.Local || variable.Kind == VariableKind.Parameter) {
                    from.AddFreeVariable(variable, true);

                    for (ScopeStatement scope = from.Parent; scope != this; scope = scope.Parent) {
                        scope.AddFreeVariable(variable, false);
                    }

                    AddCellVariable(variable);
                    ContainsNestedFreeVariables = true;
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

                if (variable.Kind != VariableKind.Nonlocal) {
                    return variable;
                }
            }

            // Try to bind in outer scopes
            bool stopAtGlobal = variable?.Kind == VariableKind.Nonlocal;
            for (ScopeStatement parent = Parent;
                parent != null && !(stopAtGlobal && parent.IsGlobal);
                parent = !parent.IsGlobal ? parent.Parent : null) {

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
                _variableMapping[param.PythonVariable] = param.FinishBind(forceClosureCell: ExposesLocalVariable(param.PythonVariable));
            }
            base.FinishBind(binder);
        }

        private void Verify(PythonNameBinder binder) {
            if (ContainsImportStar) {
                binder.ReportSyntaxError("import * only allowed at module level", this);
            }
        }

        /// <summary>
        /// Pulls the closure tuple from our function/generator which is flowed into each function call.
        /// </summary>
        internal override MSAst.Expression/*!*/ GetParentClosureTuple() {
            return _GetClosureTupleFromFunctionCall;
        }

        public override MSAst.Expression Reduce() {
            Debug.Assert(PythonVariable != null, "Shouldn't be called by lambda expression");

            MSAst.Expression function = MakeFunctionExpression();
            return GlobalParent.AddDebugInfoAndVoid(
                AssignValue(Parent.GetVariableExpression(PythonVariable), function),
                new SourceSpan(GlobalParent.IndexToLocation(StartIndex), GlobalParent.IndexToLocation(HeaderIndex))
            );
        }
        
        /// <summary>
        /// Returns an expression which creates the function object.
        /// </summary>
        internal MSAst.Expression MakeFunctionExpression() {
            var defaults = new List<MSAst.Expression>();
            var kwdefaults = new List<MSAst.Expression>();
            var annotations = new List<MSAst.Expression>();

            if (ReturnAnnotation != null) {
                // value needs to come before key in the array
                annotations.Add(AstUtils.Convert(ReturnAnnotation, typeof(object)));
                annotations.Add(Ast.Constant("return", typeof(string)));
            }

            foreach (var param in _parameters) {
                if (param.Kind == ParameterKind.Normal && param.DefaultValue != null) {
                    defaults.Add(AstUtils.Convert(param.DefaultValue, typeof(object)));
                }

                if (param.Kind == ParameterKind.KeywordOnly && param.DefaultValue != null) {
                    // value needs to come before key in the array
                    kwdefaults.Add(AstUtils.Convert(param.DefaultValue, typeof(object)));
                    kwdefaults.Add(Ast.Constant(param.Name, typeof(string)));
                }

                if (param.Annotation != null) {
                    // value needs to come before key in the array
                    annotations.Add(AstUtils.Convert(param.Annotation, typeof(object)));
                    annotations.Add(Ast.Constant(param.Name, typeof(string)));
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
                    kwdefaults.Count == 0 ? AstUtils.Constant(null, typeof(PythonDictionary)) :
                        (MSAst.Expression)Ast.Call(                                                 // 5. kwdefaults
                            AstMethods.MakeDictFromItems,
                            Ast.NewArrayInit(
                                typeof(object),
                                kwdefaults
                            )
                        ),
                    annotations.Count == 0 ? AstUtils.Constant(null, typeof(PythonDictionary)) :
                        (MSAst.Expression)Ast.Call(                                                 // 6. annotations
                            AstMethods.MakeDictFromItems,
                            Ast.NewArrayInit(
                                typeof(object),
                                annotations
                            )
                        ),
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
                        (MSAst.Expression)Ast.NewArrayInit(typeof(object), defaults),
                    kwdefaults.Count == 0 ? AstUtils.Constant(null, typeof(PythonDictionary)) :
                        (MSAst.Expression)Ast.Call(                                                 // 5. kwdefaults
                            AstMethods.MakeDictFromItems,
                            Ast.NewArrayInit(
                                typeof(object),
                                kwdefaults
                            )
                        ),
                    annotations.Count == 0 ? AstUtils.Constant(null, typeof(PythonDictionary)) :
                        (MSAst.Expression)Ast.Call(                                                 // 6. annotations
                            AstMethods.MakeDictFromItems,
                            Ast.NewArrayInit(
                                typeof(object),
                                annotations
                            )
                        )
                );
            }

            return AddDecorators(ret, Decorators);
        }

        #region IInstructionProvider Members

        void IInstructionProvider.AddInstructions(LightCompiler compiler) {
            if (Decorators != null) {
                // decorators aren't supported, skip using the optimized instruction.
                compiler.Compile(Reduce());
                return;
            }

            // currently needed so we can later compile
            MSAst.Expression funcCode = GlobalParent.Constant(GetOrMakeFunctionCode());
            FuncCodeExpr = funcCode;

            var variable = Parent.GetVariableExpression(PythonVariable);

            CompileAssignment(compiler, variable, CreateFunctionInstructions);
        }

        private void CreateFunctionInstructions(LightCompiler compiler) {
            // emit context if we have a special local context
            CodeContext globalContext = null;

            compiler.Compile(Parent.LocalContext);

            // emit name if necessary
            PythonGlobal globalName = null;
            if (GetVariableExpression(_nameVariable) is PythonGlobalVariableExpression name) {
                globalName = name.Global;
            } else {
                compiler.Compile(((IPythonGlobalExpression)GetVariableExpression(_nameVariable)).RawValue());
            }

            // emit defaults
            int defaultCount = 0;
            for (int i = _parameters.Length - 1; i >= 0; i--) {
                var param = _parameters[i];
                if (param.Kind == ParameterKind.Normal && param.DefaultValue != null) {
                    compiler.Compile(AstUtils.Convert(param.DefaultValue, typeof(object)));
                    defaultCount++;
                }
            }

            // emit kwdefaults
            int kwdefaultCount = 0;
            for (int i = _parameters.Length - 1; i >= 0; i--) {
                var param = _parameters[i];
                if (param.Kind == ParameterKind.KeywordOnly && param.DefaultValue != null) {
                    compiler.Compile(AstUtils.Convert(param.DefaultValue, typeof(object)));
                    compiler.Compile(AstUtils.Constant(param.Name, typeof(string)));
                    kwdefaultCount++;
                }
            }

            // emit annotations
            int annotationCount = 0;
            if (ReturnAnnotation != null) {
                compiler.Compile(AstUtils.Convert(ReturnAnnotation, typeof(object)));
                compiler.Compile(AstUtils.Constant("return", typeof(string)));
                annotationCount++;
            }

            for (int i = _parameters.Length - 1; i >= 0; i--) {
                var param = _parameters[i];
                if (param.Annotation != null) {
                    compiler.Compile(AstUtils.Convert(param.Annotation, typeof(object)));
                    compiler.Compile(AstUtils.Constant(param.Name, typeof(string)));
                    annotationCount++;
                }
            }

            compiler.Instructions.Emit(new FunctionDefinitionInstruction(globalContext, this, defaultCount, kwdefaultCount, annotationCount, globalName));
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
                var setter = typeof(PythonOps).GetMethod(lookup.IsLocal ? nameof(PythonOps.SetLocal) : nameof(PythonOps.SetGlobal));
                instructions.Emit(CallInstruction.Create(setter));
                return;
            }

            if (variable is MSAst.ParameterExpression functionValueParam) {
                instructions.EmitStoreLocal(compiler.Locals.GetLocalIndex(functionValueParam));
                return;
            }

            if (variable is PythonGlobalVariableExpression globalVar) {
                instructions.Emit(new PythonSetGlobalInstruction(globalVar.Global));
                instructions.EmitPop();
                return;
            }
            Debug.Assert(false, "Unsupported variable type for light compiling function");
        }

        private class FunctionDefinitionInstruction : Instruction {
            private readonly FunctionDefinition _def;
            private readonly int _defaultCount;
            private readonly CodeContext _context;
            private readonly PythonGlobal _name;
            private readonly int _kwdefaultCount;
            private readonly int _annotationCount;

            public FunctionDefinitionInstruction(CodeContext context, FunctionDefinition/*!*/ definition, int defaultCount, int kwdefaultCount, int annotationCount, PythonGlobal name) {
                Assert.NotNull(definition);

                _context = context;
                _defaultCount = defaultCount;
                _def = definition;
                _name = name;
                _kwdefaultCount = kwdefaultCount;
                _annotationCount = annotationCount;
            }

            public override int Run(InterpretedFrame frame) {
                PythonDictionary annotations = null;
                if (_annotationCount > 0) {
                    annotations = new PythonDictionary();
                    for (int i = 0; i < _annotationCount; i++) {
                        annotations.Add(frame.Pop(), frame.Pop());
                    }
                }

                PythonDictionary kwdefaults = null;
                if (_kwdefaultCount > 0) {
                    kwdefaults = new PythonDictionary();
                    for (int i = 0; i < _kwdefaultCount; i++) {
                        kwdefaults.Add(frame.Pop(), frame.Pop());
                    }
                }

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

                frame.Push(PythonOps.MakeFunction(context, _def.FunctionCode, modName, defaults, kwdefaults, annotations));

                return +1;
            }

            public override int ConsumedStack {
                get {
                    return _defaultCount + (_kwdefaultCount * 2) + (_annotationCount * 2) +
                        (_context == null ? 1 : 0) +
                        (_name    == null ? 1 : 0);
                }
            }

            public override int ProducedStack => 1;
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

        internal override string ScopeDocumentation => GetDocumentation(Body);

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
            Type delegateType = GetDelegateType(_parameters, needsWrapperMethod, out _);

            MSAst.ParameterExpression localContext = null;
            ReadOnlyCollectionBuilder<MSAst.ParameterExpression> locals = new ReadOnlyCollectionBuilder<MSAst.ParameterExpression>();
            if (NeedsLocalContext) {
                localContext = LocalCodeContextVariable;
                locals.Add(localContext);
            }

            MSAst.ParameterExpression[] parameters = CreateParameters(needsWrapperMethod, locals);

            List<MSAst.Expression> init = new List<MSAst.Expression>();

            foreach (var param in _parameters) {
                if (GetVariableExpression(param.PythonVariable) is IPythonVariableExpression pyVar) {
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
                new SourceSpan(new SourceLocation(0, start.Line, start.Column), new SourceLocation(0, start.Line, int.MaxValue))));


            // For generators, we need to do a check before the first statement for Generator.Throw() / Generator.Close().
            // The exception traceback needs to come from the generator's method body, and so we must do the check and throw
            // from inside the generator.
            if (IsGenerator) {
                MSAst.Expression s1 = YieldExpression.CreateCheckThrowExpression(SourceSpan.None);
                statements.Add(s1);
            }

            if (Body.CanThrow && !(Body is SuiteStatement) && Body.StartIndex != -1) {
                statements.Add(UpdateLineNumber(GlobalParent.IndexToLocation(Body.StartIndex).Line));
            }

            statements.Add(Body);
            MSAst.Expression body = Ast.Block(statements);

            if (Body.CanThrow && GlobalParent.PyContext.PythonOptions.Frames) {
                body = AddFrame(LocalContext, Ast.Property(_functionParam, typeof(PythonFunction).GetProperty(nameof(PythonFunction.__code__))), body);
                locals.Add(FunctionStackVariable);
            }

            body = AddProfiling(body);
            body = WrapScopeStatements(body, Body.CanThrow);
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

        internal override LightLambdaExpression GetLambda() => EnsureFunctionLambda();

        internal FunctionCode FunctionCode => GetOrMakeFunctionCode();

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

        private bool EmitDebugFunction() => EmitDebugSymbols && !GlobalParent.PyContext.EnableTracing;

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
                if (Decorators != null) {
                    foreach (Expression decorator in Decorators) {
                        decorator.Walk(walker);
                    }
                }
                ReturnAnnotation?.Walk(walker);
                Body?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        /// <summary>
        /// Determines delegate type for the Python function
        /// </summary>
        private static Type GetDelegateType(Parameter[] parameters, bool wrapper, out Delegate originalTarget)
            => PythonCallTargets.GetPythonTargetType(wrapper, parameters.Length, out originalTarget);

        internal override bool CanThrow => false;

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
                if (node is PythonGlobalVariableExpression global) {
                    return new LookupGlobalVariable(
                        PythonAst._globalContext,
                        global.Variable.Name,
                        global.Variable.Kind == VariableKind.Local
                    );
                }

                // set covers sets and deletes
                if (node is PythonSetGlobalVariableExpression setGlobal) {
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

                if (node is PythonRawGlobalValueExpression rawValue) {
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
