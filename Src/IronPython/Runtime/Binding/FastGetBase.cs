// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    /// <summary>
    /// Base class for all of our fast get delegates.  This holds onto the
    /// delegate and provides the Update function.
    /// </summary>
    internal abstract class FastGetBase {
        internal Func<CallSite, object, CodeContext, object> _func;
        internal int _hitCount;

        public abstract bool IsValid(PythonType type);

        internal virtual bool ShouldCache {
            get {
                return true;
            }
        }

        internal bool ShouldUseNonOptimizedSite {
            get {
                return _hitCount < 100;
            }
        }

        /// <summary>
        /// Updates the call site when the current rule is no longer applicable.
        /// </summary>
        protected static object Update(CallSite site, object self, CodeContext context) {
            return ((CallSite<Func<CallSite, object, CodeContext, object>>)site).Update(site, self, context);
        }
    }
}