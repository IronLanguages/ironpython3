// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if NET

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace IronPythonTest {
    /// <summary>
    /// Provides IAsyncEnumerable test helpers accessible from Python via clr.AddReference('IronPythonTest').
    /// </summary>
    public static class AsyncInteropHelpers {
        /// <summary>
        /// Returns an IAsyncEnumerable&lt;int&gt; that yields the given values.
        /// </summary>
        public static IAsyncEnumerable<int> GetAsyncInts(params int[] values) {
            return YieldInts(values);
        }

        private static async IAsyncEnumerable<int> YieldInts(
            int[] values,
            [EnumeratorCancellation] CancellationToken ct = default) {
            foreach (var v in values) {
                await Task.Yield();
                ct.ThrowIfCancellationRequested();
                yield return v;
            }
        }

        /// <summary>
        /// Returns an IAsyncEnumerable&lt;string&gt; that yields the given values.
        /// </summary>
        public static IAsyncEnumerable<string> GetAsyncStrings(params string[] values) {
            return YieldStrings(values);
        }

        private static async IAsyncEnumerable<string> YieldStrings(
            string[] values,
            [EnumeratorCancellation] CancellationToken ct = default) {
            foreach (var v in values) {
                await Task.Yield();
                yield return v;
            }
        }

        /// <summary>
        /// Returns an empty IAsyncEnumerable&lt;int&gt;.
        /// </summary>
        public static IAsyncEnumerable<int> GetEmptyAsyncInts() {
            return EmptyAsyncEnumerable();
        }

        private static async IAsyncEnumerable<int> EmptyAsyncEnumerable(
            [EnumeratorCancellation] CancellationToken ct = default) {
            await Task.CompletedTask;
            yield break;
        }
    }
}

#endif
