// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace IronPythonTest {
    public class DefaultParams {
        public static int FuncWithDefaults(int x=1,
           int y=2,
           int z=3) {
            return x + y + z;
        }
    }
}
