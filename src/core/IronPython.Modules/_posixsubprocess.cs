// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_NATIVE

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

[assembly: PythonModule("_posixsubprocess", typeof(IronPython.Modules.PosixSubprocess), PlatformsAttribute.PlatformFamily.Unix)]
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
