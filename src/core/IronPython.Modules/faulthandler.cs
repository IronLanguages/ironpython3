// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.InteropServices;

using IronPython.Runtime;

[assembly: PythonModule("faulthandler", typeof(IronPython.Modules.PythonFaultHandler))]
namespace IronPython.Modules {
    public static class PythonFaultHandler {
        private const int STDERR = 2;

        public static void dump_traceback(CodeContext context, [DefaultParameterValue(STDERR)] object? file, bool all_threads = true) {
            // TODO: the default file object should be sys.stderr

            // TODO: fill this up
            throw new NotImplementedException();
        }

        public static void enable(CodeContext context, [DefaultParameterValue(STDERR)] object? file, bool all_threads = true) {
            // TODO: the default file object should be sys.stderr

            // TODO: fill this up
        }
    }
}