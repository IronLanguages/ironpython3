using System;
using System.Collections.Generic;
using System.Text;

namespace IronPython.Runtime.Binding {
    interface IPythonSite {
        /// <summary>
        /// Gets the PythonContext which the CallSiteBinder is associated with.
        /// </summary>
        PythonContext/*!*/ Context {
            get;
        }
    }
}
