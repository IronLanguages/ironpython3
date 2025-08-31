// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_CTYPES

using System;

namespace IronPython.Modules {
    internal static class CTypesExtensionMethods {
        public static IntPtr Add(this IntPtr self, int offset) {
            return new IntPtr(checked(self.ToInt64() + offset));
        }
    }
}

#endif
