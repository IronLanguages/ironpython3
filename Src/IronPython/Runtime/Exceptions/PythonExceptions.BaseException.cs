// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Exceptions {
    public static partial class PythonExceptions {
        /// <summary>
        /// Base class for all Python exception objects.
        /// 
        /// When users throw exceptions they typically throw an exception which is
        /// a subtype of this.  A mapping is maintained between Python exceptions
        /// and .NET exceptions and a corresponding .NET exception is thrown which
        /// is associated with the Python exception.  This class represents the
        /// base class for the Python exception hierarchy.  
        /// 
        /// Users can catch exceptions rooted in either hierarchy.  The hierarchy
        /// determines whether the user catches the .NET exception object or the 
        /// Python exception object.
        /// 
        /// Most built-in Python exception classes are actually instances of the BaseException
        /// class here.  This is important because in CPython the exceptions do not 
        /// add new members and therefore their layouts are compatible for multiple
        /// inheritance.  The exceptions to this rule are the classes which define 
        /// their own fields within their type, therefore altering their layout:
        ///     EnvironmentError
        ///     SyntaxError
        ///         IndentationError     (same layout as SyntaxError)
        ///         TabError             (same layout as SyntaxError)
        ///     SystemExit
        ///     UnicodeDecodeError
        ///     UnicodeEncodeError
        ///     UnicodeTranslateError
        ///     
        /// These exceptions cannot be combined in multiple inheritance, e.g.:
        ///     class foo(EnvironmentError, IndentationError): pass
        ///     
        /// fails but they can be combined with anything which is just a BaseException:
        ///     class foo(UnicodeDecodeError, SystemError): pass
        ///     
        /// Therefore the majority of the classes are just BaseException instances with a 
        /// custom PythonType object.  The specialized ones have their own .NET class
        /// which inherits from BaseException.  User defined exceptions likewise inherit
        /// from this and have their own .NET class.
        /// </summary>
        [PythonType("BaseException"), DynamicBaseType, Serializable]
        public class BaseException : ICodeFormattable, IPythonObject, IDynamicMetaObjectProvider, IWeakReferenceable {
            private PythonType/*!*/ _type;          // the actual Python type of the Exception object
            private object _message = String.Empty; // the message object, cached at __init__ time, not updated on args assignment
            private PythonTuple _args;              // the tuple of args provided at creation time
            private PythonDictionary _dict;    // the dictionary for extra values, created on demand
            private System.Exception _clrException; // the cached CLR exception that is thrown
            private object[] _slots;                // slots, only used for storage of our weak reference.

            private BaseException _cause;
            private BaseException _context;
            private TraceBack _traceback;
            private bool _tracebackSet;

            public static string __doc__ = "Common base class for all non-exit exceptions.";

            #region Public API Surface

            public BaseException(PythonType/*!*/ type) {
                ContractUtils.RequiresNotNull(type, nameof(type));

                _type = type;
            }

            public static object __new__(PythonType/*!*/ cls, params object[] args\u00F8) {
                if (cls.UnderlyingSystemType == typeof(BaseException)) {
                    return new BaseException(cls);
                }
                return Activator.CreateInstance(cls.UnderlyingSystemType, cls);
            }

            public static object __new__(PythonType/*!*/ cls, [ParamDictionary]IDictionary<object, object> kwArgs\u00F8, params object[] args\u00F8) {
                if (cls.UnderlyingSystemType == typeof(BaseException)) {
                    return new BaseException(cls);
                }

                return Activator.CreateInstance(cls.UnderlyingSystemType, cls);
            }

            /// <summary>
            /// Initializes the Exception object with an unlimited number of arguments
            /// </summary>
            public virtual void __init__(params object[] args\u00F8) {
                _args = PythonTuple.MakeTuple(args\u00F8 ?? new object[] { null });
                if (_args.__len__() == 1) {
                    _message = _args[0];
                }
            }

            /// <summary>
            /// Gets or sets the arguments used for creating the exception
            /// </summary>
            public PythonTuple/*!*/ args {
                get {
                    return _args ?? PythonTuple.EMPTY;
                }
                set { _args = PythonTuple.Make(value); }
            }

            public object with_traceback(TraceBack tb) {
                __traceback__ = tb;
                return this;
            }

            /// <summary>
            /// Returns a tuple of (type, (arg0, ..., argN)) for implementing pickling/copying
            /// </summary>
            public virtual object/*!*/ __reduce__() {
                if (_dict.Count > 0)
                    return PythonTuple.MakeTuple(DynamicHelpers.GetPythonType(this), args, _dict);
                return PythonTuple.MakeTuple(DynamicHelpers.GetPythonType(this), args);
            }

            /// <summary>
            /// Returns a tuple of (type, (arg0, ..., argN)) for implementing pickling/copying
            /// </summary>
            public virtual object/*!*/ __reduce_ex__(int protocol) {
                return __reduce__();
            }

            /// <summary>
            /// Gets or sets the dictionary which is used for storing members not declared to have space reserved
            /// within the exception object.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
            public PythonDictionary/*!*/ __dict__ {
                get {
                    EnsureDict();

                    return _dict;
                }
                set {
                    if (_dict == null) {
                        throw PythonOps.TypeError("__dict__ must be a dictionary");
                    }

                    _dict = value;
                }
            }

            /// <summary>
            /// Updates the exception's state (dictionary) with the new values
            /// </summary>
            public void __setstate__(PythonDictionary state) {
                foreach (KeyValuePair<object, object> pair in state) {
                    __dict__[pair.Key] = pair.Value;
                }
            }

            /// <summary>
            /// Gets the CLR exception associated w/ this Python exception.  Not visible
            /// until a .NET namespace is imported.
            /// </summary>
            public Exception/*!*/ clsException {
                [PythonHidden]
                get {
                    return GetClrException();
                }
                internal set {
                    _clrException = value;
                }
            }

            public override string/*!*/ ToString() {
                if (_args == null) return string.Empty;
                if (_args.__len__() == 0) return String.Empty;
                if (_args.__len__() == 1) {
                    string str;
                    Extensible<string> extStr;

                    if ((str = _args[0] as string) != null) {
                        return str;
                    }

                    if ((extStr = _args[0] as Extensible<string>) != null) {
                        return extStr.Value;
                    }

                    return PythonOps.ToString(_args[0]);
                }

                return _args.ToString();
            }

            #endregion

            #region Member access operators

            /// <summary>
            /// Provides custom member lookup access that fallbacks to the dictionary
            /// </summary>
            [SpecialName]
            public object GetBoundMember(string name) {
                if (_dict != null) {
                    object res;
                    if (_dict.TryGetValue(name, out res)) {
                        return res;
                    }
                }

                return OperationFailed.Value;
            }

            /// <summary>
            /// Provides custom member assignment which stores values in the dictionary
            /// </summary>
            [SpecialName]
            public void SetMemberAfter(string name, object value) {
                EnsureDict();

                _dict[name] = value;
            }

            /// <summary>
            /// Provides custom member deletion which deletes values from the dictionary
            /// or allows clearing 'message'.
            /// </summary>
            [SpecialName]
            public bool DeleteCustomMember(string name) {
                if (name == "message") {
                    _message = null;
                    return true;
                }

                if (_dict == null) return false;

                return _dict.Remove(name);
            }

            private void EnsureDict() {
                if (_dict == null) {
                    Interlocked.CompareExchange<PythonDictionary>(ref _dict, PythonDictionary.MakeSymbolDictionary(), null);
                }
            }

            #endregion

            #region ICodeFormattable Members

            /// <summary>
            /// Implements __repr__ which returns the type name + the args
            /// tuple code formatted.
            /// </summary>
            public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
                return _type.Name + ((ICodeFormattable)args).__repr__(context);
            }

            #endregion

            #region IPythonObject Members

            PythonDictionary IPythonObject.Dict {
                get { return _dict; }
            }

            PythonDictionary IPythonObject.SetDict(PythonDictionary dict) {
                Interlocked.CompareExchange<PythonDictionary>(ref _dict, dict, null);
                return _dict;
            }

            bool IPythonObject.ReplaceDict(PythonDictionary dict) {
                return Interlocked.CompareExchange<PythonDictionary>(ref _dict, dict, null) == null;
            }

            PythonType IPythonObject.PythonType {
                get { return _type; }
            }

            public BaseException __cause__ {
                get { return _cause; }
                set {
                    _cause = value;
                    __suppress_context__ = true;
                }
            }

            public BaseException __context__ {
                get {
                    if (_context == null) return __cause__;
                    return _context;
                }
                internal set {
                    _context = value;
                }
            }

            public bool __suppress_context__ { get; set; }

            public TraceBack __traceback__ {
                get {
                    if (!_tracebackSet && _traceback == null) {
                        var clrException = GetClrException();
                        var frames = clrException.GetFrameList();
                        if (frames != null) {
                            __traceback__ = PythonOps.CreateTraceBack(clrException, frames, null, frames.Count);
                        }
                    }
                    return _traceback;
                }
                set {
                    _traceback = value;
                    _tracebackSet = true;
                }
            }

            internal bool HasCause => __cause__ != null;

            internal bool IsImplicitException => __cause__ == null && __cause__ != __context__;

            void IPythonObject.SetPythonType(PythonType/*!*/ newType) {
                if (_type.IsSystemType || newType.IsSystemType) {
                    throw PythonOps.TypeError("__class__ assignment can only be performed on user defined types");
                }

                _type = newType;
            }

            object[] IPythonObject.GetSlots() { return _slots; }
            object[] IPythonObject.GetSlotsCreate() {
                if (_slots == null) {
                    Interlocked.CompareExchange(ref _slots, new object[1], null);
                }
                return _slots;
            }

            #endregion

            #region Internal .NET Exception production

            /// <summary>
            /// Initializes the Python exception from a .NET exception
            /// </summary>
            /// <param name="exception"></param>
            [PythonHidden]
            protected internal virtual void InitializeFromClr(System.Exception/*!*/ exception) {
                if (exception.Message != null) {
                    __init__(exception.Message);
                } else {
                    __init__();
                }
            }

            /// <summary>
            /// Helper to get the CLR exception associated w/ this Python exception
            /// creating it if one has not already been created.
            /// </summary>
            internal/*!*/ System.Exception GetClrException(Exception innerException = null) {
                if (_clrException != null) {
                    return _clrException;
                }

                string stringMessage = _message as string;
                if (String.IsNullOrEmpty(stringMessage)) {
                    stringMessage = _type.Name;
                }
                System.Exception newExcep = _type._makeException(stringMessage, innerException);
                newExcep.SetPythonException(this);

                Interlocked.CompareExchange<System.Exception>(ref _clrException, newExcep, null);

                return _clrException;
            }

            internal Exception CreateClrExceptionWithCause(BaseException cause, BaseException context) {
                _cause = cause;
                _context = context;
                _traceback = null;

                if (cause != null) {
                    return GetClrException(cause.GetClrException());
                }
                if (context != null) {
                    return GetClrException(context.GetClrException());
                }

                return GetClrException();
            }

            internal System.Exception/*!*/ InitAndGetClrException(params object[] args) {
                __init__(args);

                return GetClrException();
            }

            #endregion

            #region IDynamicMetaObjectProvider Members

            DynamicMetaObject/*!*/ IDynamicMetaObjectProvider.GetMetaObject(Expression/*!*/ parameter) {
                return new Binding.MetaUserObject(parameter, BindingRestrictions.Empty, null, this);
            }

            #endregion

            #region IWeakReferenceable Members

            WeakRefTracker IWeakReferenceable.GetWeakRef() {
                return UserTypeOps.GetWeakRefHelper(this);
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                return UserTypeOps.SetWeakRefHelper(this, value);
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
                UserTypeOps.SetFinalizerHelper(this, value);
            }

            #endregion
        }
    }
}
