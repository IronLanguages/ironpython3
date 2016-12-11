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
using System.Linq.Expressions;
using Microsoft.Scripting.Ast;
#else
using Microsoft.Scripting.Ast;
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Compiler;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    /// <summary>
    /// Represents a piece of code.  This can reference either a CompiledCode
    /// object or a Function.   The user can explicitly call FunctionCode by
    /// passing it into exec or eval.
    /// </summary>
    [PythonType("code")]
    public class FunctionCode : IExpressionSerializable {
        [PythonHidden]
        internal Delegate Target, LightThrowTarget;                 // the current target for the function.  This can change based upon adaptive compilation, recursion enforcement, and tracing.
        internal Delegate _normalDelegate;                          // the normal delegate - this can be a compiled or interpreted delegate.
        private Compiler.Ast.ScopeStatement _lambda;                // the original DLR lambda that contains the code
        internal readonly string _initialDoc;                       // the initial doc string
        private readonly int _localCount;                           // the number of local variables in the code
        private readonly int _argCount;                             // cached locally because it's used during calls w/ defaults
        private bool _compilingLight;                               // true if we're compiling for light exceptions
        private int _exceptionCount;

        // debugging/tracing support
        private LambdaExpression _tracingLambda;                    // the transformed lambda used for tracing/debugging
        internal Delegate _tracingDelegate;                         // the delegate used for tracing/debugging, if one has been created.  This can be interpreted or compiled.

        /// <summary>
        /// This is both the lock that is held while enumerating the threads or updating the thread accounting
        /// information.  It's also a marker CodeList which is put in place when we are enumerating the thread
        /// list and all additions need to block.
        /// 
        /// This lock is also acquired whenever we need to calculate how a function's delegate should be created 
        /// so that we don't race against sys.settrace/sys.setprofile.
        /// </summary>
        private static CodeList _CodeCreateAndUpdateDelegateLock = new CodeList();

        /// <summary>
        /// Constructor used to create a FunctionCode for code that's been serialized to disk.  
        /// 
        /// Code constructed this way cannot be interpreted or debugged using sys.settrace/sys.setprofile.
        /// 
        /// Function codes created this way do support recursion enforcement and are therefore registered in the global function code registry.
        /// </summary>
        internal FunctionCode(PythonContext context, Delegate code, Compiler.Ast.ScopeStatement scope, string documentation, int localCount) {
            _normalDelegate = code;
            _lambda = scope;
            _argCount = CalculateArgumentCount();
            _initialDoc = documentation;

            // need to take this lock to ensure sys.settrace/sys.setprofile is not actively changing
            lock (_CodeCreateAndUpdateDelegateLock) {
                SetTarget(AddRecursionCheck(context, code));
            }

            RegisterFunctionCode(context);
        }

        /// <summary>
        /// Constructor to create a FunctionCode at runtime.
        /// 
        /// Code constructed this way supports both being interpreted and debugged.  When necessary the code will
        /// be re-compiled or re-interpreted for that specific purpose.
        /// 
        /// Function codes created this way do support recursion enforcement and are therefore registered in the global function code registry.
        /// 
        /// the initial delegate provided here should NOT be the actual code.  It should always be a delegate which updates our Target lazily.
        /// </summary>
        internal FunctionCode(PythonContext context, Delegate initialDelegate, Compiler.Ast.ScopeStatement scope, string documentation, bool? tracing, bool register) {
            _lambda = scope;
            Target = LightThrowTarget = initialDelegate;
            _initialDoc = documentation;
            _localCount = scope.Variables == null ? 0 : scope.Variables.Count;
            _argCount = CalculateArgumentCount();
            if (tracing.HasValue) {
                if (tracing.Value) {
                    _tracingDelegate = initialDelegate;
                } else {
                    _normalDelegate = initialDelegate;
                }
            }

            if (register) {
                RegisterFunctionCode(context);
            }
        }

        private static PythonTuple SymbolListToTuple(IList<string> vars) {
            if (vars != null && vars.Count != 0) {
                object[] tupleData = new object[vars.Count];
                for (int i = 0; i < vars.Count; i++) {
                    tupleData[i] = vars[i];
                }
                return PythonTuple.MakeTuple(tupleData);
            } else {
                return PythonTuple.EMPTY;
            }
        }

        private static PythonTuple StringArrayToTuple(string[] closureVars) {
            if (closureVars != null && closureVars.Length != 0) {
                return PythonTuple.MakeTuple((object[])closureVars);
            } else {
                return PythonTuple.EMPTY;
            }
        }

        /// <summary>
        /// Registers the current function code in our global weak list of all function codes.
        /// 
        /// The weak list can be enumerated with GetAllCode().
        /// 
        /// Ultimately there are 3 types of threads we care about races with:
        ///     1. Other threads which are registering function codes
        ///     2. Threads calling sys.settrace which require the world to stop and get updated
        ///     3. Threads running cleanup (thread pool thread, or call to gc.collect).
        ///     
        /// The 1st two must have perfect synchronization.  We cannot have a thread registering
        /// a new function which another thread is trying to update all of the functions in the world.  Doing
        /// so would mean we could miss adding tracing to a thread.   
        /// 
        /// But the cleanup thread can run in parallel to either registrying or sys.settrace.  The only
        /// thing it needs to take a lock for is updating our accounting information about the
        /// number of code objects are alive.
        /// </summary>
        private void RegisterFunctionCode(PythonContext context) {
            if (_lambda == null) {
                return;
            }

            WeakReference codeRef = new WeakReference(this);
            CodeList prevCode;
            lock (_CodeCreateAndUpdateDelegateLock) {
                Debug.Assert(context._allCodes != _CodeCreateAndUpdateDelegateLock);
                // we do an interlocked operation here because this can run in parallel w/ the CodeCleanup thread.  The
                // lock only prevents us from running in parallel w/ an update to all of the functions in the world which
                // needs to be synchronized.
                do {
                    prevCode = context._allCodes;
                } while (Interlocked.CompareExchange(ref context._allCodes, new CodeList(codeRef, prevCode), prevCode) != prevCode);

                if (context._codeCount++ == context._nextCodeCleanup) {
                    // run cleanup of codes on another thread
                    CleanFunctionCodes(context, false);
                }
            }
        }

        internal static void CleanFunctionCodes(PythonContext context, bool synchronous) {
            if (synchronous) {
                CodeCleanup(context);
            } else {
                ThreadPool.QueueUserWorkItem(CodeCleanup, context);
            }
        }

        internal void SetTarget(Delegate target) {
            Target = LightThrowTarget = target;
        }

        internal void LightThrowCompile(CodeContext/*!*/ context) {
            if (++_exceptionCount > 20) {
                if (!_compilingLight && (object)Target == (object)LightThrowTarget) {
                    _compilingLight = true;
                    if (!IsOnDiskCode) {
                        ThreadPool.QueueUserWorkItem(x => {
                            var pyCtx = context.LanguageContext;

                            bool enableTracing;
                            lock (pyCtx._codeUpdateLock) {
                                enableTracing = context.LanguageContext.EnableTracing;
                            }

                            Delegate target;
                            if (enableTracing) {
                                target = ((LambdaExpression)LightExceptions.Rewrite(GetGeneratorOrNormalLambdaTracing(pyCtx).Reduce())).Compile();
                            } else {
                                target = ((LambdaExpression)LightExceptions.Rewrite(GetGeneratorOrNormalLambda().Reduce())).Compile();

                            }

                            lock (pyCtx._codeUpdateLock) {
                                if (context.LanguageContext.EnableTracing == enableTracing) {
                                    LightThrowTarget = target;
                                }
                            }
                        });
                    }
                }
            }
        }

        private bool IsOnDiskCode {
            get {
                if (_lambda is Compiler.Ast.SerializedScopeStatement) {
                    return true;
                } else if (_lambda is Compiler.Ast.PythonAst) {
                    return ((Compiler.Ast.PythonAst)_lambda).OnDiskProxy;
                }
                return false;
            }
        }

        /// <summary>
        /// Enumerates all function codes for updating the current type of targets we generate.
        /// 
        /// While enumerating we hold a lock so that users cannot change sys.settrace/sys.setprofile
        /// until the lock is released.
        /// </summary>
        private static IEnumerable<FunctionCode> GetAllCode(PythonContext context) {
            // only a single thread can enumerate the current FunctionCodes at a time.
            lock (_CodeCreateAndUpdateDelegateLock) {
                CodeList curCodeList = Interlocked.Exchange(ref context._allCodes, _CodeCreateAndUpdateDelegateLock);
                Debug.Assert(curCodeList != _CodeCreateAndUpdateDelegateLock);

                CodeList initialCode = curCodeList;

                try {
                    while (curCodeList != null) {
                        WeakReference codeRef = curCodeList.Code;
                        FunctionCode target = (FunctionCode)codeRef.Target;

                        if (target != null) {
                            yield return target;
                        }

                        curCodeList = curCodeList.Next;
                    }
                } finally {
                    Interlocked.Exchange(ref context._allCodes, initialCode);
                }
            }
        }

        internal static void UpdateAllCode(PythonContext context) {
            foreach (FunctionCode fc in GetAllCode(context)) {
                fc.UpdateDelegate(context, false);
            }
        }

        private static void CodeCleanup(object state) {
            PythonContext context = (PythonContext)state;

            // only allow 1 thread at a time to do cleanup (other threads can continue adding)
            lock (context._codeCleanupLock) {
                // the bulk of the work is in scanning the list, this proceeeds lock free
                int removed = 0, kept = 0;
                CodeList prev = null;
                CodeList cur = GetRootCodeNoUpdating(context);

                while (cur != null) {
                    if (!cur.Code.IsAlive) {
                        if (prev == null) {
                            if (Interlocked.CompareExchange(ref context._allCodes, cur.Next, cur) != cur) {
                                // someone else snuck in and added a new code entry, spin and try again.
                                cur = GetRootCodeNoUpdating(context);
                                continue;
                            }
                            cur = cur.Next;
                            removed++;
                            continue;
                        } else {
                            // remove from the linked list, we're the only one who can change this.
                            Debug.Assert(prev.Next == cur);
                            removed++;
                            cur = prev.Next = cur.Next;
                            continue;
                        }
                    } else {
                        kept++;
                    }
                    prev = cur;
                    cur = cur.Next;
                }

                // finally update our bookkeeping statistics which requires locking but is fast.
                lock (_CodeCreateAndUpdateDelegateLock) {
                    // calculate the next cleanup, we want each pass to remove ~50% of all entries
                    const double removalGoal = .50;

                    if (context._codeCount == 0) {
                        // somehow we would have had to queue a bunch of function codes, have 1 thread
                        // clean them up all up, and a 2nd queued thread waiting to clean them up as well.
                        // At the same time there would have to be no live functions defined which means
                        // we're executing top-level code which is causing this to happen.
                        context._nextCodeCleanup = 200;
                        return;
                    }
                    //Console.WriteLine("Removed {0} entries, {1} remain", removed, context._codeCount);

                    Debug.Assert(removed <= context._codeCount);
                    double pctRemoved = (double)removed / (double)context._codeCount; // % of code removed
                    double targetRatio = pctRemoved / removalGoal;                    // how we need to adjust our last goal

                    // update the total and next node cleanup
                    int newCount = Interlocked.Add(ref context._codeCount, -removed);
                    Debug.Assert(newCount >= 0);

                    // set the new target for cleanup
                    int nextCleanup = targetRatio != 0 ? newCount + (int)(context._nextCodeCleanup / targetRatio) : -1;
                    if (nextCleanup > 0) {
                        // we're making good progress, use the newly calculated next cleanup point
                        context._nextCodeCleanup = nextCleanup;
                    } else {
                        // none or very small amount cleaned up, just schedule a cleanup for sometime in the future.
                        context._nextCodeCleanup += 500;
                    }

                    Debug.Assert(context._nextCodeCleanup >= context._codeCount, String.Format("{0} vs {1} ({2})", context._nextCodeCleanup, context._codeCount, targetRatio));
                }
            }
        }

        private static CodeList GetRootCodeNoUpdating(PythonContext context) {
            CodeList cur = context._allCodes;
            if (cur == _CodeCreateAndUpdateDelegateLock) {
                lock (_CodeCreateAndUpdateDelegateLock) {
                    // wait until enumerating thread is done, but it's alright
                    // if we got cur and then an enumeration started (because we'll
                    // just clear entries out)
                    cur = context._allCodes;
                    Debug.Assert(cur != _CodeCreateAndUpdateDelegateLock);
                }
            }
            return cur;
        }

        public SourceSpan Span {
            [PythonHidden]
            get {
                return _lambda.Span;
            }
        }

        internal string[] ArgNames {
            get {
                return _lambda.ParameterNames;
            }
        }

        internal FunctionAttributes Flags {
            get {
                return _lambda.Flags;
            }
        }

        internal bool IsModule {
            get {
                return _lambda is Compiler.Ast.PythonAst;
            }
        }

        #region Public constructors

        /*
        /// <summary>
        /// Standard python siganture
        /// </summary>
        /// <param name="argcount"></param>
        /// <param name="nlocals"></param>
        /// <param name="stacksize"></param>
        /// <param name="flags"></param>
        /// <param name="codestring"></param>
        /// <param name="constants"></param>
        /// <param name="names"></param>
        /// <param name="varnames"></param>
        /// <param name="filename"></param>
        /// <param name="name"></param>
        /// <param name="firstlineno"></param>
        /// <param name="nlotab"></param>
        /// <param name="freevars"></param>
        /// <param name="callvars"></param>
        public FunctionCode(int argcount, int nlocals, int stacksize, int flags, string codestring, object constants, Tuple names, Tuple varnames, string filename, string name, int firstlineno, object nlotab, [DefaultParameterValue(null)]object freevars, [DefaultParameterValue(null)]object callvars) {
        }*/

        #endregion

        #region Public Python API Surface

        public PythonTuple co_varnames {
            get {
                return SymbolListToTuple(_lambda.GetVarNames());
            }
        }

        public int co_argcount {
            get {
                return _argCount;
            }
        }

        private int CalculateArgumentCount() {
            int argCnt = _lambda.ArgCount;
            FunctionAttributes flags = Flags;
            if ((flags & FunctionAttributes.ArgumentList) != 0) argCnt--;
            if ((flags & FunctionAttributes.KeywordDictionary) != 0) argCnt--;
            return argCnt;
        }

        /// <summary>
        /// Returns a list of variable names which are accessed from nested functions.
        /// </summary>
        public PythonTuple co_cellvars {
            get {
                return SymbolListToTuple(_lambda.CellVariables != null ? ArrayUtils.ToArray(_lambda.CellVariables) : null);
            }
        }

        /// <summary>
        /// Returns the byte code.  IronPython does not implement this and always
        /// returns an empty string for byte code.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public object co_code {
            get {
                return String.Empty;
            }
        }

        /// <summary>
        /// Returns a list of constants used by the function.
        /// 
        /// The first constant is the doc string, or None if no doc string is provided.
        /// 
        /// IronPython currently does not include any other constants than the doc string.
        /// </summary>
        public PythonTuple co_consts {
            get {
                if (_initialDoc != null) {
                    return PythonTuple.MakeTuple(_initialDoc, null);
                }

                return PythonTuple.MakeTuple((object)null);
            }
        }

        /// <summary>
        /// Returns the filename that the code object was defined in.
        /// </summary>
        public string co_filename {
            get {
                return _lambda.Filename;
            }
        }

        /// <summary>
        /// Returns the 1st line number of the code object.
        /// </summary>
        public int co_firstlineno {
            get {
                return Span.Start.Line;
            }
        }

        /// <summary>
        /// Returns a set of flags for the function.
        /// 
        ///     0x04 is set if the function used *args
        ///     0x08 is set if the function used **args
        ///     0x20 is set if the function is a generator
        /// </summary>
        public int co_flags {
            get {
                return (int)Flags;
            }
        }

        /// <summary>
        /// Returns a list of free variables (variables accessed
        /// from an outer scope).  This does not include variables
        /// accessed in the global scope.
        /// </summary>
        public PythonTuple co_freevars {
            get {
                return SymbolListToTuple(_lambda.FreeVariables != null ? CollectionUtils.ConvertAll(_lambda.FreeVariables, x => x.Name) : null);
            }
        }

        /// <summary>
        /// Returns a mapping between byte code and line numbers.  IronPython does
        /// not implement this because byte code is not available.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public object co_lnotab {
            get {
                throw PythonOps.NotImplementedError("");
            }
        }

        /// <summary>
        /// Returns the name of the code (function name, class name, or &lt;module&gt;).
        /// </summary>
        public string co_name {
            get {
                return _lambda.Name;
            }
        }

        /// <summary>
        /// Returns a list of global variable names accessed by the code.
        /// </summary>
        public PythonTuple co_names {
            get {
                return SymbolListToTuple(_lambda.GlobalVariables);
            }
        }

        /// <summary>
        /// Returns the number of local varaibles defined in the function.
        /// </summary>
        public object co_nlocals {
            get {
                return _localCount;
            }
        }

        /// <summary>
        /// Returns the stack size.  IronPython does not implement this
        /// because byte code is not supported.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public object co_stacksize {
            get {
                throw PythonOps.NotImplementedError("");
            }
        }

        #endregion

        #region Internal API Surface

        internal LightLambdaExpression Code {
            get {
                return _lambda.GetLambda();
            }
        }

        internal Compiler.Ast.ScopeStatement PythonCode {
            get {
                return (Compiler.Ast.ScopeStatement)_lambda;
            }
        }

        internal object Call(CodeContext/*!*/ context) {
            if (co_freevars != PythonTuple.EMPTY) {
                throw PythonOps.TypeError("cannot exec code object that contains free variables: {0}", co_freevars.__repr__(context));
            }

            if (Target == null || (Target.GetMethodInfo() != null && Target.GetMethodInfo().DeclaringType == typeof(PythonCallTargets))) {
                UpdateDelegate(context.LanguageContext, true);
            }

            Func<CodeContext, CodeContext> classTarget = Target as Func<CodeContext, CodeContext>;
            if (classTarget != null) {
                return classTarget(context);
            }

            LookupCompilationDelegate moduleCode = Target as LookupCompilationDelegate;
            if (moduleCode != null) {
                return moduleCode(context, this);
            }

            Func<FunctionCode, object> optimizedModuleCode = Target as Func<FunctionCode, object>;
            if (optimizedModuleCode != null) {
                return optimizedModuleCode(this);
            }

            var func = new PythonFunction(context, this, null, ArrayUtils.EmptyObjects, new MutableTuple<object>());
            CallSite<Func<CallSite, CodeContext, PythonFunction, object>> site = PythonContext.GetContext(context).FunctionCallSite;
            return site.Target(site, context, func);
        }

        /// <summary>
        /// Creates a FunctionCode object for exec/eval/execfile'd/compile'd code.
        /// 
        /// The code is then executed in a specific CodeContext by calling the .Call method.
        /// 
        /// If the code is being used for compile (vs. exec/eval/execfile) then it needs to be
        /// registered in case our tracing mode changes.
        /// </summary>
        internal static FunctionCode FromSourceUnit(SourceUnit sourceUnit, PythonCompilerOptions options, bool register) {
            var code = PythonContext.CompilePythonCode(sourceUnit, options, ThrowingErrorSink.Default);
            if (sourceUnit.CodeProperties == ScriptCodeParseResult.Empty) {
                // source span is made up
                throw new SyntaxErrorException("unexpected EOF while parsing",
                    sourceUnit, new SourceSpan(new SourceLocation(0, 1, 1), new SourceLocation(0, 1, 1)), 0, Severity.Error);
            }
            return ((RunnableScriptCode)code).GetFunctionCode(register);
        }

        #endregion

        #region Private helper functions

        private void ExpandArgsTuple(List<string> names, PythonTuple toExpand) {
            for (int i = 0; i < toExpand.__len__(); i++) {
                if (toExpand[i] is PythonTuple) {
                    ExpandArgsTuple(names, toExpand[i] as PythonTuple);
                } else {
                    names.Add(toExpand[i] as string);
                }
            }
        }

        #endregion

        public override bool Equals(object obj) {
            // overridden because CPython defines this on code objects
            return base.Equals(obj);
        }

        public override int GetHashCode() {
            // overridden because CPython defines this on code objects
            return base.GetHashCode();
        }

        public int __cmp__(CodeContext/*!*/ context, [NotNull]FunctionCode/*!*/  other) {
            if (other == this) {
                return 0;
            }

            long lres = IdDispenser.GetId(this) - IdDispenser.GetId(other);
            return lres > 0 ? 1 : -1;
        }

        // these are present in CPython but always return NotImplemented.
        [return: MaybeNotImplemented]
        [Python3Warning("code inequality comparisons not supported in 3.x")]
        public static NotImplementedType operator >(FunctionCode self, FunctionCode other) {
            return PythonOps.NotImplemented;
        }

        [return: MaybeNotImplemented]
        [Python3Warning("code inequality comparisons not supported in 3.x")]
        public static NotImplementedType operator <(FunctionCode self, FunctionCode other) {
            return PythonOps.NotImplemented;
        }

        [return: MaybeNotImplemented]
        [Python3Warning("code inequality comparisons not supported in 3.x")]
        public static NotImplementedType operator >=(FunctionCode self, FunctionCode other) {
            return PythonOps.NotImplemented;
        }

        [return: MaybeNotImplemented]
        [Python3Warning("code inequality comparisons not supported in 3.x")]
        public static NotImplementedType operator <=(FunctionCode self, FunctionCode other) {
            return PythonOps.NotImplemented;
        }

        /// <summary>
        /// Called the 1st time a function is invoked by our OriginalCallTarget* methods
        /// over in PythonCallTargets.  This computes the real delegate which needs to be
        /// created for the function.  Usually this means starting off interpretering.  It 
        /// also involves adding the wrapper function for recursion enforcement.
        /// 
        /// Because this can race against sys.settrace/setprofile we need to take our 
        /// _ThreadIsEnumeratingAndAccountingLock to ensure no one is actively changing all
        /// of the live functions.
        /// </summary>
        internal void LazyCompileFirstTarget(PythonFunction function) {
            lock (_CodeCreateAndUpdateDelegateLock) {
                UpdateDelegate(PythonContext.GetContext(function.Context), true);
            }
        }

        /// <summary>
        /// Updates the delegate based upon current Python context settings for recursion enforcement
        /// and for tracing.
        /// </summary>
        internal void UpdateDelegate(PythonContext context, bool forceCreation) {
            Delegate finalTarget;

            if (context.EnableTracing && _lambda != null) {
                if (_tracingLambda == null) {
                    if (!forceCreation) {
                        // the user just called sys.settrace(), don't force re-compilation of every method in the system.  Instead
                        // we'll just re-compile them as they're invoked.
                        PythonCallTargets.GetPythonTargetType(_lambda.ParameterNames.Length > PythonCallTargets.MaxArgs, _lambda.ParameterNames.Length, out Target);
                        LightThrowTarget = Target;
                        return;
                    }
                    _tracingLambda = GetGeneratorOrNormalLambdaTracing(context);
                }

                if (_tracingDelegate == null) {
                    _tracingDelegate = CompileLambda(_tracingLambda, new TargetUpdaterForCompilation(context, this).SetCompiledTargetTracing);
                }

                finalTarget = _tracingDelegate;
            } else {
                if (_normalDelegate == null) {
                    if (!forceCreation) {                        
                        // we cannot create the delegate when forceCreation is false because we hold the
                        // _CodeCreateAndUpdateDelegateLock and compiling the delegate may create a FunctionCode
                        // object which requires the lock.
                        PythonCallTargets.GetPythonTargetType(_lambda.ParameterNames.Length > PythonCallTargets.MaxArgs, _lambda.ParameterNames.Length, out Target);
                        LightThrowTarget = Target;
                        return;
                    }
                    _normalDelegate = CompileLambda(GetGeneratorOrNormalLambda(), new TargetUpdaterForCompilation(context, this).SetCompiledTarget);
                }

                finalTarget = _normalDelegate;
            }

            finalTarget = AddRecursionCheck(context, finalTarget);

            SetTarget(finalTarget);
        }

        /// <summary>
        /// Called to set the initial target delegate when the user has passed -X:Debug to enable
        /// .NET style debugging.
        /// </summary>
        internal void SetDebugTarget(PythonContext context, Delegate target) {
            _normalDelegate = target;

            SetTarget(AddRecursionCheck(context, target));
        }

        /// <summary>
        /// Gets the LambdaExpression for tracing.  
        /// 
        /// If this is a generator function code then the lambda gets tranformed into the correct generator code.
        /// </summary>
        private LambdaExpression GetGeneratorOrNormalLambdaTracing(PythonContext context) {
            var debugProperties = new PythonDebuggingPayload(this);

            var debugInfo = new Microsoft.Scripting.Debugging.CompilerServices.DebugLambdaInfo(
                null,           // IDebugCompilerSupport
                null,           // lambda alias
                false,          // optimize for leaf frames
                null,           // hidden variables
                null,           // variable aliases
                debugProperties // custom payload
            );

            if ((Flags & FunctionAttributes.Generator) == 0) {
                return context.DebugContext.TransformLambda((LambdaExpression)Compiler.Ast.Node.RemoveFrame(_lambda.GetLambda()), debugInfo);
            }

            return Expression.Lambda(
                Code.Type,
                new GeneratorRewriter(
                    _lambda.Name,
                    Compiler.Ast.Node.RemoveFrame(Code.Body)
                ).Reduce(
                    _lambda.ShouldInterpret,
                    _lambda.EmitDebugSymbols,
                    context.Options.CompilationThreshold,
                    Code.Parameters,
                    x => (Expression<Func<MutableTuple, object>>)context.DebugContext.TransformLambda(x, debugInfo)
                ),
                Code.Name,
                Code.Parameters
            );
        }

        
        /// <summary>
        /// Gets the correct final LambdaExpression for this piece of code.
        /// 
        /// This is either just _lambda or _lambda re-written to be a generator expression.
        /// </summary>
        private LightLambdaExpression GetGeneratorOrNormalLambda() {
            LightLambdaExpression finalCode;
            if ((Flags & FunctionAttributes.Generator) == 0) {
                finalCode = Code;
            } else {
                finalCode = Code.ToGenerator(
                    _lambda.ShouldInterpret, 
                    _lambda.EmitDebugSymbols, 
                    _lambda.GlobalParent.PyContext.Options.CompilationThreshold
                );
            }
            return finalCode;
        }

        private Delegate CompileLambda(LightLambdaExpression code, EventHandler<LightLambdaCompileEventArgs> handler) {
#if EMIT_PDB
            if (_lambda.EmitDebugSymbols) {
                return CompilerHelpers.CompileToMethod((LambdaExpression)code.Reduce(), DebugInfoGenerator.CreatePdbGenerator(), true);
            }
#endif    
            if (_lambda.ShouldInterpret) {
                Delegate result = code.Compile(_lambda.GlobalParent.PyContext.Options.CompilationThreshold);

                // If the adaptive compiler decides to compile this function, we
                // want to store the new compiled target. This saves us from going
                // through the interpreter stub every call.
                var lightLambda = result.Target as LightLambda;
                if (lightLambda != null) {
                    lightLambda.Compile += handler;
                }

                return result;
            }

            return code.Compile();
        }

        private Delegate CompileLambda(LambdaExpression code, EventHandler<LightLambdaCompileEventArgs> handler) {
#if EMIT_PDB
            if (_lambda.EmitDebugSymbols) {
                return CompilerHelpers.CompileToMethod(code, DebugInfoGenerator.CreatePdbGenerator(), true);
            } 
#endif
            if (_lambda.ShouldInterpret) {
                Delegate result = CompilerHelpers.LightCompile(code, _lambda.GlobalParent.PyContext.Options.CompilationThreshold);

                // If the adaptive compiler decides to compile this function, we
                // want to store the new compiled target. This saves us from going
                // through the interpreter stub every call.
                var lightLambda = result.Target as LightLambda;
                if (lightLambda != null) {
                    lightLambda.Compile += handler;
                }

                return result;
            }

            return code.Compile();
        }

        internal Delegate AddRecursionCheck(PythonContext context, Delegate finalTarget) {
            if (context.RecursionLimit != Int32.MaxValue) {
                if (finalTarget is Func<CodeContext, CodeContext> || 
                    finalTarget is Func<FunctionCode, object> ||
                    finalTarget is LookupCompilationDelegate) {
                    // no recursion enforcement on classes or modules
                    return finalTarget;
                }

                switch (_lambda.ParameterNames.Length) {
                    #region Generated Python Recursion Delegate Switch

                    // *** BEGIN GENERATED CODE ***
                    // generated by function: gen_recursion_delegate_switch from: generate_calls.py

                    case 0:
                        finalTarget = new Func<PythonFunction, object>(new PythonFunctionRecursionCheck0((Func<PythonFunction, object>)finalTarget).CallTarget);
                        break;
                    case 1:
                        finalTarget = new Func<PythonFunction, object, object>(new PythonFunctionRecursionCheck1((Func<PythonFunction, object, object>)finalTarget).CallTarget);
                        break;
                    case 2:
                        finalTarget = new Func<PythonFunction, object, object, object>(new PythonFunctionRecursionCheck2((Func<PythonFunction, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 3:
                        finalTarget = new Func<PythonFunction, object, object, object, object>(new PythonFunctionRecursionCheck3((Func<PythonFunction, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 4:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object>(new PythonFunctionRecursionCheck4((Func<PythonFunction, object, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 5:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object, object>(new PythonFunctionRecursionCheck5((Func<PythonFunction, object, object, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 6:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object, object, object>(new PythonFunctionRecursionCheck6((Func<PythonFunction, object, object, object, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 7:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object, object, object, object>(new PythonFunctionRecursionCheck7((Func<PythonFunction, object, object, object, object, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 8:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object, object, object, object, object>(new PythonFunctionRecursionCheck8((Func<PythonFunction, object, object, object, object, object, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 9:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object, object, object, object, object, object>(new PythonFunctionRecursionCheck9((Func<PythonFunction, object, object, object, object, object, object, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 10:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object>(new PythonFunctionRecursionCheck10((Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 11:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object, object>(new PythonFunctionRecursionCheck11((Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 12:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object, object, object>(new PythonFunctionRecursionCheck12((Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 13:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object, object, object, object>(new PythonFunctionRecursionCheck13((Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 14:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object, object, object, object, object>(new PythonFunctionRecursionCheck14((Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object, object, object, object, object>)finalTarget).CallTarget);
                        break;
                    case 15:
                        finalTarget = new Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object, object, object, object, object, object>(new PythonFunctionRecursionCheck15((Func<PythonFunction, object, object, object, object, object, object, object, object, object, object, object, object, object, object, object, object>)finalTarget).CallTarget);
                        break;

                    // *** END GENERATED CODE ***

                    #endregion
                    default:
                        finalTarget = new Func<PythonFunction, object[], object>(new PythonFunctionRecursionCheckN((Func<PythonFunction, object[], object>)finalTarget).CallTarget);
                        break;

                }
            }
            return finalTarget;
        }

        class TargetUpdaterForCompilation {
            private readonly PythonContext _context;
            private readonly FunctionCode _code;

            public TargetUpdaterForCompilation(PythonContext context, FunctionCode code) {
                _code = code;
                _context = context;
            }

            public void SetCompiledTarget(object sender, Microsoft.Scripting.Interpreter.LightLambdaCompileEventArgs e) {
                _code.SetTarget(_code.AddRecursionCheck(_context, _code._normalDelegate = e.Compiled));
            }

            public void SetCompiledTargetTracing(object sender, Microsoft.Scripting.Interpreter.LightLambdaCompileEventArgs e) {
                _code.SetTarget(_code.AddRecursionCheck(_context, _code._tracingDelegate = e.Compiled));
            }
        }

        #region IExpressionSerializable Members

        Expression IExpressionSerializable.CreateExpression() {
            return Expression.Call(
                typeof(PythonOps).GetMethod("MakeFunctionCode"),
                Compiler.Ast.PythonAst._globalContext,
                Expression.Constant(_lambda.Name),
                Expression.Constant(_initialDoc, typeof(string)),
                Expression.NewArrayInit(
                    typeof(string),
                    ArrayUtils.ConvertAll(_lambda.ParameterNames, (x) => Expression.Constant(x))
                ),
                Expression.Constant(Flags),
                Expression.Constant(_lambda.IndexSpan.Start),
                Expression.Constant(_lambda.IndexSpan.End),
                Expression.Constant(_lambda.Filename),
                GetGeneratorOrNormalLambda(),
                TupleToStringArray(co_freevars),
                TupleToStringArray(co_names),
                TupleToStringArray(co_cellvars),
                TupleToStringArray(co_varnames),
                Expression.Constant(_localCount)
            );
        }

        private static Expression TupleToStringArray(PythonTuple tuple) {
            return tuple.Count > 0 ?
                (Expression)Expression.NewArrayInit(
                    typeof(string),
                    ArrayUtils.ConvertAll(tuple._data, (x) => Expression.Constant(x))
                ) :
                (Expression)Expression.Constant(null, typeof(string[]));
        }

        #endregion

        /// <summary>
        /// Extremely light weight linked list of weak references used for tracking
        /// all of the FunctionCode objects which get created and need to be updated
        /// for purposes of recursion enforcement or tracing.
        /// </summary>
        internal class CodeList {
            public readonly WeakReference Code;
            public CodeList Next;

            public CodeList() { }

            public CodeList(WeakReference code, CodeList next) {
                Code = code;
                Next = next;
            }
        }
    }

    internal class PythonDebuggingPayload {
        public FunctionCode Code;
        private Dictionary<int, bool> _handlerLocations;
        private Dictionary<int, Dictionary<int, bool>> _loopAndFinallyLocations;

        public PythonDebuggingPayload(FunctionCode code) {
            Code = code;
        }

        public Dictionary<int, bool> HandlerLocations {
            get {
                if (_handlerLocations == null) {
                    GatherLocations();
                }

                return _handlerLocations;
            }
        }

        public Dictionary<int, Dictionary<int, bool>> LoopAndFinallyLocations {
            get {
                if (_loopAndFinallyLocations == null) {
                    GatherLocations();
                }

                return _loopAndFinallyLocations;
            }
        }

        private void GatherLocations() {
            var walker = new TracingWalker();

            Code.PythonCode.Walk(walker);

            _loopAndFinallyLocations = walker.LoopAndFinallyLocations;
            _handlerLocations = walker.HandlerLocations;
        }

        class TracingWalker : Compiler.Ast.PythonWalker {
            private bool _inLoop, _inFinally;
            private int _loopId;
            public Dictionary<int, bool> HandlerLocations = new Dictionary<int, bool>();
            public Dictionary<int, Dictionary<int, bool>> LoopAndFinallyLocations = new Dictionary<int, Dictionary<int, bool>>();
            private Dictionary<int, bool> _loopIds = new Dictionary<int, bool>();

            public override bool Walk(Compiler.Ast.ForStatement node) {
                UpdateLoops(node);
                WalkLoopBody(node.Body, false);

                if (node.Else != null) {
                    node.Else.Walk(this);
                }

                return false;
            }

            private void WalkLoopBody(IronPython.Compiler.Ast.Statement body, bool isFinally) {

                bool inLoop = _inLoop;
                bool inFinally = _inFinally;

                int loopId = ++_loopId;
                _inFinally = false;
                _inLoop = true;
                _loopIds.Add(loopId, isFinally);

                body.Walk(this);

                _inLoop = inLoop;
                _inFinally = inFinally;
                LoopOrFinallyIds.Remove(loopId);
            }

            public override bool Walk(Compiler.Ast.WhileStatement node) {
                UpdateLoops(node);

                WalkLoopBody(node.Body, false);

                if (node.ElseStatement != null) {
                    node.ElseStatement.Walk(this);
                }

                return false;
            }

            public override bool Walk(Compiler.Ast.TryStatement node) {
                UpdateLoops(node);

                node.Body.Walk(this);

                if (node.Handlers != null) {
                    foreach (var handler in node.Handlers) {
                        HandlerLocations[handler.Span.Start.Line] = false;
                        handler.Body.Walk(this);
                    }
                }

                if (node.Finally != null) {
                    WalkLoopBody(node.Finally, true);
                }
                return false;
            }


            public override bool Walk(IronPython.Compiler.Ast.AssertStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.AssignmentStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.AugmentedAssignStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.BreakStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.ClassDefinition node) {
                UpdateLoops(node);
                return false;
            }

            public override bool Walk(IronPython.Compiler.Ast.ContinueStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override void PostWalk(IronPython.Compiler.Ast.EmptyStatement node) {
                UpdateLoops(node);
                base.PostWalk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.DelStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.EmptyStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.GlobalStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.FromImportStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.ExpressionStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.FunctionDefinition node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.IfStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.ImportStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.RaiseStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            public override bool Walk(IronPython.Compiler.Ast.WithStatement node) {
                UpdateLoops(node);
                return base.Walk(node);
            }

            private void UpdateLoops(Compiler.Ast.Statement stmt) {
                if (_inFinally || _inLoop) {
                    if (!LoopAndFinallyLocations.ContainsKey(stmt.Span.Start.Line)) {
                        LoopAndFinallyLocations.Add(stmt.Span.Start.Line, new Dictionary<int, bool>(LoopOrFinallyIds));
                    }
                }
            }

            public Dictionary<int, bool> LoopOrFinallyIds {
                get {
                    if (_loopIds == null) {
                        _loopIds = new Dictionary<int, bool>();
                    }
                    return _loopIds;
                }
            }

        }
    }
}
