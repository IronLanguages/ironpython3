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
using System.Diagnostics;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;

namespace IronPython.Runtime {
    /// <summary>
    /// Cached global value.  Created and maintained on a per-language basis.  Default
    /// implementation returns a singleton which indicates caching is not occuring.
    /// </summary>
    public sealed class ModuleGlobalCache {
        private object _value;

        internal static readonly object NotCaching = new object();
        internal static readonly ModuleGlobalCache NoCache = new ModuleGlobalCache(NotCaching);

        /// <summary>
        /// Creates a new ModuleGlobalCache with the specified value.
        /// </summary>
        public ModuleGlobalCache(object value) {
            _value = value;
        }

        /// <summary>
        /// True if the ModuleGlobalCache is participating in a caching strategy.
        /// </summary>
        public bool IsCaching {
            get {
                return _value != NotCaching;
            }
        }

        /// <summary>
        /// True if there is currently a value associated with this global variable.  False if
        /// it is currently unassigned.
        /// </summary>
        public bool HasValue {
            get {
                return _value != Uninitialized.Instance;
            }
        }

        /// <summary>
        /// Gets or sets the current cached value
        /// </summary>
        public object Value {
            get {
                return _value;
            }
            set {
                if (_value == NotCaching) throw new ValueErrorException("Cannot change non-caching value.");
                _value = value;
            }
        }

        /// <summary>
        /// Event handler for when the value has changed.  Language implementors should call this when
        /// the cached value is invalidated.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2109:ReviewVisibleEventHandlers")]
        public void Changed(object sender, ModuleChangeEventArgs e) {
            ContractUtils.RequiresNotNull(e, "e");

            switch (e.ChangeType) {
                case ModuleChangeType.Delete: Value = Uninitialized.Instance; break;
                case ModuleChangeType.Set: Value = e.Value; break;
                default: Debug.Assert(false, "unknown ModuleChangeType"); break;
            }
        }
    }
}
