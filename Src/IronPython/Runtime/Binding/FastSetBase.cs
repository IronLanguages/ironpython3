// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Binding {
    class FastSetBase {
        internal Delegate _func;
        internal int _version;
        internal int _hitCount;

        public FastSetBase(int version) {
            _version = version;
        }

        public bool ShouldUseNonOptimizedSite {
            get {
                return _hitCount < 100;
            }
        }
    }

    /// <summary>
    /// Base class for all of our fast set delegates.  This holds onto the
    /// delegate and provides the Update and Optimize functions.
    /// </summary>
    class FastSetBase<TValue> : FastSetBase {
        public FastSetBase(int version) : base(version) {
        }

        /// <summary>
        /// Updates the call site when the current rule is no longer applicable.
        /// </summary>
        protected static object Update(CallSite site, object self, TValue value) {
            return ((CallSite<Func<CallSite, object, TValue, object>>)site).Update(site, self, value);
        }
    }
}