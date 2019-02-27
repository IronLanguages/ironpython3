// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

#if FEATURE_PIPES
using System.IO.Pipes;
#endif

[assembly: PythonModule("nt", typeof(IronPython.Modules.PythonNT))]
namespace IronPython.Modules {
    public static class PythonNT {
        public const string __doc__ = "Provides low-level operating system access for files, the environment, etc...";

#if FEATURE_PROCESS
        private static Dictionary<int, Process> _processToIdMapping = new Dictionary<int, Process>();
        private static List<int> _freeProcessIds = new List<int>();
        private static int _processCount;
#endif

        private static readonly object _keyFields = new object();
        private static readonly string _keyHaveFunctions = "_have_functions";

        [SpecialName]
        public static void PerformModuleReload(PythonContext context, PythonDictionary dict) {
            var have_functions = new PythonList();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
                have_functions.Add("MS_WINDOWS");
            }

            context.GetOrCreateModuleState(_keyFields, () => {
                dict.Add(_keyHaveFunctions, have_functions);
                return dict;
            });
        }

        #region Public API Surface

#if FEATURE_PROCESS
        public static void abort() {
            System.Environment.FailFast("IronPython os.abort");
        }
#endif

        /// <summary>
        /// Checks for the specific permissions, provided by the mode parameter, are available for the provided path.  Permissions can be:
        /// 
        /// F_OK: Check to see if the file exists
        /// R_OK | W_OK | X_OK: Check for the specific permissions.  Only W_OK is respected.
        /// </summary>
        public static bool access(CodeContext/*!*/ context, string path, int mode) {
            if (path == null) throw PythonOps.TypeError("expected string, got None");

#if FEATURE_FILESYSTEM
            try {
                FileAttributes fa = File.GetAttributes(path);
                if (mode == F_OK) {
                    return true;
                }
                // match the behavior of the VC C Runtime
                if ((fa & FileAttributes.Directory) != 0) {
                    // directories have read & write access
                    return true;
                }
                if ((fa & FileAttributes.ReadOnly) != 0 && (mode & W_OK) != 0) {
                    // want to write but file is read-only
                    return false;
                }
                return true;
            } catch (ArgumentException) {
            } catch (PathTooLongException) {
            } catch (NotSupportedException) {
            } catch (FileNotFoundException) {
            } catch (DirectoryNotFoundException) {
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
            return false;
#else
            throw new NotImplementedException();
#endif
        }

#if FEATURE_FILESYSTEM

        public static void chdir([NotNull]string path) {
            if (String.IsNullOrEmpty(path)) {
                throw PythonExceptions.CreateThrowable(WindowsError, PythonExceptions._OSError.ERROR_INVALID_NAME, "Path cannot be an empty string", null, PythonExceptions._OSError.ERROR_INVALID_NAME);
            }

            try {
                Directory.SetCurrentDirectory(path);
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

        // Isolate Mono.Unix from the rest of the method so that we don't try to load the Mono.Posix assembly on Windows.
        private static void chmodUnix(string path, int mode) {
            if (Mono.Unix.Native.Syscall.chmod(path, Mono.Unix.Native.NativeConvert.ToFilePermissions((uint)mode)) == 0) return;
            var error = Marshal.GetLastWin32Error();
            throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, error, strerror(error));
        }

        public static void chmod(string path, int mode, [ParamDictionary]IDictionary<string, object> dict) {
            if (dict != null) {
                foreach (var key in dict.Keys) {
                    switch (key) {
                        case "dir_fd":
                        case "follow_symlinks":
                            // TODO: implement these!
                            break;
                        default:
                            throw PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", key);
                    }
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                try {
                    FileInfo fi = new FileInfo(path);
                    if ((mode & S_IWRITE) != 0) {
                        fi.Attributes &= ~(FileAttributes.ReadOnly);
                    } else {
                        fi.Attributes |= FileAttributes.ReadOnly;
                    }
                } catch (Exception e) {
                    throw ToPythonException(e, path);
                }
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                chmodUnix(path, mode);
            } else {
                throw new PlatformNotSupportedException();
            }
        }

#endif

        public static void close(CodeContext/*!*/ context, int fd) {
            PythonContext pythonContext = context.LanguageContext;
            PythonFileManager fileManager = pythonContext.FileManager;
            if (fileManager.TryGetFileFromId(pythonContext, fd, out PythonIOModule.FileIO file)) {
                fileManager.CloseIfLast(context, fd, file);
            } else {
                Stream stream = fileManager.GetObjectFromId(fd) as Stream;
                if (stream == null) {
                    throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
                }
                fileManager.CloseIfLast(fd, stream);
            }
        }

        public static void closerange(CodeContext/*!*/ context, int fd_low, int fd_high) {
            for (var fd = fd_low; fd <= fd_high; fd++) {
                try {
                    close(context, fd);
                } catch (OSException) {
                    // ignore errors on close
                }
            }
        }
        private static bool IsValidFd(CodeContext/*!*/ context, int fd) {
            PythonContext pythonContext = context.LanguageContext;
            if (pythonContext.FileManager.TryGetFileFromId(pythonContext, fd, out PythonIOModule.FileIO _)) {
                return true;
            }
            if (pythonContext.FileManager.TryGetObjectFromId(pythonContext, fd, out object o)) {
                if (o is Stream) {
                    return true;
                }
            }
            return false;
        }

        public static int dup(CodeContext/*!*/ context, int fd) {
            PythonContext pythonContext = context.LanguageContext;
            if (pythonContext.FileManager.TryGetFileFromId(pythonContext, fd, out PythonIOModule.FileIO file)) {
                return pythonContext.FileManager.AddToStrongMapping(file);
            } else {
                Stream stream = pythonContext.FileManager.GetObjectFromId(fd) as Stream;
                if (stream == null) {
                    throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
                }
                return pythonContext.FileManager.AddToStrongMapping(stream);
            }
        }


        public static int dup2(CodeContext/*!*/ context, int fd, int fd2) {
            PythonContext pythonContext = context.LanguageContext;

            if (!IsValidFd(context, fd)) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
            }

            if (!pythonContext.FileManager.ValidateFdRange(fd2)) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
            }

            bool fd2Valid = IsValidFd(context, fd2);

            if (fd == fd2) {
                if (fd2Valid) {
                    return fd2;
                }
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
            }

            if (fd2Valid) {
                close(context, fd2);
            }

            if (pythonContext.FileManager.TryGetFileFromId(pythonContext, fd, out PythonIOModule.FileIO file)) {
                return pythonContext.FileManager.AddToStrongMapping(file, fd2);
            }
            var stream = pythonContext.FileManager.GetObjectFromId(fd) as Stream;
            if (stream == null) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
            }
            return pythonContext.FileManager.AddToStrongMapping(stream, fd2);
        }



#if FEATURE_PROCESS
        /// <summary>
        /// single instance of environment dictionary is shared between multiple runtimes because the environment
        /// is shared by multiple runtimes.
        /// </summary>
        public static readonly object environ = new PythonDictionary(new EnvironmentDictionaryStorage());
#endif

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType error = Builtin.OSError;

