using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// Wrapper class used when a user defined type (new-style or old-style)
    /// defines __index__.  We provide a conversion from all user defined
    /// types to the Index type so they can be used for determing and method bind
    /// time the most appropriate method to dispatch to.
    /// </summary>
    public class Index {
        private readonly object _value;

        public Index(object/*!*/ value) {
            ContractUtils.RequiresNotNull(value, "value");

            _value = value;
        }

        internal object/*!*/ Value {
            get {
                return _value;
            }
        }
    }
}
