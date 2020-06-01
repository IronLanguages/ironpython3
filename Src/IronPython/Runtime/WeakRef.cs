// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime {

    /// <summary>
    /// single finalizable instance used to track and deliver all the 
    /// callbacks for a single object that has been weakly referenced by
    /// one or more references and proxies.  The reference to this object
    /// is held in objects that implement IWeakReferenceable.
    /// </summary>
    public class WeakRefTracker {
        private readonly struct CallbackInfo {
            private readonly object _callback;
            private readonly WeakHandle _longRef;
            private readonly WeakHandle _shortRef;
            
            public CallbackInfo(object callback, object weakRef) {
                _callback = callback;
                // we need a short ref & a long ref to deal with the case
                // when what we're finalizing is cyclic trash.  (see test_weakref
                // test_callbacks_on_callback).  If the short ref is dead, but the
                // long ref still lives then it means we'the weakref is in the
                // finalization queue and we shouldn't run it's callback - we're
                // just unlucky and are getting ran first.
                _longRef = new WeakHandle(weakRef, true);
                _shortRef = new WeakHandle(weakRef, false);
            }

            public object Callback {
                get { return _callback; }
            }

            public object WeakRef {
                get {
                    object longRefTarget = _longRef.Target;
                    object shortRefTarget = _shortRef.Target;

                    // already in finalization?
                    if (shortRefTarget != longRefTarget)
                        return null;

                    return longRefTarget;
                }
            }

            public void Free() {
                _longRef.Free();
                _shortRef.Free();
            }
        }

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly List<CallbackInfo> _callbacks = new List<CallbackInfo>(1);

        public WeakRefTracker(IWeakReferenceable target) {
        }

        public WeakRefTracker(IWeakReferenceable target, object callback, object weakRef) : this(target) {
            ChainCallback(callback, weakRef);
        }

        public void ChainCallback(object callback, object weakRef) {
            _lock.EnterWriteLock();
            try {
                _callbacks.Add(new CallbackInfo(callback, weakRef));
            } finally {
                _lock.ExitWriteLock();
            }
        }

        public int HandlerCount {
            get {
                _lock.EnterReadLock();
                try {
                    return _callbacks.Count;
                } finally {
                    _lock.ExitReadLock();
                }
            }
        }

        public void RemoveHandlerAt(int index) {
            _lock.EnterWriteLock();
            try {
                _callbacks.RemoveAt(index);
            } finally {
                _lock.ExitWriteLock();
            }
        }

        public void RemoveHandler(object o) {
            _lock.EnterWriteLock();
            try {
                for (int i = 0; i < _callbacks.Count; i++) {
                    if (_callbacks[i].WeakRef == o) {
                        _callbacks.RemoveAt(i);
                        break;
                    }
                }
            } finally {
                _lock.ExitWriteLock();
            }

        }

        internal bool Contains(object callback, object weakref) {
            _lock.EnterReadLock();
            try {
                return _callbacks.Any(o => o.Callback == callback && o.WeakRef == weakref);
            } finally {
                _lock.ExitReadLock();
            }
        }

        public object GetHandlerCallback(int index) {
            _lock.EnterReadLock();
            try {
                return _callbacks[index].Callback;
            } finally {
                _lock.ExitReadLock();
            }
        }

        public object GetWeakRef(int index) {
            _lock.EnterReadLock();
            try {
                return _callbacks[index].WeakRef;
            } finally {
                _lock.ExitReadLock();
            }
        }

        ~WeakRefTracker() {
            _lock.EnterWriteLock();
            try {
                // callbacks are delivered last registered to first registered.
                for (int i = _callbacks.Count - 1; i >= 0; i--) {

                    CallbackInfo ci = _callbacks[i];
                    try {
                        if (ci.Callback != null) {
                            // a little ugly - we only run callbacks that aren't a part
                            // of cyclic trash.  but classes use a single field for
                            // finalization & GC - and that's always cyclic, so we need to special case it.
                            if (ci.Callback is InstanceFinalizer fin) {
                                // Going through PythonCalls / Rules requires the types be public.
                                // Explicit check so that we can keep InstanceFinalizer internal.
                                fin.CallDirect(DefaultContext.Default);

                            } else {
                                // Non-instance finalizer goes through normal call mechanism.
                                object weakRef = ci.WeakRef;
                                if (weakRef != null)
                                    PythonCalls.Call(ci.Callback, weakRef);
                            }
                        }
                    } catch (Exception) {
                        // TODO (from Python docs):
                        // Exceptions raised by the callback will be noted on the standard error output, 
                        // but cannot be propagated; they are handled in exactly the same way as exceptions 
                        // raised from an object’s __del__() method.
                    }

                    ci.Free();
                }
            } finally {
                _lock.ExitWriteLock();
            }
        }
    }


    /// <summary>
    /// Finalizable object used to hook up finalization calls for OldInstances.
    /// 
    /// We create one of these each time an object w/ a finalizer gets created.  The
    /// only reference to this object is the instance so when that goes out of context
    /// this does as well and this will get finalized.  
    /// </summary>
    internal sealed class InstanceFinalizer {
        private readonly object _instance;

        internal InstanceFinalizer(CodeContext/*!*/ context, object inst) {
            Debug.Assert(inst != null);
            _instance = inst;
        }

        // This corresponds to a __del__ method on a class. 
        // Callers will do a direct invoke so that instanceFinalizer can stay non-public.
        internal object CallDirect(CodeContext context) {
            PythonTypeOps.TryInvokeUnaryOperator(context, _instance, "__del__", out _);
            return null;
        }
    }
}
