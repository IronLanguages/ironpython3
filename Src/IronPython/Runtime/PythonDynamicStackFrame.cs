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

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// A DynamicStackFrame which has Python specific data.  Currently this
    /// includes the code context which may provide access to locals and the
    /// function code object which is needed to build frame objects from.
    /// </summary>
    [Serializable]
    sealed class PythonDynamicStackFrame : DynamicStackFrame
#if !SILVERLIGHT
        , ISerializable 
#endif
    {
        private readonly CodeContext _context;
        private readonly FunctionCode _code;

        public PythonDynamicStackFrame(CodeContext/*!*/ context, FunctionCode/*!*/ funcCode, int line)
            : base(GetMethod(context, funcCode), funcCode.co_name, funcCode.co_filename, line) {
            Assert.NotNull(context, funcCode);

            _context = context;
            _code = funcCode;
        }

        private static MethodBase GetMethod(CodeContext context, FunctionCode funcCode) {
            MethodBase method;
            Debug.Assert(funcCode._normalDelegate != null || funcCode._tracingDelegate != null);
            if (!context.LanguageContext.EnableTracing || funcCode._tracingDelegate == null) {
                method = funcCode._normalDelegate.GetMethod();
            } else {
                method = funcCode._tracingDelegate.GetMethod();
            }
            return method;
        }


#if FEATURE_SERIALIZATION
        private PythonDynamicStackFrame(SerializationInfo info, StreamingContext context)
            : base((MethodBase)info.GetValue("method", typeof(MethodBase)), (string)info.GetValue("funcName", typeof(string)), (string)info.GetValue("filename", typeof(string)), (int)info.GetValue("line", typeof(int))) {
        }
#endif


        /// <summary>
        /// Gets the code context of the function.
        /// 
        /// If the function included a call to locals() or the FullFrames
        /// option is enabled then the code context includes all local variables.
        /// 
        /// Null if deserialized.
        /// </summary>
        public CodeContext CodeContext {
            get {
                return _context;
            }
        }

        /// <summary>
        /// Gets the code object for this frame.  This is used in creating
        /// the trace back. Null if deserialized.
        /// </summary>
        public FunctionCode Code {
            get {
                return _code;
            }
        }
#if FEATURE_SERIALIZATION
        #region ISerializable Members

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("method", GetMethod());
            info.AddValue("funcName", GetMethodName());
            info.AddValue("filename", GetFileName());
            info.AddValue("line", GetFileLineNumber());
        }

        #endregion
#endif
    }
}