        public static void _exit(CodeContext/*!*/ context, int code) {
            context.LanguageContext.DomainManager.Platform.TerminateScriptExecution(code);
        }

        [LightThrowing]
        public static object fstat(CodeContext/*!*/ context, int fd) {
            PythonContext pythonContext = context.LanguageContext;
            if (pythonContext.FileManager.TryGetFileFromId(pythonContext, fd, out PythonIOModule.FileIO file) && file.name is string strName) {
                return lstat(strName);
            }
            throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
        }

        public static void fsync(CodeContext context, int fd) {
            PythonContext pythonContext = context.LanguageContext;
            var pf = pythonContext.FileManager.GetFileFromId(pythonContext, fd);
            try {
                pf.flush(context);
            } catch (Exception ex) when (ex is ValueErrorException || ex is IOException) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 9, "Bad file descriptor");
            }
        }

        public static string getcwd(CodeContext/*!*/ context) {
            return context.LanguageContext.DomainManager.Platform.CurrentDirectory;
        }

        public static Bytes getcwdb(CodeContext/*!*/ context) {
            var encoding = SysModule.getfilesystemencoding() as string;
            return StringOps.encode(context, context.LanguageContext.DomainManager.Platform.CurrentDirectory, encoding);
        }

#if NETCOREAPP2_1 || NETSTANDARD2_0
        private static readonly char[] invalidPathChars = new char[] { '\"', '<', '>' };
#endif

        public static string _getfullpathname(CodeContext/*!*/ context, [NotNull]string/*!*/ dir) {
            PlatformAdaptationLayer pal = context.LanguageContext.DomainManager.Platform;

            try {
                return pal.GetFullPath(dir);
            } catch (ArgumentException) {
                // .NET validates the path, CPython doesn't... so we replace invalid chars with 
                // Char.Maxvalue, get the full path, and then replace the Char.Maxvalue's back w/ 
                // their original value.
                string newdir = dir;

                if (IsWindows()) {
                    if (newdir.Length >= 2 && newdir[1] == ':' &&
                        (newdir[0] < 'a' || newdir[0] > 'z') && (newdir[0] < 'A' || newdir[0] > 'Z')) {
                        // invalid drive, .NET will reject this
                        if (newdir.Length == 2) {
                            return newdir + Path.DirectorySeparatorChar;
                        } else if (newdir[2] == Path.DirectorySeparatorChar) {
                            return newdir;
                        } else {
                            return newdir.Substring(0, 2) + Path.DirectorySeparatorChar + newdir.Substring(2);
                        }
                    }
                    if (newdir.Length > 2 && newdir.IndexOf(':', 2) != -1) {
                        // : is an invalid char if it's not in the 2nd position
                        newdir = newdir.Substring(0, 2) + newdir.Substring(2).Replace(':', Char.MaxValue);
                    }

                    if (newdir.Length > 0 && newdir[0] == ':') {
                        newdir = Char.MaxValue + newdir.Substring(1);
                    }
                }

                foreach (char c in Path.GetInvalidPathChars()) {
                    newdir = newdir.Replace(c, Char.MaxValue);
                }

#if NETCOREAPP2_1 || NETSTANDARD2_0
                foreach (char c in invalidPathChars) {
                    newdir = newdir.Replace(c, Char.MaxValue);
                }
#endif

                foreach (char c in Path.GetInvalidFileNameChars()) {
                    // don't replace the volume or directory separators
                    if (c == Path.VolumeSeparatorChar || c == Path.DirectorySeparatorChar) continue;
                    newdir = newdir.Replace(c, Char.MaxValue);
                }

                // walk backwards through the path replacing the same characters.  We should have
                // only updated the directory leaving the filename which we're fixing.
                string res = pal.GetFullPath(newdir);
                int curDir = dir.Length;
                for (int curRes = res.Length - 1; curRes >= 0; curRes--) {
                    if (res[curRes] == Char.MaxValue) {
                        for (curDir--; curDir >= 0; curDir--) {
                            if (newdir[curDir] == Char.MaxValue) {
                                res = res.Substring(0, curRes) + dir[curDir] + res.Substring(curRes + 1);
                                break;
                            }
                        }
                    }
                }

                return res;
            }
        }

        private static bool IsWindows() {
            return Environment.OSVersion.Platform == PlatformID.Win32NT ||
                Environment.OSVersion.Platform == PlatformID.Win32S ||
                Environment.OSVersion.Platform == PlatformID.Win32Windows;
        }

#if FEATURE_PROCESS
        public static int getpid() {
            return System.Diagnostics.Process.GetCurrentProcess().Id;
        }
