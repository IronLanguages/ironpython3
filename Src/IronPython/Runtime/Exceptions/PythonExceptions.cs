﻿/* ****************************************************************************
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
#else
using Microsoft.Scripting.Ast;
#endif

#if !FEATURE_REMOTING
using MarshalByRefObject = System.Object;
#endif

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("exceptions", typeof(IronPython.Runtime.Exceptions.PythonExceptions))]
namespace IronPython.Runtime.Exceptions {
    /// <summary>
    /// Implementation of the Python exceptions module and the IronPython/CLR exception mapping 
    /// mechanism.  The exception module is the parent module for all Python exception classes
    /// and therefore is built-in to IronPython.dll instead of IronPython.Modules.dll.
    /// 
    /// The exception mapping mechanism is exposed as internal surface area available to only
    /// IronPython / IronPython.Modules.dll.  The actual exceptions themselves are all public.
    /// 
    /// Because the oddity of the built-in exception types all sharing the same physical layout
    /// (see also PythonExceptions.BaseException) some classes are defined as classes w/ their
    /// proper name and some classes are defined as PythonType fields.  When a class is defined
    /// for convenience their's also an _TypeName version which is the PythonType.
    /// </summary>
    public static partial class PythonExceptions {
        private static object _pythonExceptionKey = typeof(BaseException);
        internal const string DefaultExceptionModule = "exceptions";
        public const string __doc__ = "Provides the most commonly used exceptions for Python programs";
        
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
        [PythonType("BaseException"), DynamicBaseTypeAttribute, Serializable]
        public class BaseException : ICodeFormattable, IPythonObject, IDynamicMetaObjectProvider, IWeakReferenceable {
            private PythonType/*!*/ _type;          // the actual Python type of the Exception object
            private object _message = String.Empty; // the message object, cached at __init__ time, not updated on args assignment
            private PythonTuple _args;              // the tuple of args provided at creation time
            private PythonDictionary _dict;    // the dictionary for extra values, created on demand
            private System.Exception _clrException; // the cached CLR exception that is thrown
            private object[] _slots;                // slots, only used for storage of our weak reference.

            // PEP 3134 changes for exception chaining
            private BaseException _cause;
            private BaseException _context;
            private TraceBack _traceback;

            public static string __doc__ = "Common base class for all non-exit exceptions.";

            #region Public API Surface

            public BaseException(PythonType/*!*/ type) : this(type, null) {

            }

            public BaseException(PythonType/*!*/ type, BaseException cause)
            {
                ContractUtils.RequiresNotNull(type, "type");

                _type = type;

                // Set nested python exception
                CreateClrExceptionWithCause(cause, null);
            }

            public static object __new__(PythonType/*!*/ cls, params object[] _args) {
                if (cls.UnderlyingSystemType == typeof(BaseException)) {
                    if (_args == null || _args.Length == 0)
                    {
                        return new BaseException(cls);
                    }
                    else if (_args.Length > 0) {
                        if (_args[0] is BaseException) {
                            return new BaseException(cls, (BaseException)(_args[0]));
                        }
                    }
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
            /// Returns the exception 'message' if only a single argument was provided 
            /// during creation or an empty string.
            /// </summary>
            public object message {
                [Python3Warning("BaseException.message has been deprecated as of Python 2.6")]
                get { return _message; }
                [Python3Warning("BaseException.message has been deprecated as of Python 2.6")]
                set { _message = value; }
            }

            /// <summary>
            /// Gets or sets the arguments used for creating the exception
            /// </summary>
            public object/*!*/ args {
                get {
                    return _args ?? PythonTuple.EMPTY;
                }
                set { _args = PythonTuple.Make(value); }
            }
            
            /// <summary>
            /// Returns a tuple of (type, (arg0, ..., argN)) for implementing pickling/copying
            /// </summary>
            public virtual object/*!*/ __reduce__() {
                return PythonTuple.MakeTuple(DynamicHelpers.GetPythonType(this), args);
            }

            /// <summary>
            /// Returns a tuple of (type, (arg0, ..., argN)) for implementing pickling/copying
            /// </summary>
            public virtual object/*!*/ __reduce_ex__(int protocol) {
                return __reduce__();
            }

            /// <summary>
            /// Gets the nth member of the args property
            /// </summary>            
            public object this[int index] {
                [Python3Warning("__getitem__ not supported for exception classes in 3.x; use args attribute")]
                get {
                    return ((PythonTuple)args)[index];
                }
            }

            [Python3Warning("__getslice__ not supported for exception classes in 3.x; use args attribute")]
            public PythonTuple/*!*/ __getslice__(int start, int stop) {
                PythonTuple argTuple = (PythonTuple)args;
                Slice.FixSliceArguments(argTuple._data.Length, ref start, ref stop);
                return PythonTuple.MakeTuple(ArrayOps.GetSlice(argTuple._data, start, stop));
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
                    __dict__[pair.Key] =  pair.Value;
                }
            }

            /// <summary>
            /// Gets the CLR exception associated w/ this Python exception.  Not visible
            /// until a .NET namespace is imported.
            /// </summary>
            public System.Exception/*!*/ clsException {
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
                    } else if ((extStr = _args[0] as Extensible<string>) != null) {
                        return extStr.Value;
                    }

                    return PythonOps.ToString(_args[0]);
                }

                return _args.ToString();
            }

            public string __unicode__() {
                return ToString();
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

            /// <summary>
            /// Gets or sets the attribute for explicitly chained exceptions
            /// </summary>
            public BaseException __cause__
            {
                get
                {
                    return _cause;
                }

                internal set
                {
                    _cause = value;
                }
            }

            /// <summary>
            /// Gets or sets the attribute for implicitly chained exceptions
            /// </summary>
            public BaseException __context__
            {
                get
                {
                    // If we have no context, be sure we return the context, this is maybe not null
                    if (_context == null)
                    {
                        return __cause__;
                    }

                    return _context;
                }

                internal set
                {
                    _context = value;
                }
            }

            /// <summary>
            /// Gets or sets attribute for the traceback.
            /// </summary>
            public TraceBack __traceback__
            {
                get
                {
                    if (_traceback == null)
                    {
                        var clrException = GetClrException();
                        List<DynamicStackFrame> frames = clrException.GetFrameList();
                        _traceback = PythonOps.CreateTraceBack(clrException, frames, frames.Count);
                    }

                    return _traceback;
                }
            }

            /// <summary>
            /// Gets whether a cause exists or not
            /// </summary>
            public bool HasCause
            {
                get
                {
                    return __cause__ != null;
                }
            }

            /// <summary>
            /// Gets whether the exception is an implcicit exception
            /// </summary>
            public bool IsImplicitException
            {
                get
                {
                    if (__cause__ != __context__ && __cause__ == null)
                    {
                        return true;
                    }

                    return false;
                }
            }

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
            /// <param name="innerException">Optional inner exception</param>
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

            /// <summary>
            /// Returns a create clr-exception with the specific inner-exception
            /// </summary>
            /// <param name="cause"></param>
            /// <param name="context"></param>
            /// <returns></returns>
            internal Exception CreateClrExceptionWithCause(BaseException cause, BaseException context)
            {
                _cause = cause;

                // Set default context. Not totally sure that this is the correct way
                // following PEP 3134.
                _context = context;
                _traceback = null;

                // Create CLR-Exception and set the cause as Inner-Exception
                if (cause != null)
                {
                    var causeClrException = cause.GetClrException();
                    _clrException = GetClrException(causeClrException);
                }
                // ... with inner implicit exception
                else if (context != null)
                {
                    var causeClrException = context.GetClrException();
                    _clrException = GetClrException(causeClrException);
                }

                return _clrException;
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

        #region Custom Exception Code

        public partial class _SyntaxError : BaseException {
            public override string ToString() {
                PythonTuple t = ((PythonTuple)args) as PythonTuple;
                if (t != null) {
                    switch (t.__len__()) {
                        case 0: return PythonOps.ToString(null);
                        case 1: return PythonOps.ToString(t[0]);
                        case 2:
                            string msg = t[0] as string;
                            if (msg != null) {
                                return msg;
                            }

                            goto default;
                        default: return PythonOps.ToString(t);
                    }
                }
                return String.Empty;
            }

            public override void __init__(params object[] args) {
                base.__init__(args);

                if (args != null && args.Length != 0) {
                    msg = args[0];
                    
                    if (args.Length >= 2) {
                        // args can be provided either as:
                        //  (msg, filename, lineno, offset, text, printFileandLineStr)
                        // or:
                        //  (msg, (filename, lineno, offset, text, printFileandLineStr))
                        PythonTuple locationInfo = args[1] as PythonTuple;
                        if(locationInfo != null) {
                            if (locationInfo.__len__() != 4) {
                                throw PythonOps.IndexError("SyntaxError expected tuple with 4 arguments, got {0}", locationInfo.__len__());
                            }

                            filename = locationInfo[0];
                            lineno = locationInfo[1];
                            offset = locationInfo[2];
                            text = locationInfo[3];
                        } 
                    }
                }
            }
        }


        public partial class _EnvironmentError : BaseException {
            public override object __reduce__() {
                if (_filename != null) {
                    return PythonTuple.MakeTuple(DynamicHelpers.GetPythonType(this), PythonTuple.MakeTuple(ArrayUtils.Append(((PythonTuple)args)._data, _filename)));
                }
                return base.__reduce__();
            }

            public override void __init__(params object[] args) {
                if (args != null) {
                    switch (args.Length) {
                        case 0:
                        case 1:                     // do nothing special
                            break;
                        case 2:                             // errno + strerror
                            _errno = args[0];
                            _strerror = args[1];
                            break;
                        case 3:                             // errno, str error, filename
                            _errno = args[0];
                            _strerror = args[1];
                            _filename = args[2];
                            // CPython doesn't include filename in args, remove
                            // it before calling base init.
                            args = ArrayUtils.RemoveLast(args);
                            break;
                        default:
                            // do nothing special for four or more args
                            break;
                    }
                }

                base.__init__(args);
            }

            public override string ToString() {
                if (_errno != null && _strerror != null) {
                    if (_filename != null) {
                        return String.Format("[Errno {0}] {1}: {2}", _errno, _strerror, _filename);
                    } else {
                        return String.Format("[Errno {0}] {1}", _errno, _strerror);
                    }
                } else if (args is PythonTuple && ((PythonTuple)args).Count > 0) {
                    return PythonOps.ToString(((PythonTuple)args)[0]);
                }

                return String.Empty;
            }

            private const int EACCES = 13;
            private const int ENOENT = 2;

            [PythonHidden]
            protected internal override void InitializeFromClr(System.Exception/*!*/ exception) {
                if (exception is FileNotFoundException ||
                    exception is DirectoryNotFoundException ||
                    exception is PathTooLongException 
#if FEATURE_DRIVENOTFOUNDEXCEPTION
                    || exception is DriveNotFoundException
#endif
                    ) {
                    __init__(ENOENT, exception.Message);
                    return;                    
                }

                UnauthorizedAccessException noAccess = exception as UnauthorizedAccessException;
                if (noAccess != null) {
                    __init__(EACCES, exception.Message);
                    return;
                }

#if !SILVERLIGHT5
                var ioExcep = exception as System.IO.IOException;
                if (ioExcep != null) {
                    try {
                        uint hr = (uint)GetHRForException(exception);
                        if ((hr & 0xffff0000U) == 0x80070000U) {
                            // win32 error code, get the real error code...
                            __init__(hr & 0xffff, exception.Message);
                            return;
                        }
                    } catch (MethodAccessException) {
                    } catch(SecurityException) {
                        // not enough permissions to do this...
                    }
                }
#endif

                base.InitAndGetClrException(exception);
            }

#if !SILVERLIGHT5
            [MethodImpl(MethodImplOptions.NoInlining)] // don't inline so the link demand is always evaluated here.
            private static int GetHRForException(System.Exception exception) {
                return System.Runtime.InteropServices.Marshal.GetHRForException(exception);
            }
#endif
        }

        public partial class _UnicodeTranslateError : BaseException {
            public override void __init__(params object[] args) {
                if (args.Length != 4) {
                    throw PythonOps.TypeError("function takes exactly 4 arguments ({0} given)", args.Length);
                }

                if (args[0] is string || args[0] is Extensible<string>) {
                    @object = args[0];
                } else {
                    throw PythonOps.TypeError("argument 4 must be unicode, not {0}", DynamicHelpers.GetPythonType(args[0]).Name);
                }

                start = args[1];
                end = args[2];

                if (args[3] is string || args[3] is Extensible<string>) {
                    reason = args[3];
                } else {
                    throw PythonOps.TypeError("argument 4 must be str, not {0}", DynamicHelpers.GetPythonType(args[3]).Name);
                }

                base.__init__(args);
            }
        }

        public partial class _WindowsError : _EnvironmentError {
            public override void __init__(params object[] args) {
                if (args.Length == 2 || args.Length == 3) {
                    if (!(args[0] is int)) {
                        throw PythonOps.TypeError("an integer is required for the 1st argument of WindowsError");
                    }
                }
                base.__init__(args);

                if (args != null && (args.Length == 2 || args.Length == 3)) {
                    winerror = args[0];
                }

                /*
                 * errors were generated using this script run against CPython: 
f = file(r'C:\Program Files\Microsoft SDKs\Windows\v6.0A\Include\WinError.h', 'r')
allErrors = []
toError = {}
for x in f:
    if x.startswith('#define ERROR_'):
        name = x[8:]
        endName = name.find(' ')
        justName = name[:endName]
        error = name[endName + 1:].strip()
        for i in xrange(len(error)):
            if error[i] < '0' or error[i] > '9':
                error = error[:i]
                break
            
        if not error:
            continue
            
        errNo = WindowsError(int(error), justName).errno
        if errNo == 22:
            continue
        allErrors.append( (justName, error) )
        
        l = toError.get(errNo) 
        if l is None:
            toError[errNo] = l = []
        l.append(justName)
        
        
for name, err in allErrors:
    print 'internal const int %s = %s;' % (name, err)


for k, v in toError.iteritems():
    for name in v:
        print 'case %s:' % (name, )
    print '    errno = %d;' % (k, )
    print '    break;'
                 */

                // map from win32 error code to errno
                object errCode = errno;
                if (errCode is int) {
                    switch ((int)errCode) {
                        case ERROR_BROKEN_PIPE:
                        case ERROR_NO_DATA:
                            errno = 32;
                            break;
                        case ERROR_FILE_NOT_FOUND:
                        case ERROR_PATH_NOT_FOUND:
                        case ERROR_INVALID_DRIVE:
                        case ERROR_NO_MORE_FILES:
                        case ERROR_BAD_NETPATH:
                        case ERROR_BAD_NET_NAME:
                        case ERROR_BAD_PATHNAME:
                        case ERROR_FILENAME_EXCED_RANGE:
                            errno = 2;
                            break;
                        case ERROR_BAD_ENVIRONMENT:
                            errno = 7;
                            break;
                        case ERROR_BAD_FORMAT:
                        case ERROR_INVALID_STARTING_CODESEG:
                        case ERROR_INVALID_STACKSEG:
                        case ERROR_INVALID_MODULETYPE:
                        case ERROR_INVALID_EXE_SIGNATURE:
                        case ERROR_EXE_MARKED_INVALID:
                        case ERROR_BAD_EXE_FORMAT:
                        case ERROR_ITERATED_DATA_EXCEEDS_64k:
                        case ERROR_INVALID_MINALLOCSIZE:
                        case ERROR_DYNLINK_FROM_INVALID_RING:
                        case ERROR_IOPL_NOT_ENABLED:
                        case ERROR_INVALID_SEGDPL:
                        case ERROR_AUTODATASEG_EXCEEDS_64k:
                        case ERROR_RING2SEG_MUST_BE_MOVABLE:
                        case ERROR_RELOC_CHAIN_XEEDS_SEGLIM:
                        case ERROR_INFLOOP_IN_RELOC_CHAIN:
                            errno = 8;
                            break;
                        case ERROR_INVALID_HANDLE:
                        case ERROR_INVALID_TARGET_HANDLE:
                        case ERROR_DIRECT_ACCESS_HANDLE:
                            errno = 9;
                            break;
                        case ERROR_WAIT_NO_CHILDREN:
                        case ERROR_CHILD_NOT_COMPLETE:
                            errno = 10;
                            break;
                        case ERROR_NO_PROC_SLOTS:
                        case ERROR_MAX_THRDS_REACHED:
                        case ERROR_NESTING_NOT_ALLOWED:
                            errno = 11;
                            break;
                        case ERROR_ARENA_TRASHED:
                        case ERROR_NOT_ENOUGH_MEMORY:
                        case ERROR_INVALID_BLOCK:
                        case ERROR_NOT_ENOUGH_QUOTA:
                            errno = 12;
                            break;
                        case ERROR_ACCESS_DENIED:
                        case ERROR_CURRENT_DIRECTORY:
                        case ERROR_WRITE_PROTECT:
                        case ERROR_BAD_UNIT:
                        case ERROR_NOT_READY:
                        case ERROR_BAD_COMMAND:
                        case ERROR_CRC:
                        case ERROR_BAD_LENGTH:
                        case ERROR_SEEK:
                        case ERROR_NOT_DOS_DISK:
                        case ERROR_SECTOR_NOT_FOUND:
                        case ERROR_OUT_OF_PAPER:
                        case ERROR_WRITE_FAULT:
                        case ERROR_READ_FAULT:
                        case ERROR_GEN_FAILURE:
                        case ERROR_SHARING_VIOLATION:
                        case ERROR_LOCK_VIOLATION:
                        case ERROR_WRONG_DISK:
                        case ERROR_SHARING_BUFFER_EXCEEDED:
                        case ERROR_NETWORK_ACCESS_DENIED:
                        case ERROR_CANNOT_MAKE:
                        case ERROR_FAIL_I24:
                        case ERROR_DRIVE_LOCKED:
                        case ERROR_SEEK_ON_DEVICE:
                        case ERROR_NOT_LOCKED:
                        case ERROR_LOCK_FAILED:
                            errno = 13;
                            break;
                        case ERROR_FILE_EXISTS:
                        case ERROR_ALREADY_EXISTS:
                            errno = 17;
                            break;
                        case ERROR_NOT_SAME_DEVICE:
                            errno = 18;
                            break;
                        case ERROR_DIR_NOT_EMPTY:
                            errno = 41;
                            break;
                        case ERROR_TOO_MANY_OPEN_FILES:
                            errno = 24;
                            break;
                        case ERROR_DISK_FULL:
                            errno = 28;
                            break;
                        default:
                            errno = 22;
                            break;
                    }
                }                
            }

            internal const int ERROR_FILE_NOT_FOUND = 2;
            internal const int ERROR_PATH_NOT_FOUND = 3;
            internal const int ERROR_TOO_MANY_OPEN_FILES = 4;
            internal const int ERROR_ACCESS_DENIED = 5;
            internal const int ERROR_INVALID_HANDLE = 6;
            internal const int ERROR_ARENA_TRASHED = 7;
            internal const int ERROR_NOT_ENOUGH_MEMORY = 8;
            internal const int ERROR_INVALID_BLOCK = 9;
            internal const int ERROR_BAD_ENVIRONMENT = 10;
            internal const int ERROR_BAD_FORMAT = 11;
            internal const int ERROR_INVALID_DRIVE = 15;
            internal const int ERROR_CURRENT_DIRECTORY = 16;
            internal const int ERROR_NOT_SAME_DEVICE = 17;
            internal const int ERROR_NO_MORE_FILES = 18;
            internal const int ERROR_WRITE_PROTECT = 19;
            internal const int ERROR_BAD_UNIT = 20;
            internal const int ERROR_NOT_READY = 21;
            internal const int ERROR_BAD_COMMAND = 22;
            internal const int ERROR_CRC = 23;
            internal const int ERROR_BAD_LENGTH = 24;
            internal const int ERROR_SEEK = 25;
            internal const int ERROR_NOT_DOS_DISK = 26;
            internal const int ERROR_SECTOR_NOT_FOUND = 27;
            internal const int ERROR_OUT_OF_PAPER = 28;
            internal const int ERROR_WRITE_FAULT = 29;
            internal const int ERROR_READ_FAULT = 30;
            internal const int ERROR_GEN_FAILURE = 31;
            internal const int ERROR_SHARING_VIOLATION = 32;
            internal const int ERROR_LOCK_VIOLATION = 33;
            internal const int ERROR_WRONG_DISK = 34;
            internal const int ERROR_SHARING_BUFFER_EXCEEDED = 36;
            internal const int ERROR_BAD_NETPATH = 53;
            internal const int ERROR_NETWORK_ACCESS_DENIED = 65;
            internal const int ERROR_BAD_NET_NAME = 67;
            internal const int ERROR_FILE_EXISTS = 80;
            internal const int ERROR_CANNOT_MAKE = 82;
            internal const int ERROR_FAIL_I24 = 83;
            internal const int ERROR_NO_PROC_SLOTS = 89;
            internal const int ERROR_DRIVE_LOCKED = 108;
            internal const int ERROR_BROKEN_PIPE = 109;
            internal const int ERROR_DISK_FULL = 112;
            internal const int ERROR_INVALID_TARGET_HANDLE = 114;
            internal const int ERROR_WAIT_NO_CHILDREN = 128;
            internal const int ERROR_CHILD_NOT_COMPLETE = 129;
            internal const int ERROR_DIRECT_ACCESS_HANDLE = 130;
            internal const int ERROR_SEEK_ON_DEVICE = 132;
            internal const int ERROR_DIR_NOT_EMPTY = 145;
            internal const int ERROR_NOT_LOCKED = 158;
            internal const int ERROR_BAD_PATHNAME = 161;
            internal const int ERROR_MAX_THRDS_REACHED = 164;
            internal const int ERROR_LOCK_FAILED = 167;
            internal const int ERROR_ALREADY_EXISTS = 183;
            internal const int ERROR_INVALID_STARTING_CODESEG = 188;
            internal const int ERROR_INVALID_STACKSEG = 189;
            internal const int ERROR_INVALID_MODULETYPE = 190;
            internal const int ERROR_INVALID_EXE_SIGNATURE = 191;
            internal const int ERROR_EXE_MARKED_INVALID = 192;
            internal const int ERROR_BAD_EXE_FORMAT = 193;
            internal const int ERROR_ITERATED_DATA_EXCEEDS_64k = 194;
            internal const int ERROR_INVALID_MINALLOCSIZE = 195;
            internal const int ERROR_DYNLINK_FROM_INVALID_RING = 196;
            internal const int ERROR_IOPL_NOT_ENABLED = 197;
            internal const int ERROR_INVALID_SEGDPL = 198;
            internal const int ERROR_AUTODATASEG_EXCEEDS_64k = 199;
            internal const int ERROR_RING2SEG_MUST_BE_MOVABLE = 200;
            internal const int ERROR_RELOC_CHAIN_XEEDS_SEGLIM = 201;
            internal const int ERROR_INFLOOP_IN_RELOC_CHAIN = 202;
            internal const int ERROR_FILENAME_EXCED_RANGE = 206;
            internal const int ERROR_NESTING_NOT_ALLOWED = 215;
            internal const int ERROR_NO_DATA = 232; // The pipe is being closed.
            internal const int ERROR_NOT_ENOUGH_QUOTA = 1816;

            // These map to POSIX errno 22 and are added by hand as needed.
            internal const int ERROR_INVALID_PARAMETER = 87;
            internal const int ERROR_INVALID_NAME = 123;
            internal const int ERROR_FILE_INVALID = 1006;
            internal const int ERROR_MAPPED_ALIGNMENT = 1132;
        }

        public partial class _UnicodeDecodeError : BaseException {
            [PythonHidden]
            protected internal override void InitializeFromClr(System.Exception/*!*/ exception) {
                DecoderFallbackException ex = exception as DecoderFallbackException;
                if (ex != null) {
                    StringBuilder sb = new StringBuilder();
                    if (ex.BytesUnknown != null) {
                        for (int i = 0; i < ex.BytesUnknown.Length; i++) {
                            sb.Append((char)ex.BytesUnknown[i]);
                        }
                    }
                    __init__("unknown", sb.ToString(), ex.Index, ex.Index + 1, "");
                } else {
                    base.InitializeFromClr(exception);
                }
            }
        }

        public partial class _UnicodeEncodeError : BaseException {
            [PythonHidden]
            protected internal override void InitializeFromClr(System.Exception/*!*/ exception) {
                EncoderFallbackException ex = exception as EncoderFallbackException;
                if (ex != null) {
                    __init__((exception.Data.Contains("encoding")) ? exception.Data["encoding"] : "unknown",
                        new string(ex.CharUnknown, 1), ex.Index, ex.Index + 1, exception.Message);
                } else {
                    base.InitializeFromClr(exception);
                }
            }
        }

        public partial class _SystemExit : BaseException {
            public override void __init__(params object[] args) {
                base.__init__(args);

                if (args != null && args.Length != 0) {
                    code = message;
                }
            }
        }

        #endregion

        #region Exception translation

        internal static System.Exception CreateThrowable(PythonType type, params object[] args) {
            BaseException be = CreatePythonThrowable(type, args);

            return be.GetClrException();
        }

        internal static BaseException CreatePythonThrowable(PythonType type, params object[] args) {
            BaseException be;
            if (type.UnderlyingSystemType == typeof(BaseException)) {
                be = new BaseException(type);
            } else {
                be = (BaseException)Activator.CreateInstance(type.UnderlyingSystemType, type);
            }
            be.__init__(args);
            return be;
        }
        
        /// <summary>
        /// Creates a new throwable exception of type type where the type is an new-style exception.
        /// 
        /// Used at runtime when creating the exception from a user provided type via the raise statement.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Throwable")]
        internal static System.Exception CreateThrowableForRaise(CodeContext/*!*/ context, PythonType/*!*/ type, object value, object cause) {
            object pyEx;

            if (PythonOps.IsInstance(value, type)) {
                pyEx = value;
            } else if (value is PythonTuple) {
                pyEx = PythonOps.CallWithArgsTuple(type, ArrayUtils.EmptyObjects, value);
            } else if (value != null) {
                pyEx = PythonCalls.Call(context, type, value, cause);
            } else {
                pyEx = PythonCalls.Call(context, type, cause);
            }

            if (PythonOps.IsInstance(pyEx, type)) {
                // Get context exception
                var contextException = PythonOps.GetRawContextException();

                // If we have a context-exception or no context/cause lets return the existing
                // or a new exception
                if (cause != null || cause == null && contextException == null)
                {
                    // overloaded __new__ can return anything, if 
                    // it's the right exception type use the normal conversion...
                    // If it's wrong return an ObjectException which remembers the type.
                    return ((BaseException)pyEx).GetClrException();
                }
                else if (context != null)
                {
                    // Generate new CLR-Exception and return it, with the implicit context exception
                    return ((BaseException)pyEx).CreateClrExceptionWithCause(null, contextException);
                }
                
            }

            // user returned arbitrary object from overridden __new__, let it throw...
            return new ObjectException(type, pyEx);
        }
        
        /// <summary>
        /// Creates a throwable exception of type type where the type is an OldClass.
        /// 
        /// Used at runtime when creating the exception form a user provided type that's an old class (via the raise statement).
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Throwable")]
        internal static System.Exception CreateThrowableForRaise(CodeContext/*!*/ context, OldClass type, object value, object cause) {
            object pyEx;

            if (PythonOps.IsInstance(context, value, type)) {
                pyEx = value;
            } else if (value is PythonTuple) {
                pyEx = PythonOps.CallWithArgsTuple(type, ArrayUtils.EmptyObjects, value);
            } else {
                pyEx = PythonCalls.Call(context, type, value);
            }

            return new OldInstanceException((OldInstance)pyEx);
        }

        /// <summary>
        /// Returns the CLR exception associated with a Python exception
        /// creating a new exception if necessary
        /// </summary>
        internal static System.Exception ToClr(object pythonException) {
            PythonExceptions.BaseException pyExcep = pythonException as PythonExceptions.BaseException;
            if (pyExcep != null) {
                return pyExcep.GetClrException();
            }

            System.Exception res;
            OldInstance oi = pythonException as OldInstance;
            if(oi != null) {
                res = new OldInstanceException(oi);
            } else {
                // default exception message is the exception type (from Python)
                res = new System.Exception(PythonOps.ToString(pythonException));
            }

            res.SetPythonException(pythonException);

            return res;
        }

        /// <summary>
        /// Given a CLR exception returns the Python exception which most closely maps to the CLR exception.
        /// </summary>
        internal static object ToPython(System.Exception/*!*/ clrException) {
            Debug.Assert(clrException != null);

            // certain Python exceptions (StringException, OldInstanceException, ObjectException)
            // expose the underlying object they're wrapping directly.
            IPythonException ipe = clrException as IPythonException;
            if (ipe != null) {
                return ipe.ToPythonException();
            }

            object res = clrException.GetPythonException();
            if (res == null) {
                SyntaxErrorException syntax;

                // explicit extra conversions that need a special transformation
                if ((syntax = clrException as SyntaxErrorException) != null) {
                    return SyntaxErrorToPython(syntax);
                }

#if FEATURE_EXCEPTION_STATE
                ThreadAbortException ta;
                if ((ta = clrException as ThreadAbortException) != null) {
                    // transform TA w/ our reason into a KeyboardInterrupt exception.
                    KeyboardInterruptException reason = ta.ExceptionState as KeyboardInterruptException;
                    if (reason != null) {
                        ta.Data[typeof(KeyboardInterruptException)] = reason;
                        return ToPython(reason);
                    }

                    // check for cleared but saved reason...
                    reason = ta.Data[typeof(KeyboardInterruptException)] as KeyboardInterruptException;
                    if (reason != null) {
                        return ToPython(reason);
                    }
                }
#endif
                if (res == null) {
                    res = ToPythonNewStyle(clrException);
                }

                clrException.SetPythonException(res);
            }

            return res;
        }

        /// <summary>
        /// Creates a new style Python exception from the .NET exception
        /// </summary>
        private static BaseException/*!*/ ToPythonNewStyle(System.Exception/*!*/ clrException) {
            BaseException pyExcep;
            if (clrException is InvalidCastException || clrException is ArgumentNullException) {
                // explicit extra conversions outside the generated hierarchy
                pyExcep = new BaseException(TypeError);
            } else if (clrException is Win32Exception) {
                Win32Exception win32 = (Win32Exception)clrException;
                pyExcep = new _WindowsError();
                if ((win32.ErrorCode & 0x80070000) == 0x80070000) {
                    pyExcep.__init__(win32.ErrorCode & 0xffff, win32.Message);
                } else {
                    pyExcep.__init__(win32.ErrorCode, win32.Message);
                }
                return pyExcep;
            } else {
                // conversions from generated code (in the generated hierarchy)...
                pyExcep = ToPythonHelper(clrException);
            }

            pyExcep.InitializeFromClr(clrException);

            return pyExcep;
        }

        [Serializable]
        private class ExceptionDataWrapper : MarshalByRefObject {
            private readonly object _value;

            public ExceptionDataWrapper(object value) {
                _value = value;
            }

            public object Value {
                get {
                    return _value;
                }
            }
        }

        /// <summary>
        /// Internal helper to associate a .NET exception and a Python exception.
        /// </summary>
        private static void SetPythonException(this Exception e, object exception) {
            IPythonAwareException pyAware = e as IPythonAwareException;
            if (pyAware != null) {
                pyAware.PythonException = exception;
            } else {
                e.SetData(_pythonExceptionKey, new ExceptionDataWrapper(exception));
            }

            BaseException be = exception as BaseException;
            if (be != null) {
                be.clsException = e;
            }            
        }

        /// <summary>
        /// Internal helper to get the associated Python exception from a .NET exception.
        /// </summary>
        internal static object GetPythonException(this Exception e) {
            IPythonAwareException pyAware = e as IPythonAwareException;
            if (pyAware != null) {
                return pyAware.PythonException;
            }

            var wrapper = e.GetData(_pythonExceptionKey) as ExceptionDataWrapper;
            if (wrapper != null) {
                return wrapper.Value;
            }

            return null;
        }

        internal static List<DynamicStackFrame> GetFrameList(this Exception e) {
            IPythonAwareException pyAware = e as IPythonAwareException;
            if (pyAware != null) {
                return pyAware.Frames;
            } else {
                return e.GetData(typeof(DynamicStackFrame)) as List<DynamicStackFrame>;
            }
        }

        internal static void SetFrameList(this Exception e, List<DynamicStackFrame> frames) {
            IPythonAwareException pyAware = e as IPythonAwareException;
            if (pyAware != null) {
                pyAware.Frames = frames;
            } else {
                e.SetData(typeof(DynamicStackFrame), frames);
            }
        }

        internal static void RemoveFrameList(this Exception e) {
            IPythonAwareException pyAware = e as IPythonAwareException;
            if (pyAware != null) {
                pyAware.Frames = null;
            } else {
                e.RemoveData(typeof(DynamicStackFrame));
            }
        }

        internal static TraceBack GetTraceBack(this Exception e) {
            IPythonAwareException pyAware = e as IPythonAwareException;
            if (pyAware != null) {
                return pyAware.TraceBack;
            } else {
                return e.GetData(typeof(TraceBack)) as TraceBack;
            }
        }

        internal static void SetTraceBack(this Exception e, TraceBack traceback) {
            IPythonAwareException pyAware = e as IPythonAwareException;
            if (pyAware != null) {
                pyAware.TraceBack = traceback;
            } else {
                e.SetData(typeof(TraceBack), traceback);
            }
        }

        internal static void RemoveTraceBack(this Exception e) {
            IPythonAwareException pyAware = e as IPythonAwareException;
            if (pyAware != null) {
                pyAware.TraceBack = null;
            } else {
                e.RemoveData(typeof(TraceBack));
            }
        }

        /// <summary>
        /// Converts the DLR SyntaxErrorException into a Python new-style SyntaxError instance.
        /// </summary>
        private static BaseException/*!*/ SyntaxErrorToPython(SyntaxErrorException/*!*/ e) {
            PythonExceptions._SyntaxError se;
            if (e.GetType() == typeof(IndentationException)) {
                se = new _SyntaxError(IndentationError);
            } else if (e.GetType() == typeof(TabException)) {
                se = new _SyntaxError(TabError);
            } else {
                se = new _SyntaxError();
            }

            string sourceLine = PythonContext.GetSourceLine(e);
            string fileName = e.GetSymbolDocumentName();
            object column = (e.Column == 0 || e.GetData(PythonContext._syntaxErrorNoCaret) != null) ? null : (object)e.Column;
            
            se.args = PythonTuple.MakeTuple(e.Message, PythonTuple.MakeTuple(fileName, e.Line, column, sourceLine));

            se.filename = fileName;
            se.lineno = e.Line;
            se.offset = column;
            se.text = sourceLine;
            se.msg = e.Message;

            e.SetPythonException(se);

            return se;
        }

        /// <summary>
        /// Creates a PythonType for a built-in module.  These types are mutable like
        /// normal user types.
        /// </summary>
        [PythonHidden]
        public static PythonType CreateSubType(PythonContext/*!*/ context, PythonType baseType, string name, string module, string documentation, Func<string, Exception, Exception> exceptionMaker) {
            PythonType res = new PythonType(context, baseType, name, module, documentation, exceptionMaker);
            res.SetCustomMember(context.SharedContext, "__weakref__", new PythonTypeWeakRefSlot(res));
            res.IsWeakReferencable = true;
            return res;
        }

        /// <summary>
        /// Creates a PythonType for a built-in module.  These types are mutable like
        /// normal user types.
        /// </summary>
        [PythonHidden]
        public static PythonType CreateSubType(PythonContext/*!*/ context, PythonType baseType, Type underlyingType, string name, string module, string documentation, Func<string, Exception, Exception> exceptionMaker) {
            PythonType res = new PythonType(context, new PythonType[] { baseType }, underlyingType, name, module, documentation, exceptionMaker);
            res.SetCustomMember(context.SharedContext, "__weakref__", new PythonTypeWeakRefSlot(res));
            res.IsWeakReferencable = true;
            return res;
        }

        /// <summary>
        /// Creates a PythonType for a built-in module, where the type may inherit
        /// from multiple bases.  These types are mutable like normal user types. 
        /// </summary>
        [PythonHidden]
        public static PythonType CreateSubType(PythonContext/*!*/ context, PythonType[] baseTypes, Type underlyingType, string name, string module, string documentation, Func<string, Exception, Exception> exceptionMaker) {
            PythonType res = new PythonType(context, baseTypes, underlyingType, name, module, documentation, exceptionMaker);
            res.SetCustomMember(context.SharedContext, "__weakref__", new PythonTypeWeakRefSlot(res));
            res.IsWeakReferencable = true;
            return res;
        }


        /// <summary>
        /// Creates a new type for a built-in exception which derives from another Python
        /// type.  .  These types are built-in and immutable like any other normal type.  For 
        /// example StandardError.x = 3 is illegal.  This isn't for module exceptions which 
        /// are like user defined types.  thread.error.x = 3 is legal.
        /// </summary>
        private static PythonType CreateSubType(PythonType baseType, string name, Func<string, Exception, Exception> exceptionMaker) {
            return new PythonType(baseType, name, exceptionMaker);
        }

        /// <summary>
        /// Creates a new type for a built-in exception which is the root concrete type.  
        /// </summary>
        private static PythonType/*!*/ CreateSubType(PythonType/*!*/ baseType, Type/*!*/ concreteType, Func<string, Exception, Exception> exceptionMaker) {
            Assert.NotNull(baseType, concreteType);

            PythonType myType = DynamicHelpers.GetPythonTypeFromType(concreteType);

            myType.ResolutionOrder = Mro.Calculate(myType, new PythonType[] { baseType });
            myType.BaseTypes = new PythonType[] { baseType };
            myType.HasDictionary = true;
            myType._makeException = exceptionMaker;

            return myType;
        }

        #endregion

        #region .NET/Python Exception Merging/Tracking

        /// <summary>
        /// Gets the list of DynamicStackFrames for the current exception.
        /// </summary>
        internal static DynamicStackFrame[] GetDynamicStackFrames(Exception e) {
            List<DynamicStackFrame> frames = e.GetFrameList();

            if (frames == null) {
                return new DynamicStackFrame[0];
            }

#if !SILVERLIGHT
            frames = new List<DynamicStackFrame>(frames);
            List<DynamicStackFrame> identified = new List<DynamicStackFrame>();

            // merge .NET frames w/ any dynamic frames that we have
            try {
                StackTrace outermostTrace = new StackTrace(e);
                IList<StackTrace> otherTraces = ExceptionHelpers.GetExceptionStackTraces(e) ?? new List<StackTrace>();
                List<StackFrame> clrFrames = new List<StackFrame>();
                
                foreach (StackTrace trace in otherTraces) {
                    clrFrames.AddRange(trace.GetFrames() ?? new StackFrame[0]); // rare, sometimes GetFrames returns null
                }
                clrFrames.AddRange(outermostTrace.GetFrames() ?? new StackFrame[0]);    // rare, sometimes GetFrames returns null

                int lastFound = 0;
                foreach (StackFrame clrFrame in InterpretedFrame.GroupStackFrames(clrFrames)) {
                    MethodBase method = clrFrame.GetMethod();

                    for (int j = lastFound; j < frames.Count; j++) {
                        MethodBase other = frames[j].GetMethod();
                        // method info's don't always compare equal, check based
                        // upon name/module/declaring type which will always be a correct
                        // check for dynamic methods.
                        if (MethodsMatch(method, other)) {
                            identified.Add(frames[j]);
                            frames.RemoveAt(j);
                            lastFound = j;
                            break;
                        }
                    }
                }
            } catch (MemberAccessException) {
                // can't access new StackTrace(e) due to security
            }

            // combine identified and any remaining frames we couldn't find
            // this is equivalent of adding remaining frames in front of identified
            frames.AddRange(identified);
            return frames.ToArray();
#else 
            return frames.ToArray();
#endif
        }

        private static bool MethodsMatch(MethodBase method, MethodBase other) {
            return (method.Module == other.Module &&
                    method.DeclaringType == other.DeclaringType &&
                    method.Name == other.Name);
        }

        #endregion
    }
}
