// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Compiler;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix"), PythonType("generator")]
    [DontMapIDisposableToContextManager, DontMapIEnumerableToContains]
    public sealed class PythonGenerator : IEnumerator, IEnumerator<object>, ICodeFormattable, IEnumerable, IWeakReferenceable {
        private readonly Func<MutableTuple, object>/*!*/ _next;     // The delegate which contains the user code to perform the iteration.
        private readonly PythonFunction _function;                  // the function which created the generator
        private readonly MutableTuple _data;                        // the closure data we need to pass into each iteration.  Item000 is the index, Item001 is the current value
        private readonly MutableTuple<int, object> _dataTuple;      // the tuple which has our current index and value
        private GeneratorFlags _flags;                              // Flags capturing various state for the generator

        private GeneratorFinalizer _finalizer;                      // finalizer object

        private ExceptionState _state = new ExceptionState();

        /// <summary>
        /// We cache the GeneratorFinalizer of generators that were closed on the user
        /// thread, and did not get finalized on the finalizer thread. We can then reuse
        /// the object. Reusing objects with a finalizer is good because it reduces
        /// the load on the GC's finalizer queue.
        /// </summary>
        private static GeneratorFinalizer _LastFinalizer;

        /// <summary>
        /// Fields set by Throw() to communicate an exception to the yield point.
        /// These are plumbed through the generator to become parameters to Raise(...) invoked 
        /// at the yield suspension point in the generator.
        /// </summary>
        private object[] _excInfo;
        /// <summary>
        /// Value sent by generator.send().
        /// Since send() could send an exception, we need to keep this different from throwable's value.
        /// </summary>
        private object _sendValue;
        private WeakRefTracker _tracker;

        internal PythonGenerator(PythonFunction function, Func<MutableTuple, object>/*!*/ next, MutableTuple data) {
            _function = function;
            _next = next;
            _data = data;
            _dataTuple = GetDataTuple();
            State = GeneratorRewriter.NotStarted;

            if (_LastFinalizer == null || (_finalizer = Interlocked.Exchange(ref _LastFinalizer, null)) == null) {
                _finalizer = new GeneratorFinalizer(this);
            } else {
                _finalizer.Generator = this;
            }
        }

        #region Python Public APIs

        [LightThrowing]
        public object __next__() {
            // Python's language policy on generators is that attempting to access after it's closed (returned)
            // just continues to throw StopIteration exceptions.
            if (Closed) {
                return LightExceptions.Throw(PythonOps.StopIteration());
            }
            
            object res = NextWorker();
            if (res == OperationFailed.Value) {
                return LightExceptions.Throw(new PythonExceptions._StopIteration().InitAndGetClrException((FinalValue)));
            }

            return res;
        }

        /// <summary>
        /// See PEP 342 (http://python.org/dev/peps/pep-0342/) for details of new methods on Generator.
        /// Full signature including default params for throw is:
        ///    throw(type, value=None, traceback=None)
        /// Use multiple overloads to resolve the default parameters.
        /// </summary>
        [LightThrowing]
        public object @throw(object type) {
            return @throw(type, null, null, false);
        }

        [LightThrowing]
        public object @throw(object type, object value) {
            return @throw(type, value, null, false);
        }

        [LightThrowing]
        public object @throw(object type, object value, object traceback) {
            return @throw(type, value, traceback, false);
        }

        /// <summary>
        /// Throw(...) is like Raise(...) being called from the yield point within the generator.
        /// Note it must come from inside the generator so that the traceback matches, and so that it can 
        /// properly cooperate with any try/catch/finallys inside the generator body.
        /// 
        /// If the generator catches the exception and yields another value, that is the return value of g.throw().
        /// </summary>
        [LightThrowing]
        private object @throw(object type, object value, object traceback, bool finalizing) {
            // The Pep342 explicitly says "The type argument must not be None". 
            // According to CPython 2.5's implementation, a null type argument should:
            // - throw a TypeError exception (just as Raise(None) would) *outside* of the generator's body
            //   (so the generator can't catch it).
            // - not update any other generator state (so future calls to Next() will still work)
            if (type == null) {
                // Create the appropriate exception and throw it.
                throw PythonOps.MakeExceptionTypeError(null, true);
            }

            // Set fields which will then be used by CheckThrowable.
            // We create the actual exception from inside the generator so that if the exception's __init__ 
            // throws, the traceback matches that which we get from CPython2.5.
            _excInfo = new object[] { type, value, traceback };
            Debug.Assert(_sendValue == null);

            // Pep explicitly says that Throw on a closed generator throws the exception, 
            // and not a StopIteration exception. (This is different than Next()).
            if (Closed) {
                // this will throw the exception that we just set the fields for.
                var throwable = CheckThrowable();
                if (throwable != null) {
                    return throwable;
                }
            }
            if (finalizing) {
                // we are running on the finalizer thread - things can be already collected
                return LightExceptions.Throw(PythonOps.StopIteration());
            }
            if (!((IEnumerator)this).MoveNext()) {
                return LightExceptions.Throw(PythonOps.StopIteration());
            }
            return CurrentValue;
        }

        /// <summary>
        /// send() was added in Pep342. It sends a result back into the generator, and the expression becomes
        /// the result of yield when used as an expression.
        /// </summary>
        [LightThrowing]
        public object send(object value) {
            Debug.Assert(_excInfo == null);

            // CPython2.5's behavior is that Send(non-null) on unstarted generator should:
            // - throw a TypeError exception
            // - not change generator state. So leave as unstarted, and allow future calls to succeed.
            if (value != null && State == GeneratorRewriter.NotStarted) {
                throw PythonOps.TypeErrorForIllegalSend();
            }

            _sendValue = value;
            return __next__();
        }

        [LightThrowing]
        public object close() {
            return close(false);
        }

        /// <summary>
        /// Close introduced in Pep 342.
        /// </summary>
        [LightThrowing]
        private object close(bool finalizing) {
            // This is nop if the generator is already closed.
            // Optimization to avoid throwing + catching an exception if we're already closed.
            if (Closed) {
                return null;
            }

            // This function body is the psuedo code straight from Pep 342.
            try {
                object res = @throw(new GeneratorExitException(), null, null, finalizing);
                Exception lightEh = LightExceptions.GetLightException(res);
                if (lightEh != null) {
                    if (lightEh is StopIterationException || lightEh is GeneratorExitException) {
                        return null;
                    }
                    return lightEh;
                }

                // Generator should not have exited normally. 
                return LightExceptions.Throw(new RuntimeException("generator ignored GeneratorExit"));
            } catch (StopIterationException) {
                // Ignore, clear any stack frames we built up
            } catch (GeneratorExitException) {
                // Ignore, clear any stack frames we built up
            }
            return null;
        }

        public FunctionCode gi_code {
            get {
                return _function.__code__;
            }
        }

        public int gi_running {
            get {
                if (Active) {
                    return 1;
                }

                return 0;
            }
        }

        public TraceBackFrame gi_frame {
            get {
                return new TraceBackFrame(_function.Context, _function.Context.GlobalDict, new PythonDictionary(), gi_code);
            }
        }

        /// <summary>
        /// Gets the name of the function that produced this generator object.
        /// </summary>
        public string __name__ {
            get {
                return _function.__name__;
            }
        }

        #endregion

        #region Internal implementation details

        private int State {
            get {
                return _dataTuple.Item000;
            }
            set {
                _dataTuple.Item000 = value;
            }
        }

        private object CurrentValue {
            get {
                return _dataTuple.Item001;
            }
            set {
                _dataTuple.Item001 = value;
            }
        }

        private object FinalValue { get; set; }

        private MutableTuple<int, object> GetDataTuple() {
            MutableTuple<int, object> res = _data as MutableTuple<int, object>;
            if (res == null) {
                res = GetBigData(_data);
            }
            return res;
        }

        private static MutableTuple<int, object> GetBigData(MutableTuple data) {
            MutableTuple<int, object> smallGen;
            do {
                data = (MutableTuple)data.GetValue(0);
            } while ((smallGen = (data as MutableTuple<int, object>)) == null);

            return smallGen;
        }

        internal CodeContext Context {
            get {
                return _function.Context;
            }
        }

        internal PythonFunction Function {
            get {
                return _function;
            }
        }

        // Pep 342 says generators now have finalizers (__del__) that call Close()

        private void Finalizer() {
            // if there are no except or finally blocks then closing the
            // generator has no effect.
            if (CanSetSysExcInfo || ContainsTryFinally) {
                try {
                    // This may run the users generator.
                    object res = close(true);
                    Exception ex = LightExceptions.GetLightException(res);
                    if (ex != null) {
                        HandleFinalizerException(ex);
                    }
                } catch (Exception e) {
                    HandleFinalizerException(e);
                }
            }
        }

        private void HandleFinalizerException(Exception e) {
            // An unhandled exceptions on the finalizer could tear down the process, so catch it.

            // PEP says:
            //   If close() raises an exception, a traceback for the exception is printed to sys.stderr
            //   and further ignored; it is not propagated back to the place that
            //   triggered the garbage collection. 

            // Sample error message from CPython 2.5 looks like:
            //     Exception __main__.MyError: MyError() in <generator object at 0x00D7F6E8> ignored
            try {

                string message = "Exception in generator " + __repr__(Context) + " ignored";

                PythonOps.PrintWithDest(Context, Context.LanguageContext.SystemStandardError, message);
                PythonOps.PrintWithDest(Context, Context.LanguageContext.SystemStandardError, Context.LanguageContext.FormatException(e));
            } catch {
                // if stderr is closed then ignore any exceptions.
            }
        }

        bool IEnumerator.MoveNext() {
            if (Closed) {
                // avoid exception
                return false;
            }

            CheckSetActive();

            if (!CanSetSysExcInfo) {
                return MoveNextWorker();
            } else {
                _state.PrevException = PythonOps.CurrentExceptionState;
                PythonOps.CurrentExceptionState = _state;
                try {
                    return MoveNextWorker();
                } finally {
                    // A generator restores the sys.exc_info() status after each yield point.
                    PythonOps.CurrentExceptionState = _state.PrevException;
                    _state.PrevException = null;
                }
            }
        }

        private FunctionStack? fnStack = null;

        private void SaveFunctionStack(bool done) {
            if (!Context.LanguageContext.PythonOptions.Frames || Context.LanguageContext.EnableTracing) return;
            if (!done) {
                var stack = PythonOps.GetFunctionStack();
                var functionStack = stack[stack.Count - 1];
                Debug.Assert(functionStack.Context != null);
                functionStack.Frame = null; // don't keep the frame since f_back may be invalid
                stack.RemoveAt(stack.Count - 1);
                fnStack = functionStack;
            }
            else {
                fnStack = null;
            }
        }

        private void RestoreFunctionStack() {
            if (!Context.LanguageContext.PythonOptions.Frames) return;
            if (fnStack != null) {
                List<FunctionStack> stack = PythonOps.GetFunctionStack();
                stack.Add(fnStack.Value);
            }
        }

        /// <summary>
        /// Core implementation of IEnumerator.MoveNext()
        /// </summary>
        private bool MoveNextWorker() {
            bool ret = false;
            try {
                RestoreFunctionStack();
                try {
                    ret = GetNext();
                } finally {
                    Active = false;

                    SaveFunctionStack(!ret);

                    if (!ret) {
                        Close();
                    }
                }
            } catch (StopIterationException) {
                return false;
            }
            return ret;
        }

        /// <summary>
        /// Core implementation of Python's next() method.
        /// </summary>
        private object NextWorker() {
            // Generators can not be called re-entrantly.
            CheckSetActive();

            RestoreFunctionStack();

            _state.PrevException = PythonOps.CurrentExceptionState;
            PythonOps.CurrentExceptionState = _state;

            bool ret = false;
            try {
                // This calls into the delegate that has the real body of the generator.
                // The generator body here may:
                // 1. return an item: _next() returns true and 'next' is set to the next item in the enumeration.
                // 2. Exit normally: _next returns false.
                // 3. Exit with a StopIteration exception: for-loops and other enumeration consumers will 
                //    catch this and terminate the loop without propogating the exception.
                // 4. Exit via some other unhandled exception: This will close the generator, but the exception still propagates.
                //    _next does not return, so ret is left assigned to false (closed), which we detect in the finally.
                if (!(ret = GetNext())) {
                    FinalValue = CurrentValue;
                    CurrentValue = OperationFailed.Value;
                }
            } finally {
                PythonOps.CurrentExceptionState = _state.PrevException;
                _state.PrevException = null;

                Active = false;

                SaveFunctionStack(!ret);

                // If _next() returned false, or did not return (thus leaving ret assigned to its initial value of false), then
                // the body of the generator has exited and the generator is now closed.
                if (!ret) {
                    Close();
                }
            }

            return CurrentValue;
        }

        private void RestoreCurrentException(Exception save) {
            if (CanSetSysExcInfo) {
                PythonOps.RestoreCurrentException(save);
            }
        }

        private Exception SaveCurrentException() {
            if (CanSetSysExcInfo) {
                return PythonOps.SaveCurrentException();
            }
            return null;
        }

        private void CheckSetActive() {
            if (Active) {
                // A generator could catch this exception and continue executing, so this does
                // not necessarily close the generator.
                AlreadyExecuting();
            }
            Active = true;
        }

        private static void AlreadyExecuting() {
            throw PythonOps.ValueError("generator already executing");
        }

        /// <summary>
        /// Helper called from PythonOps after the yield statement
        /// Keepin this in a helper method:
        /// - reduces generated code size
        /// - allows better coupling with PythonGenerator.Throw()
        /// - avoids throws from emitted code (which can be harder to debug).
        /// </summary>
        /// <returns></returns>
        [LightThrowing]
        internal object CheckThrowableAndReturnSendValue() {
            // Since this method is called from the generator body's execution, the generator must be running 
            // and not closed.
            Debug.Assert(!Closed);

            if (_sendValue != null) {
                // Can't Send() and Throw() at the same time.
                Debug.Assert(_excInfo == null);

                return SwapValues();
            }
            return CheckThrowable();
        }

        private object SwapValues() {
            object sendValueBackup = _sendValue;
            _sendValue = null;
            return sendValueBackup;
        }

        /// <summary>
        /// Called to throw an exception set by Throw().
        /// </summary>
        [LightThrowing]
        private object CheckThrowable() {
            if (_excInfo != null) {
                return ThrowThrowable();
            }
            return null;
        }

        [LightThrowing]
        private object ThrowThrowable() {
            object[] throwableBackup = _excInfo;

            // Clear it so that any future Next()/MoveNext() call doesn't pick up the exception again.
            _excInfo = null;

            // This may invoke user code such as __init__, thus MakeException may throw. 
            // Since this is invoked from the generator's body, the generator can catch this exception. 
            return LightExceptions.Throw(PythonOps.MakeExceptionForGenerator(Context, throwableBackup[0], throwableBackup[1], throwableBackup[2], null));
        }

        private void Close() {
            Closed = true;
            // if we're closed the finalizer won't do anything, so suppress it.
            SuppressFinalize();
        }

        private void SuppressFinalize() {
            if (_finalizer != null) {
                _finalizer.Generator = null;
                _LastFinalizer = _finalizer;
            } else {
                // We must be on the finalizer thread, and being called from _finalizer.Finalize()
                Debug.Assert(Thread.CurrentThread.Name == null);
            }
        }

        private bool Closed {
            get {
                return (_flags & GeneratorFlags.Closed) != 0;
            }
            set {
                if (value) _flags |= GeneratorFlags.Closed;
                else _flags &= ~GeneratorFlags.Closed;
            }
        }

        /// <summary>
        /// True if the thread is currently inside the generator (ie, invoking the _next delegate).
        /// This can be used to enforce that a generator does not call back into itself. 
        /// Pep255 says that a generator should throw a ValueError if called reentrantly.
        /// </summary>
        private bool Active { get; set; }

        private bool GetNext() {
            _next(_data);
            return State != GeneratorRewriter.Finished;
        }

        internal bool CanSetSysExcInfo {
            get {
                return (_function.Flags & FunctionAttributes.CanSetSysExcInfo) != 0;
            }
        }

        internal bool ContainsTryFinally {
            get {
                return (_function.Flags & FunctionAttributes.ContainsTryFinally) != 0;
            }
        }

        #endregion

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            return string.Format("<generator object at {0}>", PythonOps.HexId(this));
        }

        #endregion

        [Flags]
        private enum GeneratorFlags {
            None,
            /// <summary>
            /// True if the generator has finished (is "closed"), else false.
            /// Python language spec mandates that calling Next on a closed generator gracefully throws a StopIterationException.
            /// This can never be reset.
            /// </summary>
            Closed = 0x01,
            /// <summary>
            /// True if the generator can set sys exc info and therefore needs exception save/restore.
            /// </summary>
            CanSetSysExcInfo = 0x04
        }


        #region IEnumerator Members

        object IEnumerator.Current {
            get { return CurrentValue; }
        }

        void IEnumerator.Reset() {
            throw new NotImplementedException();
        }

        #endregion

        #region IEnumerator<object> Members

        object IEnumerator<object>.Current {
            get { return CurrentValue; }
        }

        #endregion

        #region IDisposable Members

        void IDisposable.Dispose() {
            // nothing needed to dispose
            SuppressFinalize();
        }

        #endregion

        class GeneratorFinalizer {
            public PythonGenerator Generator;
            public GeneratorFinalizer(PythonGenerator generator) {
                Generator = generator;
            }

            ~GeneratorFinalizer() {
                var gen = Generator;
                if (gen != null) {
                    Debug.Assert(gen._finalizer == this);
                    // We set this to null to indicate that the GeneratorFinalizer has been finalized, 
                    // and should not be reused (ie saved in PythonGenerator._LastFinalizer)
                    gen._finalizer = null;

                    gen.Finalizer();
                }
            }
        }

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            // only present for better C# interop
            return this;
        }

        #endregion

        #region IWeakReferenceable Members

        WeakRefTracker IWeakReferenceable.GetWeakRef() {
            return _tracker;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            _tracker = value;
            return true;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            _tracker = value;
        }

        #endregion
    }
}
