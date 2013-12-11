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

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    /// <summary>
    /// Base class for all of our fast get delegates.  This holds onto the
    /// delegate and provides the Update function.
    /// </summary>
    abstract class FastGetBase {
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