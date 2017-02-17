using System;
using System.Collections.Generic;

using Microsoft.Scripting;

using IronPython.Runtime;

[assembly: PythonModule("atexit", typeof(IronPython.Modules.PythonAtExit))]
namespace IronPython.Modules
{
    public static class PythonAtExit
    {
        public static void register(object func, [ParamDictionary]IDictionary<object, object> kwargs, params object[] args) {
            // TODO: implement this
        }

        public static void unregister(object func) {
            // TODO: implement this
        }

        public static void _clear() {
            // TODO: implement this
        }
    }
}