#endif

        public static PythonList listdir(CodeContext/*!*/ context, [NotNull]string path) {
            if (path == String.Empty) {
                throw PythonExceptions.CreateThrowable(WindowsError, PythonExceptions._OSError.ERROR_PATH_NOT_FOUND, "The system cannot find the path specified: ''", null, PythonExceptions._OSError.ERROR_PATH_NOT_FOUND);
            }

            PythonList ret = PythonOps.MakeList();
            try {
                addBase(context.LanguageContext.DomainManager.Platform.GetFileSystemEntries(path, "*"), ret);
                return ret;
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

        public static void lseek(CodeContext context, int filedes, long offset, int whence) {
            var file = context.LanguageContext.FileManager.GetFileFromId(context.LanguageContext, filedes);

            file.seek(context, offset, whence);
        }

        /// <summary>
        /// lstat(path) -> stat result 
        /// Like stat(path), but do not follow symbolic links.
        /// </summary>
        [LightThrowing]
        public static object lstat(string path) {
            // TODO: detect links
            return stat(path, null);
        }

        [LightThrowing]
        public static object lstat([BytesConversion]IList<byte> path)
            => lstat(PythonOps.MakeString(path));


#if FEATURE_NATIVE

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static void symlink(string source, string link_name) {
            int result = Mono.Unix.Native.Syscall.symlink(source, link_name);
            if (result != 0) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, 0, source, link_name);
            }
        }

        [PythonType("uname_result"), PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public class uname_result : PythonTuple {
            public uname_result(string sysname, string nodename, string release, string version, string machine) :
                base(new object[] { sysname, nodename, release, version, machine }) {
                
            }

            public string sysname => (string)this[0];

            public string nodename => (string)this[1];

            public string release => (string)this[2];

            public string version => (string)this[3];

            public string machine => (string)this[4];

            public override string ToString() {
                return $"posix.uname_result(sysname='{sysname}', nodename='{nodename}', release='{release}', version='{version}', machine='{machine}')";
            }
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static uname_result uname() {
            Mono.Unix.Native.Utsname info;
            Mono.Unix.Native.Syscall.uname(out info);
            return new uname_result(info.sysname, info.nodename, info.release, info.version, info.machine);
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static BigInteger getuid() {
            return Mono.Unix.Native.Syscall.getuid();
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static BigInteger geteuid() {
            return Mono.Unix.Native.Syscall.geteuid();
        }

#endif

#if FEATURE_FILESYSTEM
        public static void mkdir(string path) {
            if (Directory.Exists(path))
                throw DirectoryExists();

            try {
                Directory.CreateDirectory(path);
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

        public static void mkdir(string path, int mode) {
            if (Directory.Exists(path)) throw DirectoryExists();
            // we ignore mode

            try {
                Directory.CreateDirectory(path);
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

        public static object open(CodeContext/*!*/ context, string filename, int flag) {
            return open(context, filename, flag, 0777);
        }

        private const int DefaultBufferSize = 4096;

        public static object open(CodeContext/*!*/ context, string filename, int flag, int mode) {
            try {
                FileMode fileMode = FileModeFromFlags(flag);
                FileAccess access = FileAccessFromFlags(flag);
                FileOptions options = FileOptionsFromFlags(flag);
                Stream fs;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && string.Equals(filename, "nul", StringComparison.OrdinalIgnoreCase)
                   || (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) && filename == "/dev/null") {
                    fs = Stream.Null;
                } else if (access == FileAccess.Read && (fileMode == FileMode.CreateNew || fileMode == FileMode.Create || fileMode == FileMode.Append)) {
                    // .NET doesn't allow Create/CreateNew w/ access == Read, so create the file, then close it, then
                    // open it again w/ just read access.
                    fs = new FileStream(filename, fileMode, FileAccess.Write, FileShare.None);
                    fs.Close();
                    fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, DefaultBufferSize, options);
                } else if (access == FileAccess.ReadWrite && fileMode == FileMode.Append) {
                    fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, DefaultBufferSize, options);
                } else {
                    fs = new FileStream(filename, fileMode, access, FileShare.ReadWrite, DefaultBufferSize, options);
                }

                string mode2;
                if (fs.CanRead && fs.CanWrite) mode2 = "w+";
                else if (fs.CanWrite) mode2 = "w";
                else mode2 = "r";

                if ((flag & O_BINARY) != 0) {
                    mode2 += "b";
                }

                return context.LanguageContext.FileManager.AddToStrongMapping(new PythonIOModule.FileIO(context, fs));
            } catch (Exception e) {
                throw ToPythonException(e, filename);
            }
        }

        private static FileOptions FileOptionsFromFlags(int flag) {
            FileOptions res = FileOptions.None;
            if ((flag & O_TEMPORARY) != 0) {
                res |= FileOptions.DeleteOnClose;
            }
            if ((flag & O_RANDOM) != 0) {
                res |= FileOptions.RandomAccess;
            }
            if ((flag & O_SEQUENTIAL) != 0) {
                res |= FileOptions.SequentialScan;
            }

            return res;
        }
#endif

#if FEATURE_PIPES
        private static Tuple<Stream, Stream> CreatePipeStreamsUnix() {
            Mono.Unix.UnixPipes pipes = Mono.Unix.UnixPipes.CreatePipes();
            return Tuple.Create<Stream, Stream>(pipes.Reading, pipes.Writing);
        }

        private static Tuple<Stream, Stream> CreatePipeStreams() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return CreatePipeStreamsUnix();
            } else {
                var inPipe = new AnonymousPipeServerStream(PipeDirection.In);
                var outPipe = new AnonymousPipeClientStream(PipeDirection.Out, inPipe.ClientSafePipeHandle);
                return Tuple.Create<Stream, Stream>(inPipe, outPipe);
            }
        }

        public static PythonTuple pipe(CodeContext context) {
            var pipeStreams = CreatePipeStreams();

            var inFile = new PythonIOModule.FileIO(context, pipeStreams.Item1);
            var outFile = new PythonIOModule.FileIO(context, pipeStreams.Item2);

            return PythonTuple.MakeTuple(
                context.LanguageContext.FileManager.AddToStrongMapping(inFile),
                context.LanguageContext.FileManager.AddToStrongMapping(outFile)
            );
        }
#endif

#if FEATURE_PROCESS
        public static void putenv(string varname, string value) {
            try {
                System.Environment.SetEnvironmentVariable(varname, value);
            } catch (Exception e) {
                throw ToPythonException(e);
            }
        }
#endif

        public static Bytes read(CodeContext/*!*/ context, int fd, int buffersize) {
            if (buffersize < 0) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, PythonErrorNumber.EINVAL, "Invalid argument");
            }

            try {
                PythonContext pythonContext = context.LanguageContext;
                var pf = pythonContext.FileManager.GetFileFromId(pythonContext, fd);
                return (Bytes)pf.read(context, buffersize);
            } catch (Exception e) {
                throw ToPythonException(e);
            }
        }

        public static void rename(string src, string dst) {
            try {
                Directory.Move(src, dst);
            } catch (Exception e) {
                throw ToPythonException(e);
            }
        }

        public static void rmdir(string path) {
            try {
                Directory.Delete(path);
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

#if FEATURE_PROCESS

        /// <summary>
        /// spawns a new process.
        /// 
        /// If mode is nt.P_WAIT then then the call blocks until the process exits and the return value
        /// is the exit code.
        /// 
        /// Otherwise the call returns a handle to the process.  The caller must then call nt.waitpid(pid, options)
        /// to free the handle and get the exit code of the process.  Failure to call nt.waitpid will result
        /// in a handle leak.
        /// </summary>
        public static object spawnv(CodeContext/*!*/ context, int mode, string path, object args) {
            return SpawnProcessImpl(context, MakeProcess(), mode, path, args);
        }

        /// <summary>
        /// spawns a new process.
        /// 
        /// If mode is nt.P_WAIT then then the call blocks until the process exits and the return value
        /// is the exit code.
        /// 
        /// Otherwise the call returns a handle to the process.  The caller must then call nt.waitpid(pid, options)
        /// to free the handle and get the exit code of the process.  Failure to call nt.waitpid will result
        /// in a handle leak.
        /// </summary>
        public static object spawnve(CodeContext/*!*/ context, int mode, string path, object args, object env) {
            Process process = MakeProcess();
            SetEnvironment(process.StartInfo.EnvironmentVariables, env);

            return SpawnProcessImpl(context, process, mode, path, args);
        }

        private static Process MakeProcess() {
            try {
                return new Process();
            } catch (Exception e) {
                throw ToPythonException(e);
            }
        }

        private static object SpawnProcessImpl(CodeContext/*!*/ context, Process process, int mode, string path, object args) {
            try {
                process.StartInfo.Arguments = ArgumentsToString(context, args);
                process.StartInfo.FileName = path;
                process.StartInfo.UseShellExecute = false;
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }

            if (!process.Start()) {
                throw PythonOps.OSError("Cannot start process: {0}", path);
            }
            if (mode == P_WAIT) {
                process.WaitForExit();
                int exitCode = process.ExitCode;
                process.Close();
                return exitCode;
            }

            lock (_processToIdMapping) {
                int id;
                if (_freeProcessIds.Count > 0) {
                    id = _freeProcessIds[_freeProcessIds.Count - 1];
                    _freeProcessIds.RemoveAt(_freeProcessIds.Count - 1);
                } else {
                    // process IDs are handles on CPython/Win32 so we match
                    // that behavior and return something that is handle like.  Handles
                    // on NT are guaranteed to have the low 2 bits not set and users
                    // could use these for their own purposes.  We therefore match that
                    // behavior here.
                    _processCount += 4;
                    id = _processCount;
                }

                _processToIdMapping[id] = process;
                return ScriptingRuntimeHelpers.Int32ToObject(id);
            }
        }

        /// <summary>
        /// Copy elements from a Python mapping of dict environment variables to a StringDictionary.
        /// </summary>
        private static void SetEnvironment(System.Collections.Specialized.StringDictionary currentEnvironment, object newEnvironment) {
            PythonDictionary env = newEnvironment as PythonDictionary;
            if (env == null) {
                throw PythonOps.TypeError("env argument must be a dict");
            }

            currentEnvironment.Clear();

            string strKey, strValue;
            foreach (object key in env.keys()) {
                if (!Converter.TryConvertToString(key, out strKey)) {
                    throw PythonOps.TypeError("env dict contains a non-string key");
                }
                if (!Converter.TryConvertToString(env[key], out strValue)) {
                    throw PythonOps.TypeError("env dict contains a non-string value");
                }
                currentEnvironment[strKey] = strValue;
            }
        }
#endif

        /// <summary>
        /// Convert a sequence of args to a string suitable for using to spawn a process.
        /// </summary>
        private static string ArgumentsToString(CodeContext/*!*/ context, object args) {
            IEnumerator argsEnumerator;
            System.Text.StringBuilder sb = null;
            if (!PythonOps.TryGetEnumerator(context, args, out argsEnumerator)) {
                throw PythonOps.TypeError("args parameter must be sequence, not {0}", DynamicHelpers.GetPythonType(args));
            }

            bool space = false;
            try {
                // skip the first element, which is the name of the command being run
                argsEnumerator.MoveNext();
                while (argsEnumerator.MoveNext()) {
                    if (sb == null) sb = new System.Text.StringBuilder(); // lazy creation
                    string strarg = PythonOps.ToString(argsEnumerator.Current);
                    if (space) {
                        sb.Append(' ');
                    }
                    if (strarg.IndexOf(' ') != -1) {
                        sb.Append('"');
                        // double quote any existing quotes
                        sb.Append(strarg.Replace("\"", "\"\""));
                        sb.Append('"');
                    } else {
                        sb.Append(strarg);
                    }
                    space = true;
                }
            } finally {
                IDisposable disposable = argsEnumerator as IDisposable;
                if (disposable != null) disposable.Dispose();
            }

            if (sb == null) return "";
            return sb.ToString();
        }

#if FEATURE_PROCESS
        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static void startfile(string filename, string operation = "open") {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = filename;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Verb = operation;
            try {
                process.Start();
            } catch (Exception e) {
                throw ToPythonException(e, filename);
            }
        }
#endif

        [PythonType]
        public class stat_result : PythonTuple {
            public const int n_fields = 16;
            public const int n_sequence_fields = 10;
            public const int n_unnamed_fields = 3;

            private const long nanosecondsPerSeconds = 1_000_000_000;

            internal stat_result(Mono.Unix.Native.Stat stat)
                : this(new object[16] {(int)stat.st_mode, stat.st_ino, stat.st_dev, stat.st_nlink, stat.st_uid, stat.st_gid, stat.st_size, stat.st_atime, stat.st_mtime, stat.st_ctime,
                      stat.st_atime + stat.st_atime_nsec / (double)nanosecondsPerSeconds, stat.st_mtime + stat.st_mtime_nsec / (double)nanosecondsPerSeconds, stat.st_ctime + stat.st_ctime_nsec / (double)nanosecondsPerSeconds,
                      stat.st_atime * nanosecondsPerSeconds + stat.st_atime_nsec, stat.st_mtime * nanosecondsPerSeconds + stat.st_mtime_nsec, stat.st_ctime * nanosecondsPerSeconds + stat.st_ctime_nsec }, null) { }

            internal stat_result(int mode, ulong fileidx, long size, long st_atime_ns, long st_mtime_ns, long st_ctime_ns)
                : this(new object[16] { mode, fileidx, 0, 0, 0, 0, size, st_atime_ns / nanosecondsPerSeconds, st_mtime_ns / nanosecondsPerSeconds, st_ctime_ns / nanosecondsPerSeconds,
                      st_atime_ns / (double)nanosecondsPerSeconds, st_mtime_ns / (double)nanosecondsPerSeconds, st_ctime_ns / (double)nanosecondsPerSeconds,
                      st_atime_ns, st_mtime_ns, st_ctime_ns }, null) { }

            private stat_result(object[] statResult, PythonDictionary dict) : base(statResult.Take(n_sequence_fields).ToArray()) {
                if (statResult.Length < n_sequence_fields) {
                    throw PythonOps.TypeError($"os.stat_result() takes an at least {n_sequence_fields}-sequence ({statResult.Length}-sequence given)");
                }
                else if (statResult.Length > n_fields) {
                    throw PythonOps.TypeError($"os.stat_result() takes an at least {n_sequence_fields}-sequence ({statResult.Length}-sequence given)");
                }

                object obj;
                if (statResult.Length >= 11) {
                    _atime = statResult[10];
                } else if (TryGetDictValue(dict, "st_atime", out obj)) {
                    _atime = obj;
                }

                if (statResult.Length >= 12) {
                    _mtime = statResult[11];
                } else if (TryGetDictValue(dict, "st_mtime", out obj)) {
                    _mtime = obj;
                }

                if (statResult.Length >= 13) {
                    _ctime = statResult[12];
                } else if (TryGetDictValue(dict, "st_ctime", out obj)) {
                    _ctime = obj;
                }

                if (statResult.Length >= 14) {
                    st_atime_ns = statResult[13];
                } else if (TryGetDictValue(dict, "st_atime_ns", out obj)) {
                    st_atime_ns = obj;
                }

                if (statResult.Length >= 15) {
                    st_mtime_ns = statResult[14];
                } else if (TryGetDictValue(dict, "st_mtime_ns", out obj)) {
                    st_mtime_ns = obj;
                }

                if (statResult.Length >= 16) {
                    st_ctime_ns = statResult[15];
                } else if (TryGetDictValue(dict, "st_ctime_ns", out obj)) {
                    st_ctime_ns = obj;
                }
            }

            public stat_result(CodeContext/*!*/ context, IEnumerable<object> statResult, [DefaultParameterValue(null)]PythonDictionary dict)
                : this(statResult.ToArray(), dict) { }

            private static bool TryGetDictValue(PythonDictionary dict, string name, out object value) {
                value = null;
                return dict != null && dict.TryGetValue(name, out value);
            }

            private readonly object _atime;
            private readonly object _mtime;
            private readonly object _ctime;

            public object st_mode => this[0];
            public object st_ino => this[1];
            public object st_dev => this[2];
            public object st_nlink => this[3];
            public object st_uid => this[4];
            public object st_gid => this[5];
            public object st_size => this[6];
            public object st_atime => _atime ?? this[7];
            public object st_mtime => _mtime ?? this[8];
            public object st_ctime => _ctime ?? this[9];
            public object st_atime_ns { get; }
            public object st_mtime_ns { get; }
            public object st_ctime_ns { get; }

            public override string/*!*/ __repr__(CodeContext/*!*/ context) {
                return string.Format("nt.stat_result("
                    + "st_mode={0}, "
                    + "st_ino={1}, "
                    + "st_dev={2}, "
                    + "st_nlink={3}, "
                    + "st_uid={4}, "
                    + "st_gid={5}, "
                    + "st_size={6}, "
                    + "st_atime={7}, "
                    + "st_mtime={8}, "
                    + "st_ctime={9})", ToArray());
            }

            public PythonTuple __reduce__() {
                PythonDictionary timeDict = new PythonDictionary(3);
                timeDict["st_atime"] = _atime;
                timeDict["st_mtime"] = _mtime;
                timeDict["st_ctime"] = _ctime;
                timeDict["st_atime_ns"] = st_atime_ns;
                timeDict["st_mtime_ns"] = st_mtime_ns;
                timeDict["st_ctime_ns"] = st_ctime_ns;

                return MakeTuple(
                    DynamicHelpers.GetPythonTypeFromType(typeof(stat_result)),
                    MakeTuple(MakeTuple(this), timeDict)
                );
            }
        }

        private static bool HasExecutableExtension(string path) {
            string extension = Path.GetExtension(path).ToLower(CultureInfo.InvariantCulture);
            return (extension == ".exe" || extension == ".dll" || extension == ".com" || extension == ".bat");
        }

        // Isolate Mono.Unix from the rest of the method so that we don't try to load the Mono.Posix assembly on Windows.
        private static object statUnix(string path) {
            if (Mono.Unix.Native.Syscall.stat(path, out Mono.Unix.Native.Stat buf) == 0) {
                return new stat_result(buf);
            }
            var error = Marshal.GetLastWin32Error();
            return LightExceptions.Throw(PythonExceptions.CreateThrowable(PythonExceptions.OSError, error, strerror(error)));
        }

        private const int OPEN_EXISTING = 3;
        private const int FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const int FILE_READ_ATTRIBUTES = 0x0080;
        private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const int FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            int dwDesiredAccess,
            int dwShareMode,
            IntPtr securityAttrs,
            int dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct BY_HANDLE_FILE_INFORMATION {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [Documentation("stat(path) -> stat result\nGathers statistics about the specified file or directory")]
        [LightThrowing]
        public static object stat(string path, [ParamDictionary]IDictionary<string, object> dict) {
            if (path == null) {
                return LightExceptions.Throw(PythonOps.TypeError("expected string, got NoneType"));
            }

            if (dict != null) {
                foreach (var key in dict.Keys) {
                    switch (key) {
                        case "dir_fd":
                        case "follow_symlinks":
                            // TODO: implement these!
                            break;
                        default:
                            return LightExceptions.Throw(PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", key));
                    }
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                try {
                    FileInfo fi = new FileInfo(path);
                    int mode = 0;
                    long size;

                    if (Directory.Exists(path)) {
                        size = 0;
                        mode = 0x4000 | S_IEXEC;
                    } else if (File.Exists(path)) {
                        size = fi.Length;
                        mode = 0x8000;
                        if (HasExecutableExtension(path)) {
                            mode |= S_IEXEC;
                        }
                    } else {
                        return LightExceptions.Throw(PythonExceptions.CreateThrowable(WindowsError, PythonExceptions._OSError.ERROR_PATH_NOT_FOUND, "file does not exist: " + path, null, PythonExceptions._OSError.ERROR_PATH_NOT_FOUND));
                    }

                    mode |= S_IREAD;
                    if ((fi.Attributes & FileAttributes.ReadOnly) == 0) {
                        mode |= S_IWRITE;
                    }

                    const long epochDifferenceLong = 62135596800 * TimeSpan.TicksPerSecond;

                    // 1 tick = 100 nanoseconds
                    long st_atime_ns = (fi.LastAccessTime.ToUniversalTime().Ticks - epochDifferenceLong) * 100;
                    long st_mtime_ns = (fi.LastWriteTime.ToUniversalTime().Ticks - epochDifferenceLong) * 100;
                    long st_ctime_ns = (fi.CreationTime.ToUniversalTime().Ticks - epochDifferenceLong) * 100;

                    ulong fileIdx = 0;
                    var handle = CreateFile(path, FILE_READ_ATTRIBUTES, 0, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero);
                    if (!handle.IsInvalid) {
                        if (GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION fileInfo)) {
                            fileIdx = (((ulong)fileInfo.FileIndexHigh) << 32) + fileInfo.FileIndexLow;
                        }
                        handle.Close();
                    }

                    return new stat_result(mode, fileIdx, size, st_atime_ns, st_mtime_ns, st_ctime_ns);
                } catch (ArgumentException) {
                    return LightExceptions.Throw(PythonExceptions.CreateThrowable(WindowsError, PythonExceptions._OSError.ERROR_INVALID_NAME, "The path is invalid: " + path, null, PythonExceptions._OSError.ERROR_INVALID_NAME));
                } catch (Exception e) {
                    return LightExceptions.Throw(ToPythonException(e, path));
                }
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return statUnix(path);
            } else {
                throw new PlatformNotSupportedException();
            }
        }

        [LightThrowing]
        public static object stat([BytesConversion]IList<byte> path, [ParamDictionary]IDictionary<string, object> dict)
            => stat(PythonOps.MakeString(path), dict);

        public static string strerror(int code) {
            switch (code) {
                case 0: return "No error";
                case PythonErrorNumber.E2BIG: return "Arg list too long";
                case PythonErrorNumber.EACCES: return "Permission denied";
                case PythonErrorNumber.EAGAIN: return "Resource temporarily unavailable";
                case PythonErrorNumber.EBADF: return "Bad file descriptor";
                case PythonErrorNumber.EBUSY: return "Resource device";
                case PythonErrorNumber.ECHILD: return "No child processes";
                case PythonErrorNumber.EDEADLK: return "Resource deadlock avoided";
                case PythonErrorNumber.EDOM: return "Domain error";
                case PythonErrorNumber.EDQUOT: return "Unknown error";
                case PythonErrorNumber.EEXIST: return "File exists";
                case PythonErrorNumber.EFAULT: return "Bad address";
                case PythonErrorNumber.EFBIG: return "File too large";
                case PythonErrorNumber.EILSEQ: return "Illegal byte sequence";
                case PythonErrorNumber.EINTR: return "Interrupted function call";
                case PythonErrorNumber.EINVAL: return "Invalid argument";
                case PythonErrorNumber.EIO: return "Input/output error";
                case PythonErrorNumber.EISCONN: return "Unknown error";
                case PythonErrorNumber.EISDIR: return "Is a directory";
                case PythonErrorNumber.EMFILE: return "Too many open files";
                case PythonErrorNumber.EMLINK: return "Too many links";
                case PythonErrorNumber.ENAMETOOLONG: return "Filename too long";
                case PythonErrorNumber.ENFILE: return "Too many open files in system";
                case PythonErrorNumber.ENODEV: return "No such device";
                case PythonErrorNumber.ENOENT: return "No such file or directory";
                case PythonErrorNumber.ENOEXEC: return "Exec format error";
                case PythonErrorNumber.ENOLCK: return "No locks available";
                case PythonErrorNumber.ENOMEM: return "Not enough space";
                case PythonErrorNumber.ENOSPC: return "No space left on device";
                case PythonErrorNumber.ENOSYS: return "Function not implemented";
                case PythonErrorNumber.ENOTDIR: return "Not a directory";
                case PythonErrorNumber.ENOTEMPTY: return "Directory not empty";
                case PythonErrorNumber.ENOTSOCK: return "Unknown error";
                case PythonErrorNumber.ENOTTY: return "Inappropriate I/O control operation";
                case PythonErrorNumber.ENXIO: return "No such device or address";
                case PythonErrorNumber.EPERM: return "Operation not permitted";
                case PythonErrorNumber.EPIPE: return "Broken pipe";
                case PythonErrorNumber.ERANGE: return "Result too large";
                case PythonErrorNumber.EROFS: return "Read-only file system";
                case PythonErrorNumber.ESPIPE: return "Invalid seek";
                case PythonErrorNumber.ESRCH: return "No such process";
                case PythonErrorNumber.EXDEV: return "Improper link";
                default:
                    return "Unknown error " + code;
            }
        }

        private static PythonType WindowsError => PythonExceptions.OSError;

#if FEATURE_PROCESS
        [Documentation("system(command) -> int\nExecute the command (a string) in a subshell.")]
        public static int system(string command) {
            ProcessStartInfo psi = GetProcessInfo(command, false);

            if (psi == null) {
                return -1;
            }

            psi.CreateNoWindow = false;

            try {
                Process process = Process.Start(psi);
                if (process == null) {
                    return -1;
                }
                process.WaitForExit();
                return process.ExitCode;
            } catch (Win32Exception) {
                return 1;
            }
        }
#endif

#if FEATURE_FILESYSTEM
        public static object times() {
            System.Diagnostics.Process p = System.Diagnostics.Process.GetCurrentProcess();

            return PythonTuple.MakeTuple(p.UserProcessorTime.TotalSeconds,
                p.PrivilegedProcessorTime.TotalSeconds,
                0,  // child process system time
                0,  // child process os time
                DateTime.Now.Subtract(p.StartTime).TotalSeconds);
        }

        public static void remove(string path) {
            UnlinkWorker(path);
        }

        public static void unlink(string path) {
            UnlinkWorker(path);
        }

        private static void UnlinkWorker(string path) {
            if (path == null) {
                throw new ArgumentNullException(nameof(path));
            } else if (path.IndexOfAny(Path.GetInvalidPathChars()) != -1 || Path.GetFileName(path).IndexOfAny(Path.GetInvalidFileNameChars()) != -1) {
                throw PythonExceptions.CreateThrowable(WindowsError, PythonExceptions._OSError.ERROR_INVALID_NAME, "The filename, directory name, or volume label syntax is incorrect", path, PythonExceptions._OSError.ERROR_INVALID_NAME);
            } else if (!File.Exists(path)) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.FileNotFoundError, PythonExceptions._OSError.ERROR_FILE_NOT_FOUND, "The system cannot find the file specified", path, PythonExceptions._OSError.ERROR_FILE_NOT_FOUND);
            }

            try {
                File.Delete(path);
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }
#endif

#if FEATURE_PROCESS
        public static void unsetenv(string varname) {
            System.Environment.SetEnvironmentVariable(varname, null);
        }
#endif

        public static object urandom(int n) {
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] data = new byte[n];
            rng.GetBytes(data);

            return Bytes.Make(data);
        }

        public static object urandom(BigInteger n) {
            return urandom((int)n);
        }

        public static object urandom(double n) {
            throw PythonOps.TypeError("integer argument expected, got float");
        }

        private static readonly object _umaskKey = new object();

        public static int umask(CodeContext/*!*/ context, int mask) {
            mask &= 0x180;
            object oldMask = context.LanguageContext.GetSetModuleState(_umaskKey, mask);
            if (oldMask == null) {
                return 0;
            } else {
                return (int)oldMask;
            }
        }

        public static int umask(CodeContext/*!*/ context, BigInteger mask) {
            return umask(context, (int)mask);
        }

        public static int umask(double mask) {
            throw PythonOps.TypeError("integer argument expected, got float");
        }

#if FEATURE_FILESYSTEM

        private static void utimeUnix(string path, long atime_ns, long utime_ns) {
            var atime = new Mono.Unix.Native.Timespec();
            atime.tv_sec = atime_ns / 1_000_000_000;
            atime.tv_nsec = atime_ns % 1_000_000_000;
            var utime = new Mono.Unix.Native.Timespec();
            utime.tv_sec = utime_ns / 1_000_000_000;
            utime.tv_nsec = utime_ns % 1_000_000_000;

            if (Mono.Unix.Native.Syscall.utimensat(Mono.Unix.Native.Syscall.AT_FDCWD, path, new[] { atime, utime }, 0) == 0) return;
            var error = Marshal.GetLastWin32Error();
            throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, error, strerror(error));
        }

        public static void utime(string path, [ParamDictionary]IDictionary<string, object> dict)
            => utime(path, null, dict);

        public static void utime(string path, PythonTuple times, [ParamDictionary]IDictionary<string, object> dict) {
            PythonTuple ns = null;
            if (dict != null) {
                foreach (var pair in dict) {
                    switch (pair.Key) {
                        case "ns":
                            if (times != null) {
                                throw PythonOps.ValueError("utime: you may specify either 'times' or 'ns' but not both");
                            }
                            if (pair.Value is PythonTuple t && t.__len__() == 2) {
                                ns = t;
                            } else {
                                throw PythonOps.TypeError("utime: 'ns' must be a tuple of two ints");
                            }
                            break;
                        case "dir_fd":
                        case "follow_symlinks":
                            // TODO: implement these!
                            break;
                        default:
                            throw PythonOps.TypeError("'{0}' is an invalid keyword argument for this function", pair.Key);
                    }
                }
            }

            if (times != null && times.__len__() != 2) {
                throw PythonOps.TypeError("times value must be a 2-value tuple (atime, mtime)");
            }

            // precision is lost when using FileInfo on Linux, use a syscall instead
            if (ns != null && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                utimeUnix(path, Converter.ConvertToInt64(ns[0]), Converter.ConvertToInt64(ns[1]));
                return;
            }

            try {
                // Create a DirectoryInfo or FileInfo depending on what it is
                // Changing the times of a directory does not work with a FileInfo and v.v.
                var fi = Directory.Exists(path) ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path);
                var atime = DateTime.Now;
                var mtime = DateTime.Now;
                if (times != null) {
                    atime = new DateTime(PythonTime.TimestampToTicks(Converter.ConvertToDouble(times[0])), DateTimeKind.Utc);
                    mtime = new DateTime(PythonTime.TimestampToTicks(Converter.ConvertToDouble(times[1])), DateTimeKind.Utc);
                } else if (ns != null) {
                    const long epochDifferenceLong = 62135596800 * TimeSpan.TicksPerSecond;
                    atime = new DateTime(Converter.ConvertToInt64(ns[0]) / 100 + epochDifferenceLong, DateTimeKind.Utc);
                    mtime = new DateTime(Converter.ConvertToInt64(ns[1]) / 100 + epochDifferenceLong, DateTimeKind.Utc);
                }
                fi.LastAccessTime = atime;
                fi.LastWriteTime = mtime;
            } catch (Exception e) {
                throw ToPythonException(e, path);
            }
        }

#endif

#if FEATURE_PROCESS
        public static PythonTuple waitpid(int pid, object options) {
            Process process;
            lock (_processToIdMapping) {
                if (!_processToIdMapping.TryGetValue(pid, out process)) {
                    throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, PythonErrorNumber.ECHILD, "No child processes");
                }
            }

            process.WaitForExit();
            PythonTuple res = PythonTuple.MakeTuple(pid, process.ExitCode);

            lock (_processToIdMapping) {
                // lower 3 bits are user defined and ignored (matching NT's handle semantics)
                _processToIdMapping.Remove(pid & ~0x03);
                _freeProcessIds.Add(pid & ~0x03);
            }

            return res;
        }
