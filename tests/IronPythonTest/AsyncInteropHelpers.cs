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
        /// Returns a real async Task&lt;int&gt; with a delay.
        /// The runtime type will be AsyncStateMachineBox, not Task&lt;int&gt; directly.
        /// </summary>
        public static async Task<int> GetAsyncInt(int value, int delayMs = 50) {
            await Task.Delay(delayMs);
            return value;
        }

        /// <summary>
        /// Returns a real async Task&lt;string&gt; with a delay.
        /// </summary>
        public static async Task<string> GetAsyncString(string value, int delayMs = 50) {
            await Task.Delay(delayMs);
            return value;
        }

        /// <summary>
        /// Returns a real async Task (void result) with a delay.
        /// </summary>
        public static async Task DoAsync(int delayMs = 50) {
            await Task.Delay(delayMs);
        }

        /// <summary>
        /// Async Task&lt;int&gt; that respects a CancellationToken.
        /// Throws OperationCanceledException if token is cancelled during the delay.
        /// </summary>
        public static async Task<int> GetAsyncIntWithCancellation(int value, CancellationToken token, int delayMs = 5000) {
            await Task.Delay(delayMs, token);
            return value;
        }

        /// <summary>
        /// Async Task that respects a CancellationToken.
        /// </summary>
        public static async Task DoAsyncWithCancellation(CancellationToken token, int delayMs = 5000) {
            await Task.Delay(delayMs, token);
        }

        /// <summary>
        /// IAsyncEnumerable&lt;int&gt; that yields values with delay and respects cancellation.
        /// </summary>
        public static IAsyncEnumerable<int> GetAsyncIntsWithCancellation(CancellationToken token, params int[] values) {
            return YieldIntsWithCancellation(values, token);
        }

        private static async IAsyncEnumerable<int> YieldIntsWithCancellation(
            int[] values,
            CancellationToken token,
            [EnumeratorCancellation] CancellationToken ct = default) {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, ct);
            foreach (var v in values) {
                await Task.Delay(50, linked.Token);
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
