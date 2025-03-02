// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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

[assembly: PythonModule("_thread", typeof(IronPython.Modules.PythonThread))]
namespace IronPython.Modules {
    public static class PythonThread {
        public const string __doc__ = "Provides low level primitives for threading.";

        private static readonly object _stackSizeKey = new object();
        private static object _threadCountKey = new object();
        [ThreadStatic] private static List<@lock> _sentinelLocks;

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.SetModuleState(_stackSizeKey, 0);
            context.EnsureModuleException("threaderror", dict, "error", "thread");
        }

        #region Public API Surface

        public static double TIMEOUT_MAX = Math.Floor(TimeSpan.MaxValue.TotalSeconds);

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

        /// <summary>
        /// Stops execution of Python or other .NET code on the main thread.  If the thread is
        /// blocked in native code the thread will be interrupted after it returns back to Python
        /// or other .NET code.  
        /// </summary>
        public static void interrupt_main(CodeContext context) {
            var thread = context.LanguageContext.MainThread;
            if (thread != null) {
#pragma warning disable SYSLIB0006 // Thread.Abort is not supported and throws PlatformNotSupportedException on .NET Core.
                thread.Abort(new KeyboardInterruptException(""));
#pragma warning restore SYSLIB0006
            } else {
                throw PythonOps.SystemError("no main thread has been registered");
            }
        }

        public static void exit() {
            PythonOps.SystemExit();
        }

        [Documentation("allocate_lock() -> lock object\nAllocates a new lock object that can be used for synchronization")]
        public static object allocate_lock() {
            return new @lock();
        }

        public static object get_ident() {
            return Environment.CurrentManagedThreadId;
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

        [Documentation("_set_sentinel() -> lock\n\nSet a sentinel lock that will be released when the current thread\nstate is finalized (after it is untied from the interpreter).\n\nThis is a private API for the threading module.")]
        public static object _set_sentinel(CodeContext context) {
            if (_sentinelLocks == null) {
                _sentinelLocks = new List<@lock>();
            }
            var obj = new @lock();
            _sentinelLocks.Add(obj);
            return obj;
        }

        #endregion

#nullable enable

        [PythonType, PythonHidden]
        public sealed class @lock {
            private AutoResetEvent? blockEvent;
            private Thread? curHolder;

            public object __enter__() {
                acquire();
                return this;
            }

            public void __exit__(CodeContext/*!*/ context, [NotNone] params object[] args) {
                release(context);
            }

            public bool acquire(bool blocking = true, double timeout = -1) {
                var timespan = Timeout.InfiniteTimeSpan;

                if (timeout != -1) {
                    if (!blocking) throw PythonOps.ValueError("can't specify a timeout for a non-blocking call");
                    if (timeout < 0) throw PythonOps.ValueError("timeout value must be a non-negative number");
                    timespan = TimeSpan.FromSeconds(timeout);
                }

                for (; ; ) {
                    if (Interlocked.CompareExchange(ref curHolder, Thread.CurrentThread, null) is null) {
                        return true;
                    }
                    if (!blocking) {
                        return false;
                    }
                    if (blockEvent == null) {
                        // try again in case someone released us, checked the block
                        // event and discovered it was null so they didn't set it.
                        CreateBlockEvent();
                        continue;
                    }
                    if (!blockEvent.WaitOne(timespan)) {
                        return false;
                    }
                    GC.KeepAlive(this);
                }
            }

            public void release(CodeContext/*!*/ context) {
                if (Interlocked.Exchange(ref curHolder, null) is null) {
                    throw PythonOps.RuntimeError("release unlocked lock");
                }
                if (blockEvent != null) {
                    // if this isn't set yet we race, it's handled in Acquire()
                    blockEvent.Set();
                    GC.KeepAlive(this);
                }
            }

            public bool locked()
                => curHolder is not null;

            public string __repr__() {
                if (curHolder is null) {
                    return $"<unlocked _thread.lock object at 0x{IdDispenser.GetId(this):X16}>";
                }
                return $"<locked _thread.lock object at 0x{IdDispenser.GetId(this):X16}>";
            }

            private void CreateBlockEvent() {
                AutoResetEvent are = new AutoResetEvent(false);
                if (Interlocked.CompareExchange(ref blockEvent, are, null) is not null) {
                    are.Close();
                }
            }
        }

        [PythonType]
        public sealed class RLock {
            private AutoResetEvent? blockEvent;
            private Thread? curHolder;
            private int count;

            public object __enter__() {
                acquire();
                return this;
            }

            public void __exit__(CodeContext/*!*/ context, [NotNone] params object[] args) {
                release();
            }

