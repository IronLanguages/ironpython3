using System;
using System.Collections.Generic;

using Microsoft.Scripting;

using IronPython.Runtime;
using Microsoft.Scripting.Runtime;

[assembly: PythonModule("atexit", typeof(IronPython.Modules.PythonAtExit))]
namespace IronPython.Modules
{
    [Documentation(@"allow programmer to define multiple exit functions to be executed upon normal program termination.

Two public functions, register and unregister, are defined.
")]
    public static class PythonAtExit
    {
        [Documentation(@"register(func, *args, **kwargs) -> func

Register a function to be executed upon normal program termination\n\

    func - function to be called at exit
    args - optional arguments to pass to func
    kwargs - optional keyword arguments to pass to func

    func is returned to facilitate usage as a decorator.")]
        public static void register(object func, [ParamDictionary]IDictionary<object, object> kwargs, params object[] args) {
            // TODO: implement this
        }

        [Documentation(@"unregister(func) -> None

Unregister an exit function which was previously registered using
atexit.register

    func - function to be unregistered")]
        public static void unregister(object func) {
            // TODO: implement this
        }

        [Documentation(@"_clear() -> None

Clear the list of previously registered exit functions.")]
        public static void _clear() {
            // TODO: implement this
        }

        [Documentation(@"_run_exitfuncs() -> None

Run all registered exit functions.")]
        public static void _run_exitfuncs() {
            // TODO: implement this
        }

        [Documentation(@"_ncallbacks() -> int

Return the number of registered exit functions.")]
        public static int _ncallbacks() {
            // TODO: implement this
            return 0;
        }
    }
}