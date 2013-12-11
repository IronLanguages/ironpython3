/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

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