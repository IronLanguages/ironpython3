// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace IronPython.Runtime.Operations {
    public static class UIntPtrOps {

        #region Binary Operations - Comparisons

        [SpecialName]
        public static bool Equals(UIntPtr x, UIntPtr y) => x == y;
        [SpecialName]
        public static bool NotEquals(UIntPtr x, UIntPtr y) => x != y;

        #endregion

    }
}
