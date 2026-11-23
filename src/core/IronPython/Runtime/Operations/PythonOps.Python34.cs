// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if PYTHON_34

using System;
using System.ComponentModel;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Exceptions;

namespace IronPython.Runtime.Operations {
    // Methods that are no longer emitted, but that modules compiled by 3.4.2 and earlier still call.
    public static partial class PythonOps {
        [Obsolete("Use SaveCurrentExceptionState")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Exception? SaveCurrentException() {
            return CurrentExceptionState?.Exception?.Exception;
        }

        [Obsolete("Use RestoreCurrentExceptionState")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RestoreCurrentException(Exception? clrException) {
            var current = CurrentExceptionState?.Exception;
            if (current?.Exception == clrException) return;
            SetCurrentExceptionState(clrException is null ? null : new HandledException(clrException));
        }

        /// <summary>
        /// Re-raises the exception of a 'with' block after __exit__ returned false.
        /// </summary>
        [Obsolete("No longer emitted")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Exception MakeRethrowExceptionWorker(Exception e) {
            e.RemoveTraceBack();
            return ExceptionHelpers.UpdateForRethrow(e);
        }

        [Obsolete("Use MakeDictFromKeyValueItems")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static PythonDictionary MakeDictFromItems(params object[] data) {
            return new PythonDictionary(new CommonDictionaryStorage(SwapPairs(data), false));
        }

        [Obsolete("Use MakeHomogeneousDictFromKeyValueItems")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static PythonDictionary MakeHomogeneousDictFromItems(object[] data) {
            return new PythonDictionary(new CommonDictionaryStorage(SwapPairs(data), true));
        }

        [Obsolete("Use MakeConstantDictStorageFromKeyValueItems")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static object MakeConstantDictStorage(params object[] data) {
            return new ConstantDictionaryStorage(new CommonDictionaryStorage(SwapPairs(data), false));
        }

        // turns value/key pairs into key/value pairs
        private static object[] SwapPairs(object[] data) {
            var res = new object[data.Length];
            for (int i = 0; i < data.Length; i += 2) {
                res[i] = data[i + 1];
                res[i + 1] = data[i];
            }
            return res;
        }
    }
}

#endif