            public bool acquire(bool blocking = true, double timeout = -1) {
                var timespan = Timeout.InfiniteTimeSpan;

                if (timeout != -1) {
                    if (!blocking) throw PythonOps.ValueError("can't specify a timeout for a non-blocking call");
                    if (timeout < 0) throw PythonOps.ValueError("timeout value must be a non-negative number");
                    timespan = TimeSpan.FromSeconds(timeout);
                }

                var currentThread = Thread.CurrentThread;

                for (; ; ) {
                    var previousThread = Interlocked.CompareExchange(ref curHolder, currentThread, null);
                    if (previousThread == currentThread) {
                        count++;
                        return true;
                    }
                    if (previousThread is null) {
                        count = 1;
                        return true;
                    }
                    if (!blocking) {
                        return false;
                    }
                    if (blockEvent is null) {
                        // try again in case someone released us, checked the block
                        // event and discovered it was null so they didn't set it.
                        CreateBlockEvent();
                        continue;
                    }
                    if (!blockEvent.WaitOne(timespan)) {
                        return false;
                    }
                    GC.KeepAlive(this);
                }
            }

            public void release() {
                var currentThread = Thread.CurrentThread;

                if (curHolder != currentThread) {
                    throw PythonOps.RuntimeError("cannot release un-acquired lock");
                }
                if (--count > 0) {
                    return;
                }

                if (Interlocked.Exchange(ref curHolder, null) is null) {
                    throw PythonOps.RuntimeError("release unlocked lock");
                }
                if (blockEvent is not null) {
                    // if this isn't set yet we race, it's handled in acquire()
                    blockEvent.Set();
                    GC.KeepAlive(this);
                }
            }

            public string __repr__() {
                if (curHolder is null) {
                    return $"<unlocked _thread.RLock object owner=0 count=0 at 0x{IdDispenser.GetId(this):X16}>";
                }
                return $"<locked _thread.RLock object owner={curHolder?.ManagedThreadId} count={count} at 0x{IdDispenser.GetId(this):X16}>";
            }

            public void _acquire_restore([NotNone] PythonTuple state) {
                acquire();
                count = (int)state[0]!;
                curHolder = (Thread?)state[1];
            }

            public PythonTuple _release_save() {
                var count = Interlocked.Exchange(ref this.count, 0);
                if (count == 0) {
                    throw PythonOps.RuntimeError("cannot release un-acquired lock");
                }

                // release
                var owner = Interlocked.Exchange(ref curHolder, null);
                blockEvent?.Set();

                return PythonTuple.MakeTuple(count, owner);
            }

            public bool _is_owned()
                => curHolder == Thread.CurrentThread;

            private void CreateBlockEvent() {
                AutoResetEvent are = new AutoResetEvent(false);
                if (Interlocked.CompareExchange(ref blockEvent, are, null) != null) {
                    are.Close();
                }
            }
        }

#nullable restore

        #region Internal Implementation details

        private static Thread CreateThread(CodeContext/*!*/ context, ThreadStart start) {
            int size = GetStackSize(context);
            return (size != 0) ? new Thread(start, size) : new Thread(start);
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
                        PythonOps.CallWithArgsTupleAndKeywordDictAndContext(_context, _func, [], [], _args, _kwargs);
                    } else {
                        PythonOps.CallWithArgsTuple(_func, [], _args);
                    }
#pragma warning restore 618
                } catch (SystemExitException) {
                    // ignore and quit
                } catch (Exception e) {
                    PythonOps.PrintWithDest(_context, _context.LanguageContext.SystemStandardError, "Unhandled exception on thread");
                    string result = _context.LanguageContext.FormatException(e);
                    PythonOps.PrintWithDest(_context, _context.LanguageContext.SystemStandardError, result);
                } finally {
                    lock (_threadCountKey) {
                        int curCount = (int)_context.LanguageContext.GetModuleState(_threadCountKey);
                        _context.LanguageContext.SetModuleState(_threadCountKey, curCount - 1);
                    }

                    // release sentinel locks if locked.
                    if (_sentinelLocks != null) {
                        foreach (var obj in _sentinelLocks) {
                            if (obj.locked()) {
                                obj.release(_context);
                            }
                        }
                        _sentinelLocks.Clear();
                    }
                }
            }
        }
        #endregion

        private static int GetStackSize(CodeContext/*!*/ context) {
            return (int)context.LanguageContext.GetModuleState(_stackSizeKey);
        }

        private static void SetStackSize(CodeContext/*!*/ context, int stackSize) {
            context.LanguageContext.SetModuleState(_stackSizeKey, stackSize);
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

                public override DictionaryStorage AsMutable(ref DictionaryStorage storage) => this;

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
