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
using System.Threading;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
using SpecialName = System.Runtime.CompilerServices.SpecialNameAttribute;

[assembly: PythonModule("thread", typeof(IronPython.Modules.PythonThread))]
namespace IronPython.Modules {
    public static class PythonThread {
        public const string __doc__ = "Provides low level primitives for threading.";

        private static readonly object _stackSizeKey = new object();
        private static object _threadCountKey = new object();

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.SetModuleState(_stackSizeKey, 0);
            context.EnsureModuleException("threaderror", dict, "error", "thread");
        }
      
        #region Public API Surface

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType LockType = DynamicHelpers.GetPythonTypeFromType(typeof(@lock));

        [Documentation("start_new_thread(function, [args, [kwDict]]) -> thread id\nCreates a new thread running the given function")]
        public static object start_new_thread(CodeContext/*!*/ context, object function, object args, object kwDict) {
            PythonTuple tupArgs = args as PythonTuple;
            if (tupArgs == null) throw PythonOps.TypeError("2nd arg must be a tuple");

            Thread t = CreateThread(context, new ThreadObj(context, function, tupArgs, kwDict).Start);
            t.Start();

            return t.ManagedThreadId;
        }

        [Documentation("start_new_thread(function, args, [kwDict]) -> thread id\nCreates a new thread running the given function")]
        public static object start_new_thread(CodeContext/*!*/ context, object function, object args) {
            PythonTuple tupArgs = args as PythonTuple;
            if (tupArgs == null) throw PythonOps.TypeError("2nd arg must be a tuple");

            Thread t = CreateThread(context, new ThreadObj(context, function, tupArgs, null).Start);
            t.IsBackground = true;
            t.Start();

            return t.ManagedThreadId;
        }

#if FEATURE_THREAD && !SILVERLIGHT && !WP75
        /// <summary>
        /// Stops execution of Python or other .NET code on the main thread.  If the thread is
        /// blocked in native code the thread will be interrupted after it returns back to Python
        /// or other .NET code.  
        /// </summary>
        public static void interrupt_main(CodeContext context) {
            var thread = context.LanguageContext.MainThread;
            if (thread != null) {
                thread.Abort(new KeyboardInterruptException(""));
            } else {
                throw PythonOps.SystemError("no main thread has been registered");
            }
        }
#endif

        public static void exit() {
            PythonOps.SystemExit();
        }

        [Documentation("allocate_lock() -> lock object\nAllocates a new lock object that can be used for synchronization")]
        public static object allocate_lock() {
            return new @lock();
        }

        public static object get_ident() {
            return Thread.CurrentThread.ManagedThreadId;
        }

        public static int stack_size(CodeContext/*!*/ context) {
            return GetStackSize(context);
        }

        public static int stack_size(CodeContext/*!*/ context, int size) {
            if (size < 32 * 1024 && size != 0) {
                throw PythonOps.ValueError("size too small: {0}", size);
            }

            int oldSize = GetStackSize(context);
            
            SetStackSize(context, size);
            
            return oldSize;
        }

        // deprecated synonyms, wrappers over preferred names...
        [Documentation("start_new(function, [args, [kwDict]]) -> thread id\nCreates a new thread running the given function")]
        public static object start_new(CodeContext context, object function, object args) {
            return start_new_thread(context, function, args);
        }

        public static void exit_thread() {
            exit();
        }

        public static object allocate() {
            return allocate_lock();
        }

        public static int _count(CodeContext context) {
            return (int)context.LanguageContext.GetOrCreateModuleState<object>(_threadCountKey, () => 0);
        }

        #endregion

        [PythonType, PythonHidden]
        public class @lock {
            private AutoResetEvent blockEvent;
            private Thread curHolder;

            public object __enter__() {
                acquire();
                return this;
            }

            public void __exit__(CodeContext/*!*/ context, params object[] args) {
                release(context);
            }
            
            public object acquire() {
                return (acquire(ScriptingRuntimeHelpers.True));
            }

            public object acquire(object waitflag) {
                bool fWait = PythonOps.IsTrue(waitflag);
                for (; ; ) {
                    if (Interlocked.CompareExchange<Thread>(ref curHolder, Thread.CurrentThread, null) == null) {
                        return ScriptingRuntimeHelpers.True;
                    }
                    if (!fWait) {
                        return ScriptingRuntimeHelpers.False;
                    }
                    if (blockEvent == null) {
                        // try again in case someone released us, checked the block
                        // event and discovered it was null so they didn't set it.
                        CreateBlockEvent();
                        continue;
                    }
                    blockEvent.WaitOne();
                    GC.KeepAlive(this);
                }
            }

            public void release(CodeContext/*!*/ context, params object[] param) {
                release(context);
            }

            public void release(CodeContext/*!*/ context) {
                if (Interlocked.Exchange<Thread>(ref curHolder, null) == null) {
                    throw PythonExceptions.CreateThrowable((PythonType)PythonContext.GetContext(context).GetModuleState("threaderror"), "lock isn't held", null);
                }
                if (blockEvent != null) {
                    // if this isn't set yet we race, it's handled in Acquire()
                    blockEvent.Set();
                    GC.KeepAlive(this);
                }
            }

