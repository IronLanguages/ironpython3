using System;
using System.Runtime.InteropServices;

using IronPython.Runtime;

[assembly: PythonModule("faulthandler", typeof(IronPython.Modules.PythonFaultHandler))]
namespace IronPython.Modules {
    public static class PythonFaultHandler {
        private const int STDERR = 2;

        public static void dump_traceback(CodeContext context, [DefaultParameterValue(STDERR)]object file, [DefaultParameterValue(true)]bool all_threads) {
            // TODO: the default file object should be sys.stderr

            // TODO: fill this up
            throw new NotImplementedException();
        }        
    }
}