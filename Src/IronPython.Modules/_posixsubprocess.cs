#if FEATURE_NATIVE

using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using System.Numerics;

[assembly: PythonModule("_posixsubprocess", typeof(IronPython.Modules.PosixSubprocess), PythonModuleAttribute.PlatformFamily.Unix)]
namespace IronPython.Modules {
    public class PosixSubprocess {
        public const string __doc__ = "A POSIX helper for the subprocess module.";


        [Documentation(@"fork_exec(args, executable_list, close_fds, cwd, env,
          p2cread, p2cwrite, c2pread, c2pwrite,
          errread, errwrite, errpipe_read, errpipe_write,
          restore_signals, call_setsid, preexec_fn)

Forks a child process, closes parent file descriptors as appropriate in the
child and dups the few that are needed before calling exec() in the child
process.

The preexec_fn, if supplied, will be called immediately before exec.
WARNING: preexec_fn is NOT SAFE if your application uses threads.
         It may trigger infrequent, difficult to debug deadlocks.

If an error occurs in the child process before the exec, it is
serialized and written to the errpipe_write fd per subprocess.py.

Returns: the child process's PID.

Raises: Only on an error in the parent process.
")]
        public static object fork_exec(CodeContext/*!*/ context, params object[] args) {
            throw PythonOps.NotImplementedError("fork_exec is currently not implemented");
        }
    }
}

#endif