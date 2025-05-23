﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

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
            private PythonTuple _args;              // the tuple of args provided at creation time
            private PythonDictionary? _dict;    // the dictionary for extra values, created on demand
            private System.Exception? _clrException; // the cached CLR exception that is thrown
            private object[]? _slots;                // slots, only used for storage of our weak reference.

            private BaseException? _cause;
            private BaseException? _context;
            private TraceBack? _traceback;
            private bool _tracebackSet;

            public static string __doc__ = "Common base class for all non-exit exceptions.";

            #region Public API Surface

            public BaseException([NotNone] PythonType/*!*/ type) {
                ContractUtils.RequiresNotNull(type, nameof(type));
                _type = type;
                _args = PythonTuple.EMPTY;
            }

            public static object __new__([NotNone] PythonType/*!*/ cls, [NotNone] params object?[] args) {
                BaseException res;
                if (cls.UnderlyingSystemType == typeof(BaseException)) {
                    res = new BaseException(cls);
                } else {
                    res = (BaseException)Activator.CreateInstance(cls.UnderlyingSystemType, cls)!;
                }
                res._args = new PythonTuple(DefaultContext.Default, args);
                return res;
            }

            public static object __new__([NotNone] PythonType/*!*/ cls, [ParamDictionary] IDictionary<string, object?> kwArgs, [NotNone] params object?[] args)
                => __new__(cls, args);

            /// <summary>
            /// Initializes the Exception object with an unlimited number of arguments
            /// </summary>
            public virtual void __init__([NotNone] params object?[] args) {
                _args = PythonTuple.MakeTuple(args ?? new object?[] { null });
            }

            /// <summary>
            /// Gets or sets the arguments used for creating the exception
            /// </summary>
            public object/*!*/ args {
                get { return _args; }
                [param: AllowNull]
                set { _args = PythonTuple.Make(value); }
            }

            public object with_traceback(TraceBack? tb) {
                __traceback__ = tb;
                return this;
            }

            /// <summary>
            /// Returns a tuple of (type, (arg0, ..., argN)) for implementing pickling/copying
            /// </summary>
            public virtual object/*!*/ __reduce__() {
                if (_dict != null && _dict.Count > 0)
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
            public PythonDictionary/*!*/ __dict__ {
                get {
                    EnsureDict();

                    return _dict!;
                }
                [param: AllowNull]
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
            public void __setstate__(PythonDictionary? state) {
                if (state == null) return;
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
                return (_args.__len__()) switch {
                    0 => string.Empty,
                    1 => PythonOps.ToString(_args[0]),
                    _ => _args.ToString(),
                };
            }

            #endregion

            #region Member access operators

            /// <summary>
            /// Provides custom member lookup access that fallbacks to the dictionary
            /// </summary>
            [SpecialName]
            public object GetBoundMember([NotNone] string name) {
                if (_dict != null) {
                    if (_dict.TryGetValue(name, out object res)) {
                        return res;
                    }
                }

                return OperationFailed.Value;
            }

            /// <summary>
            /// Provides custom member assignment which stores values in the dictionary
            /// </summary>
            [SpecialName]
            public void SetMemberAfter([NotNone] string name, object? value) {
                EnsureDict();

                _dict![name] = value;
            }

            /// <summary>
            /// Provides custom member deletion which deletes values from the dictionary
            /// or allows clearing 'message'.
            /// </summary>
            [SpecialName]
            public bool DeleteCustomMember([NotNone] string name) {
                if (_dict == null) return false;

                return _dict.Remove(name);
            }

            private void EnsureDict() {
                if (_dict == null) {
                    Interlocked.CompareExchange(ref _dict, PythonDictionary.MakeSymbolDictionary(), null);
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

            PythonDictionary? IPythonObject.Dict {
                get { return _dict; }
            }

            PythonDictionary IPythonObject.SetDict(PythonDictionary dict) {
                Interlocked.CompareExchange(ref _dict, dict, null);
                return _dict;
            }

            bool IPythonObject.ReplaceDict(PythonDictionary dict) {
                return Interlocked.CompareExchange(ref _dict, dict, null) == null;
            }

            PythonType IPythonObject.PythonType {
                get { return _type; }
            }

            public BaseException? __cause__ {
                get { return _cause; }
                set {
                    _cause = value;
                    __suppress_context__ = true;
                }
            }

            public BaseException? __context__ {
                get {
                    return _context ?? __cause__;
                }
                internal set {
                    _context = value;
                }
            }

            public bool __suppress_context__ { get; set; }

            public TraceBack? __traceback__ {
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

            object[]? IPythonObject.GetSlots() { return _slots; }

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
            internal/*!*/ System.Exception GetClrException(Exception? innerException = null) {
                if (_clrException != null) {
                    return _clrException;
                }

                string? stringMessage = _args.FirstOrDefault() as string;
                if (string.IsNullOrEmpty(stringMessage)) {
                    stringMessage = _type.Name;
                }
                System.Exception newExcep = _type._makeException(stringMessage, innerException);
                newExcep.SetPythonException(this);

                Interlocked.CompareExchange(ref _clrException, newExcep, null);

                return _clrException;
            }

            internal Exception CreateClrExceptionWithCause(BaseException? cause, BaseException? context, bool suppressContext) {
                _cause = cause;
                _context = context;
                __suppress_context__ = suppressContext;
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
