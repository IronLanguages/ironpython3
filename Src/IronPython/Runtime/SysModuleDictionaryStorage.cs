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
using System.Text;
using IronPython.Modules;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    /// <summary>
    /// Optimized storage for setting exc_type, exc_value, and exc_traceback.
    /// 
    /// This optimization can go away in Python 3.0 when these attributes are no longer used.
    /// </summary>
    class SysModuleDictionaryStorage : ModuleDictionaryStorage {
        private ExceptionState _exceptionState = null;
        private object _excType, _excValue, _excTraceback;
        private ExceptionStateFlags _setValues = ExceptionStateFlags.Traceback | ExceptionStateFlags.Type | ExceptionStateFlags.Value, _removedValues;

        public SysModuleDictionaryStorage()
            : base(typeof(SysModule)) {
        }

        public override void Add(ref DictionaryStorage storage, object key, object value) {
            lock (this) {
                AddNoLock(ref storage, key, value);
            }
        }

        public override void AddNoLock(ref DictionaryStorage storage, object key, object value) {            
            // add always needs to check...
            string strKey = key as string;
            if (strKey != null) {
                switch (strKey) {
                    case "exc_type":
                        _setValues |= ExceptionStateFlags.Type;
                        _removedValues &= ~ExceptionStateFlags.Type;
                        _excType = value;
                        break;
                    case "exc_value":
                        _setValues |= ExceptionStateFlags.Value;
                        _removedValues &= ~ExceptionStateFlags.Value;
                        _excValue = value;
                        break;
                    case "exc_traceback":
                        _setValues |= ExceptionStateFlags.Traceback;
                        _removedValues &= ~ExceptionStateFlags.Traceback;
                        _excTraceback = value;
                        break;
                }
            }
            
            base.AddNoLock(ref storage, key, value);
        }

        public override bool Remove(ref DictionaryStorage storage, object key) {
            // check the strKey only if we have some exception info set
            ExceptionState exState = _exceptionState;
            if (exState != null || _setValues != 0) {
                string strKey = key as string;
                if (strKey != null) {
                    switch (strKey) {
                        case "exc_type":
                            lock (this) {
                                _excType = null;
                                _setValues &= ~ExceptionStateFlags.Type;
                                _removedValues |= ExceptionStateFlags.Type;
                            }
                            break;
                        case "exc_value":
                            lock (this) {
                                _excValue = null;
                                _setValues &= ~ExceptionStateFlags.Value;
                                _removedValues |= ExceptionStateFlags.Value;
                            }
                            break;
                        case "exc_traceback":
                            lock (this) {
                                _excTraceback = null;
                                _setValues &= ~ExceptionStateFlags.Traceback;
                                _removedValues |= ExceptionStateFlags.Traceback;
                            }
                            break;
                    }
                }
            }

            return base.Remove(ref storage, key);
        }

        public override bool TryGetValue(object key, out object value) {
            ExceptionState exState = _exceptionState;
            // check the strKey only if we have some exception info set
            if (exState != null || _setValues != 0) {
                string strKey = key as string;
                if (strKey != null && TryGetExcValue(exState, strKey, out value)) {
                    return true;                    
                }
            }

            return base.TryGetValue(key, out value);
        }

        private bool TryGetExcValue(ExceptionState exState, string strKey, out object value) {
            switch (strKey) {
                case "exc_type":
                    lock (this) {
                        if ((_removedValues & ExceptionStateFlags.Type) == 0) {
                            if ((_setValues & ExceptionStateFlags.Type) != 0) {
                                value = _excType;
                            } else {
                                value = exState.Type;
                            }
                            return true;
                        }
                    }
                    break;
                case "exc_value":
                    lock (this) {
                        if ((_removedValues & ExceptionStateFlags.Value) == 0) {
                            if ((_setValues & ExceptionStateFlags.Value) != 0) {
                                value = _excValue;
                            } else {
                                value = exState.Value;
                            }
                            return true;
                        }
                    }
                    break;
                case "exc_traceback":
                    lock (this) {
                        if ((_removedValues & ExceptionStateFlags.Traceback) == 0) {
                            if ((_setValues & ExceptionStateFlags.Traceback) != 0) {
                                value = _excTraceback;
                            } else {
                                _excTraceback = CreateTraceBack(exState);
                                _setValues |= ExceptionStateFlags.Traceback;
                                value = _excTraceback;
                            }
                            return true;
                        }
                    }
                    break;
            }
            value = null;
            return false;
        }

        public override List<KeyValuePair<object, object>> GetItems() {
            var res = base.GetItems();
            object value;
            if (TryGetValue("exc_traceback", out value)) {
                res.Add(new KeyValuePair<object, object>("exc_traceback", value));
            }
            if (TryGetValue("exc_type", out value)) {
                res.Add(new KeyValuePair<object, object>("exc_type", value));
            }
            if (TryGetValue("exc_value", out value)) {
                res.Add(new KeyValuePair<object, object>("exc_value", value));
            }

            return res;
        }

        private static object CreateTraceBack(ExceptionState list) {
            return PythonOps.CreateTraceBack(list.ClrException, list.Traceback, list.FrameCount);
        }

        public override void Clear(ref DictionaryStorage storage) {
            lock (this) {
                _exceptionState = null;
                _setValues = 0;
                _removedValues = 0;
                _excTraceback = _excType = _excValue = null;
                base.Clear(ref storage);
            }
        }

        [Flags]
        enum ExceptionStateFlags {
            Type     = 0x01,
            Value    = 0x02,
            Traceback = 0x04
        }

        class ExceptionState {
            public readonly object Type;
            public readonly object Value;
            public readonly List<DynamicStackFrame> Traceback;
            public readonly int FrameCount;
            public readonly Exception ClrException;
            
            public ExceptionState(Exception clrException, object type, object value, List<DynamicStackFrame> traceback) {
                Type = type;
                Value = value;
                Traceback = traceback;
                ClrException = clrException;
                if (traceback != null) {
                    FrameCount = traceback.Count;
                }
            }
        }

        public void UpdateExceptionInfo(Exception clrException, object type, object value, List<DynamicStackFrame> traceback) {
            lock (this) {
                _exceptionState = new ExceptionState(clrException, type, value, traceback);
                _setValues = _removedValues = 0;
            }
        }

        public void UpdateExceptionInfo(object type, object value, object traceback) {
            lock (this) {
                _exceptionState = new ExceptionState(null, type, value, null);
                _excTraceback = traceback;
                _setValues = ExceptionStateFlags.Traceback;
                _removedValues = 0;
            }
        }

        public void ExceptionHandled() {
            lock (this) {
                _setValues = ExceptionStateFlags.Traceback | ExceptionStateFlags.Type | ExceptionStateFlags.Value;
                _removedValues = 0;
                _exceptionState = null;
                _excTraceback = _excType = _excValue = null;
            }
        }

        public override void Reload() {
            base.Reload();
        }
    }

    
}