#endif

        public static int write(CodeContext/*!*/ context, int fd, [BytesConversion]IList<byte> text) {
            try {
                PythonContext pythonContext = context.LanguageContext;
                var pf = pythonContext.FileManager.GetFileFromId(pythonContext, fd);
                pf.write(context, text);
                return text.Count;
            } catch (Exception e) {
                throw ToPythonException(e);
            }
        }

#if FEATURE_PROCESS

        [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
        private static extern int sys_kill(int pid, int sig);


        [Documentation(@"Send signal sig to the process pid. Constants for the specific signals available on the host platform 
are defined in the signal module.")]
        public static void kill(CodeContext/*!*/ context, int pid, int sig) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                if (sys_kill(pid, sig) == 0) return;

                var error = Marshal.GetLastWin32Error();
                throw PythonExceptions.CreateThrowable(PythonExceptions.OSError, error, strerror(error));
            } else {
                if (PythonSignal.NativeSignal.GenerateConsoleCtrlEvent((uint)sig, (uint)pid)) return;

                // If the calls to GenerateConsoleCtrlEvent didn't work, simply
                // forcefully kill the process.
                Process toKill = Process.GetProcessById(pid);
                toKill.Kill();
            }
        }

#endif

        public const int O_APPEND = 0x8;
        public const int O_CREAT = 0x100;
        public const int O_TRUNC = 0x200;

        public const int O_EXCL = 0x400;
        public const int O_NOINHERIT = 0x80;

        public const int O_RANDOM = 0x10;
        public const int O_SEQUENTIAL = 0x20;

        public const int O_SHORT_LIVED = 0x1000;
        public const int O_TEMPORARY = 0x40;

        public const int O_WRONLY = 0x1;
        public const int O_RDONLY = 0x0;
        public const int O_RDWR = 0x2;

        public const int O_BINARY = 0x8000;
        public const int O_TEXT = 0x4000;

        public const int P_WAIT = 0;
        public const int P_NOWAIT = 1;
        public const int P_NOWAITO = 3;

        // Not used by IronPython
        public const int P_OVERLAY = 2;
        public const int P_DETACH = 4;

        public const int TMP_MAX = 32767;


        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WNOHANG = 1;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WUNTRACED = 2;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WCONTINUED = 8;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WSTOPPED = 2;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WEXITED = 4;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int WNOWAIT = 0x1000000;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int P_ALL = 0;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int P_PID = 1;

        [PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public const int P_PGID = 2;


        [Documentation(@"WCOREDUMP(status) -> bool

Return True if the process returning 'status' was dumped to a core file."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static bool WCOREDUMP(int status) {
            //#define WCOREDUMP(s) ((s) & 0x80)
            return (status & 0x80) != 0;
        }

        [Documentation(@"WIFCONTINUED(status) -> bool

Return True if the process returning 'status' was continued from a
job control stop."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static bool WIFCONTINUED(int status) {
            //#define WIFCONTINUED(s) ((s) == 0xffff)
            return status == 0xffff;
        }

        [Documentation(@"WIFSTOPPED(status) -> bool

Return True if the process returning 'status' was stopped."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static bool WIFSTOPPED(int status) {
            //#define WIFSTOPPED(s) (((s) & 0xff) == 0x7f)
            return (status & 0xff) == 0x7f;
        }

        [Documentation(@"WIFSIGNALED(status) -> bool

Return True if the process returning 'status' was terminated by a signal."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static bool WIFSIGNALED(int status) {
            return ((byte)((status & 0x7f) >> 1)) > 0;
        }

        [Documentation(@"WIFEXITED(status) -> bool

Return true if the process returning 'status' exited using the exit()
system call."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static bool WIFEXITED(int status) {
            return WTERMSIG(status) == 0;
        }

        [Documentation(@"WEXITSTATUS(status) -> integer

Return the process return code from 'status'."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static int WEXITSTATUS(int status) {
            // #define WEXITSTATUS(s) (((s) & 0xff00) >> 8)
            return (status & 0xff00) >> 8;
        }

        [Documentation(@"WTERMSIG(status) -> integer

Return the signal that terminated the process that provided the 'status'
value."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static int WTERMSIG(int status) {
            //#define WTERMSIG(s) ((s) & 0x7f)
            return (status & 0x7f);
        }

        [Documentation(@"WSTOPSIG(status) -> integer

Return the signal that stopped the process that provided
the 'status' value."),
            PythonHidden(PlatformsAttribute.PlatformFamily.Windows)]
        public static int WSTOPSIG(int status) {
            // #define WSTOPSIG(s) WEXITSTATUS(s)
            return WEXITSTATUS(status);
        }

        #endregion

        #region Private implementation details

        private static Exception ToPythonException(Exception e) {
            return ToPythonException(e, null);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1404:CallGetLastErrorImmediatelyAfterPInvoke")]
        private static Exception ToPythonException(Exception e, string filename) {
            if (e is IPythonAwareException) {
                return e;
            }

            if (e is ArgumentException || e is ArgumentNullException || e is ArgumentTypeException) {
                // rethrow reasonable exceptions
                return ExceptionHelpers.UpdateForRethrow(e);
            }

            int error = Marshal.GetLastWin32Error();

            string message = e.Message;
            int errorCode = 0;

            bool isWindowsError = false;
            if (e is Win32Exception winExcep) {
                errorCode = winExcep.NativeErrorCode;
                message = GetFormattedException(e, errorCode);
                isWindowsError = true;
            } else {
                if (e is UnauthorizedAccessException unauth) {
                    isWindowsError = true;
                    errorCode = PythonExceptions._OSError.ERROR_ACCESS_DENIED;
                    if (filename != null) {
                        message = string.Format("Access is denied: '{0}'", filename);
                    } else {
                        message = "Access is denied";
                    }
                }

                if (e is IOException ioe) {
                    switch (error) {
                        case PythonExceptions._OSError.ERROR_DIR_NOT_EMPTY:
                            throw PythonExceptions.CreateThrowable(WindowsError, error, "The directory is not empty", null, error);
                        case PythonExceptions._OSError.ERROR_ACCESS_DENIED:
                            throw PythonExceptions.CreateThrowable(WindowsError, error, "Access is denied", null, error);
                        case PythonExceptions._OSError.ERROR_SHARING_VIOLATION:
                            throw PythonExceptions.CreateThrowable(WindowsError, error, "The process cannot access the file because it is being used by another process", null, error);
                    }
                }

                errorCode = System.Runtime.InteropServices.Marshal.GetHRForException(e);

                if ((errorCode & ~0xfff) == (unchecked((int)0x80070000))) {
                    // Win32 HR, translate HR to Python error code if possible, otherwise
                    // report the HR.
                    errorCode = errorCode & 0xfff;
                    message = GetFormattedException(e, errorCode);
                    isWindowsError = true;
                }
            }

            if (isWindowsError) {
                return PythonExceptions.CreateThrowable(WindowsError, errorCode, message, null, errorCode);
            }

            return PythonExceptions.CreateThrowable(PythonExceptions.OSError, errorCode, message);
        }

        private static string GetFormattedException(Exception e, int hr) {
            return "[Errno " + hr.ToString() + "] " + e.Message;
        }

        // Win32 error codes

        private const int S_IWRITE = 0x80 + 0x10 + 0x02; // owner / group / world
        private const int S_IREAD = 0x100 + 0x20 + 0x04; // owner / group / world
        private const int S_IEXEC = 0x40 + 0x08 + 0x01; // owner / group / world

        public const int F_OK = 0;
        public const int X_OK = 1;
        public const int W_OK = 2;
        public const int R_OK = 4;

        private static void addBase(string[] files, PythonList ret) {
            foreach (string file in files) {
                ret.AddNoLock(Path.GetFileName(file));
            }
        }

        private static FileMode FileModeFromFlags(int flags) {
            if ((flags & O_APPEND) != 0) return FileMode.Append;
            if ((flags & O_EXCL) != 0) {
                if ((flags & O_CREAT) != 0) {
                    return FileMode.CreateNew;
                }

                return FileMode.Open;
            }
            if ((flags & O_CREAT) != 0) return FileMode.Create;
            if ((flags & O_TRUNC) != 0) return FileMode.Truncate;

            return FileMode.Open;
        }

        private static FileAccess FileAccessFromFlags(int flags) {
            if ((flags & O_RDWR) != 0) return FileAccess.ReadWrite;
            if ((flags & O_WRONLY) != 0) return FileAccess.Write;

            return FileAccess.Read;
        }

#if FEATURE_PROCESS

        private static ProcessStartInfo GetProcessInfo(string command, bool throwException) {
            // TODO: always run through cmd.exe ?
            command = command.Trim();
            string baseCommand, args;
            if (!TryGetExecutableCommand(command, out baseCommand, out args)) {
                if (!TryGetShellCommand(command, out baseCommand, out args)) {
                    if (throwException) {
                        throw PythonOps.WindowsError("The system can not find command '{0}'", command);
                    } else {
                        return null;
                    }
                }
            }

            ProcessStartInfo psi = new ProcessStartInfo(baseCommand, args);
            psi.UseShellExecute = false;

            return psi;
        }

        private static bool TryGetExecutableCommand(string command, out string baseCommand, out string args) {
            baseCommand = command;
            args = String.Empty;
            int pos;

            if (command[0] == '\"') {
                for (pos = 1; pos < command.Length; pos++) {
                    if (command[pos] == '\"') {
                        baseCommand = command.Substring(1, pos - 1).Trim();
                        if (pos + 1 < command.Length) {
                            args = command.Substring(pos + 1);
                        }
                        break;
                    }
                }

                if (pos == command.Length) {
                    baseCommand = command.Substring(1).Trim();
                    command = command + "\"";
                }
            } else {
                pos = command.IndexOf(' ');
                if (pos != -1) {
                    baseCommand = command.Substring(0, pos);
                    // pos won't be the last one
                    args = command.Substring(pos + 1);
                }
            }
            string fullpath = Path.GetFullPath(baseCommand);
            if (File.Exists(fullpath)) {
                baseCommand = fullpath;
                return true;
            }

            // TODO: need revisit
            string sysdir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
            foreach (string suffix in new string[] { string.Empty, ".com", ".exe", "cmd", ".bat" }) {
                fullpath = Path.Combine(sysdir, baseCommand + suffix);
                if (File.Exists(fullpath)) {
                    baseCommand = fullpath;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetShellCommand(string command, out string baseCommand, out string args) {
            baseCommand = Environment.GetEnvironmentVariable("COMSPEC");
            args = String.Empty;
            if (baseCommand == null) {
                baseCommand = Environment.GetEnvironmentVariable("SHELL");
                if (baseCommand == null) {
                    return false;
                }
                args = String.Format("-c \"{0}\"", command);
            } else {
                args = String.Format("/c {0}", command);
            }
            return true;
        }

#endif

        private static Exception DirectoryExists() {
            return PythonExceptions.CreateThrowable(PythonExceptions.FileExistsError, PythonExceptions._OSError.ERROR_ALREADY_EXISTS, "directory already exists", null, PythonExceptions._OSError.ERROR_ALREADY_EXISTS);
        }

        #endregion
    }
}
