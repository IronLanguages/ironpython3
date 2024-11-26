using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronPython.Runtime {
    /// <summary>
    /// Marks a class as being hidden from the Python hierarchy.  This is applied to the base class
    /// and then all derived types will not see the base class in their hierarchy and will not be
    /// able to access members declaredo on the base class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PythonHiddenBaseClassAttribute : Attribute {
    }
}
