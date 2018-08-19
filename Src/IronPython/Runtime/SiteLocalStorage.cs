// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace IronPython.Runtime {
    public abstract class SiteLocalStorage {
    }

    /// <summary>
    /// Provides storage which is flowed into a callers site.  The same storage object is 
    /// flowed for multiple calls enabling the callee to cache data that can be re-used
    /// across multiple calls.
    /// 
    /// Data is a public field so that this works properly with DynamicSite's as the reference
    /// type (and EnsureInitialize)
    /// </summary>
    public class SiteLocalStorage<T> : SiteLocalStorage {
        public T Data;
    }
}
