using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;

using IronPython.Runtime;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Exceptions;

[assembly: PythonModule("atexit", typeof(IronPython.Modules.PythonAtExit))]
namespace IronPython.Modules {
    public static class PythonAtExit {
        public const string __doc__ = @"allow programmer to define multiple exit functions to be executed upon normal program termination.

Two public functions, register and unregister, are defined.
";

        private static readonly object _registry_key = new object();

        private class FunctionDescriptor {
            public FunctionDescriptor(object func, IDictionary<object, object> kwargs, object[] args) {
                Func = func;
                KeywordArgs = kwargs;
                Args = args;
            }

            public object Func {
                get; 
            }

            public IDictionary<object, object> KeywordArgs {
                get; 
            }

            public object[] Args {
                get; 
            }
        }

        [SpecialName]
        public static void PerformModuleReload(PythonContext context, PythonDictionary dict) {
            if (!context.HasModuleState(_registry_key))
                context.SetModuleState(_registry_key, new List<FunctionDescriptor>());

            context.SetSystemStateValue("exitfunc", (Action<CodeContext>)_run_exitfuncs);
        }

        [Documentation(@"register(func, *args, **kwargs) -> func

Register a function to be executed upon normal program termination\n\

    func - function to be called at exit
    args - optional arguments to pass to func
    kwargs - optional keyword arguments to pass to func

    func is returned to facilitate usage as a decorator.")]
        public static object register(CodeContext context, object func, [ParamDictionary]IDictionary<object, object> kwargs, params object[] args) {
            if (!PythonOps.IsCallable(context, func)) {
                throw PythonOps.TypeError("the first argument must be callable");
            }

            var functions = context.GetPythonContext().GetModuleState(_registry_key) as List<FunctionDescriptor>;
            if (functions != null) {
                lock (functions) {
                    functions.Add(new FunctionDescriptor(func, kwargs, args));
                }
            }
            return func;
        }

        [Documentation(@"unregister(func) -> None

Unregister an exit function which was previously registered using
atexit.register

    func - function to be unregistered")]
        public static void unregister(CodeContext context, object func) {
            var functions = context.GetPythonContext().GetModuleState(_registry_key) as List<FunctionDescriptor>;
            if (functions != null) {
                lock (functions) {
                    functions.RemoveAll(x => PythonOps.Compare(context, x.Func, func) == 0);
                }
            }
        }

        [Documentation(@"_clear() -> None

Clear the list of previously registered exit functions.")]
        public static void _clear(CodeContext context) {
            var functions = context.GetPythonContext().GetModuleState(_registry_key) as List<FunctionDescriptor>;
            if (functions != null) {
                lock (functions) {
                    functions.Clear();
                }
            }
        }

        [Documentation(@"_run_exitfuncs() -> None

Run all registered exit functions.")]
        public static void _run_exitfuncs(CodeContext context) {
            var pc = context.GetPythonContext();
            var functions = pc.GetModuleState(_registry_key) as List<FunctionDescriptor>;
            if (functions != null) {
                Exception lastException = null;
                lock (functions) {
                    for (int i = functions.Count - 1; i >= 0; i--) {
                        var func = functions[i];
                        try {
                            if (func.KeywordArgs.Count > 0) {
                                pc.CallWithKeywordsAndContext(context, func.Func, func.Args, func.KeywordArgs);
                            } else {
                                pc.CallWithContext(context, func.Func, func.Args);
                            }
                        } catch (SystemExitException ex) {
                            lastException = ex;
                        } catch (Exception ex) {
                            lastException = ex;
                            PythonOps.PrintWithDest(context, pc.SystemStandardError, "Error in atexit._run_exitfuncs:\n");
                            PythonOps.PrintException(context, ex);
                        }
                    }
                }

                if (lastException != null) {
                    throw lastException;
                }
            }
        }

        [Documentation(@"_ncallbacks() -> int

Return the number of registered exit functions.")]
        public static int _ncallbacks(CodeContext context) {
            int result = 0;
            var functions = context.GetPythonContext().GetModuleState(_registry_key) as List<FunctionDescriptor>;
            if (functions != null) {
                lock (functions) {
                    result = functions.Count;
                }
            }
            return result;
        }
    }
}