            public bool locked() {
                return curHolder != null;
            }

            private void CreateBlockEvent() {
                AutoResetEvent are = new AutoResetEvent(false);
                if (Interlocked.CompareExchange<AutoResetEvent>(ref blockEvent, are, null) != null) {
                    ((IDisposable)are).Dispose();
                }
            }
        }

        #region Internal Implementation details

        private static Thread CreateThread(CodeContext/*!*/ context, ThreadStart start) {
#if !SILVERLIGHT && !WP75 && !NETSTANDARD
            int size = GetStackSize(context);
            return (size != 0) ? new Thread(start, size) : new Thread(start);
#else
            return new Thread(start);
#endif
        }

        private class ThreadObj {
            private readonly object _func, _kwargs;
            private readonly PythonTuple _args;
            private readonly CodeContext _context;

            public ThreadObj(CodeContext context, object function, PythonTuple args, object kwargs) {
                Debug.Assert(args != null);
                _func = function;
                _kwargs = kwargs;
                _args = args;
                _context = context;
            }

            public void Start() {
                lock (_threadCountKey) {
                    int startCount = (int)_context.LanguageContext.GetOrCreateModuleState<object>(_threadCountKey, () => 0);
                    _context.LanguageContext.SetModuleState(_threadCountKey, startCount + 1);
                }
                try {
#pragma warning disable 618 // TODO: obsolete
                    if (_kwargs != null) {
                        PythonOps.CallWithArgsTupleAndKeywordDictAndContext(_context, _func, ArrayUtils.EmptyObjects, ArrayUtils.EmptyStrings, _args, _kwargs);
                    } else {
                        PythonOps.CallWithArgsTuple(_func, ArrayUtils.EmptyObjects, _args);
                    }
#pragma warning restore 618
                } catch (SystemExitException) {
                    // ignore and quit
                } catch (Exception e) {
                    PythonOps.PrintWithDest(_context, PythonContext.GetContext(_context).SystemStandardError, "Unhandled exception on thread");
                    string result = _context.LanguageContext.FormatException(e);
                    PythonOps.PrintWithDest(_context, PythonContext.GetContext(_context).SystemStandardError, result);
                } finally {
                    lock (_threadCountKey) {
                        int curCount = (int)_context.LanguageContext.GetModuleState(_threadCountKey);
                        _context.LanguageContext.SetModuleState(_threadCountKey, curCount - 1);
                    }
                }
            }
        }

        #endregion

        private static int GetStackSize(CodeContext/*!*/ context) {
            return (int)PythonContext.GetContext(context).GetModuleState(_stackSizeKey);
        }

        private static void SetStackSize(CodeContext/*!*/ context, int stackSize) {
            PythonContext.GetContext(context).SetModuleState(_stackSizeKey, stackSize);
        }

        [PythonType]
        public class _local {
            private readonly PythonDictionary/*!*/ _dict = new PythonDictionary(new ThreadLocalDictionaryStorage());

            #region Custom Attribute Access

            [SpecialName]
            public object GetCustomMember(string name) {
                return _dict.get(name, OperationFailed.Value);
            }

            [SpecialName]
            public void SetMemberAfter(string name, object value) {
                _dict[name] = value;
            }

            [SpecialName]
            public void DeleteMember(string name) {
                _dict.__delitem__(name);
            }

            #endregion

            public PythonDictionary/*!*/ __dict__ {
                get {
                    return _dict;
                }
            }

            #region Dictionary Storage

            /// <summary>
            /// Provides a dictionary storage implementation whose storage is local to
            /// the thread.
            /// </summary>
            private class ThreadLocalDictionaryStorage : DictionaryStorage {
                private readonly Microsoft.Scripting.Utils.ThreadLocal<CommonDictionaryStorage> _storage = new Microsoft.Scripting.Utils.ThreadLocal<CommonDictionaryStorage>();

                public override void Add(ref DictionaryStorage storage, object key, object value) {
                    GetStorage().Add(key, value);
                }

                public override bool Contains(object key) {
                    return GetStorage().Contains(key);
                }

                public override bool Remove(ref DictionaryStorage storage, object key) {
                    return GetStorage().Remove(ref storage, key);
                }

                public override bool TryGetValue(object key, out object value) {
                    return GetStorage().TryGetValue(key, out value);
                }

                public override int Count {
                    get { return GetStorage().Count; }
                }

                public override void Clear(ref DictionaryStorage storage) {
                    GetStorage().Clear(ref storage);
                }

                public override List<KeyValuePair<object, object>>/*!*/ GetItems() {
                    return GetStorage().GetItems();
                }

                private CommonDictionaryStorage/*!*/ GetStorage() {
                    return _storage.GetOrCreate(() => new CommonDictionaryStorage());
                }
            }

            #endregion
        }
    }
}
