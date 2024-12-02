// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Numerics;

namespace IronPython.Runtime.Operations {
    public static class DateTimeOps {
        public static PythonTuple __getnewargs__(DateTime self) => PythonTuple.MakeTuple((BigInteger)self.Ticks, self.Kind);
    }
}
